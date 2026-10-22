using System.Threading.RateLimiting;
using AgentLedger.UseCases.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace AgentLedger.Web.Users;

/// <summary>Password login (ADR 0018): cookie sessions, deny by default, and a limit on login attempts.</summary>
public static class AuthenticationConfig
{
  /// <summary>Login attempts allowed per IP address per minute, unless configured otherwise.</summary>
  public const int DefaultLoginAttemptsPerMinute = 10;

  /// <summary>Registers authentication, the fallback authorization policy and the login rate limit.</summary>
  public static IServiceCollection AddLedgerAuthentication(this IServiceCollection services, IConfiguration configuration)
  {
    // Configurable so tests that sign in many times aren't limited; production uses the default.
    var loginAttemptsPerMinute = configuration.GetValue("Authentication:LoginAttemptsPerMinute", DefaultLoginAttemptsPerMinute);

    services.AddSingleton<SetupWindow>();
    services.AddScoped<SessionCookieEvents>();

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
      });

    // Deny by default: everything needs a signed-in user unless it is explicitly marked AllowAnonymous.
    services.AddAuthorizationBuilder()
      .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

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
