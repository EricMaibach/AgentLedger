using System.Threading.RateLimiting;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.UseCases.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace AgentLedger.Web.Users;

/// <summary>Password login (ADR 0018): cookie sessions, deny by default, and a limit on login attempts.</summary>
public static class AuthenticationConfig
{
  /// <summary>Ingest: an API key with the ingest scope, always (ADR 0019).</summary>
  public const string IngestPolicy = "Ingest";

  /// <summary>The JSON read API: a signed-in browser, or an API key with the read scope (ADR 0019).</summary>
  public const string ReadApiPolicy = "ReadApi";

  /// <summary>Any valid API key, whatever its scope: for a key's holder to check it.</summary>
  public const string AnyApiKeyPolicy = "AnyApiKey";

  /// <summary>Login attempts allowed per IP address per minute, unless configured otherwise.</summary>
  public const int DefaultLoginAttemptsPerMinute = 10;

  /// <summary>Registers authentication, the fallback authorization policy and the login rate limit.</summary>
  public static IServiceCollection AddLedgerAuthentication(this IServiceCollection services, IConfiguration configuration)
  {
    // Configurable so tests that sign in many times aren't limited; production uses the default.
    var loginAttemptsPerMinute = configuration.GetValue("Authentication:LoginAttemptsPerMinute", DefaultLoginAttemptsPerMinute);

    services.AddSingleton<SetupWindow>();
    services.AddScoped<SessionCookieEvents>();
    services.AddScoped<SessionCheck>();
    // Interactive pages (ADR 0023): the signed-in user, rechecked while the page stays open.
    services.AddCascadingAuthenticationState();
    services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, CircuitSessionRevalidation>();

    services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
      .AddCookie(options =>
      {
        options.Cookie.Name = "agentledger.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Secure once served over HTTPS
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.ReturnUrlParameter = "returnUrl";
        options.EventsType = typeof(SessionCookieEvents);
      })
      // Used only where a policy names it, so a key never opens a page.
      .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);

    const string cookie = CookieAuthenticationDefaults.AuthenticationScheme;
    const string apiKey = ApiKeyAuthenticationHandler.SchemeName;

    services.AddAuthorizationBuilder()
      // Deny by default: everything needs a signed-in user unless it is explicitly marked AllowAnonymous.
      .SetFallbackPolicy(new AuthorizationPolicyBuilder(cookie).RequireAuthenticatedUser().Build())
      .AddPolicy(IngestPolicy, policy => policy
        .AddAuthenticationSchemes(apiKey)
        .RequireClaim(ApiKeyAuthenticationHandler.ScopeClaim, ApiKeyScope.Ingest.Name))
      .AddPolicy(ReadApiPolicy, policy => policy
        .AddAuthenticationSchemes(cookie, apiKey)
        .RequireAuthenticatedUser()
        .RequireAssertion(context =>
          context.User.Identities.Any(identity => identity.IsAuthenticated && identity.AuthenticationType == cookie)
          || context.User.HasClaim(ApiKeyAuthenticationHandler.ScopeClaim, ApiKeyScope.Read.Name)))
      .AddPolicy(AnyApiKeyPolicy, policy => policy
        .AddAuthenticationSchemes(apiKey)
        .RequireAuthenticatedUser());

    // Only login attempts are limited, not loading the page. Behind a reverse proxy every request has the
    // proxy's address; forwarded headers come with HTTPS support (roadmap, Phase 2 step 4).
    services.AddRateLimiter(options =>
    {
      options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
      options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        HttpMethods.IsPost(http.Request.Method) && http.Request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase)
          ? RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
              _ => new FixedWindowRateLimiterOptions { PermitLimit = loginAttemptsPerMinute, Window = TimeSpan.FromMinutes(1) })
          : RateLimitPartition.GetNoLimiter(string.Empty));
    });

    return services;
  }

  /// <summary>Sign-out: a POST with the antiforgery token, so another site can't sign you out.</summary>
  public static IEndpointRouteBuilder MapSignOut(this IEndpointRouteBuilder endpoints)
  {
    endpoints.MapPost("/logout", async (HttpContext http, IAntiforgery antiforgery) =>
    {
      if (!await antiforgery.IsRequestValidAsync(http))
      {
        return Results.BadRequest();
      }

      await SessionCookie.SignOutAsync(http);
      return Results.Redirect("/login");
    }).ExcludeFromDescription();

    return endpoints;
  }
}
