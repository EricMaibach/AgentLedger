using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AgentLedger.Web.Users;

// Cookie authentication hooks (ADR 0018): the JSON API answers 401 instead of redirecting to the login page,
// and a cookie is re-checked against the user's security stamp at an interval, so a password change or reset
// ends other sessions.
internal sealed class SessionCookieEvents(IMediator mediator, TimeProvider clock, IConfiguration configuration)
  : CookieAuthenticationEvents
{
  private const string CheckedAtItem = "agentledger:checked_at";

  public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
  {
    var request = context.Request;
    if (request.Path.StartsWithSegments("/api"))
    {
      context.Response.StatusCode = StatusCodes.Status401Unauthorized;
      return Task.CompletedTask;
    }

    // A local redirect back to where the visitor was going.
    var returnUrl = request.PathBase + request.Path + request.QueryString;
    context.Response.Redirect(context.Options.LoginPath + QueryString.Create(context.Options.ReturnUrlParameter, returnUrl));
    return Task.CompletedTask;
  }

  public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
  {
    context.Response.StatusCode = StatusCodes.Status403Forbidden;
    return Task.CompletedTask;
  }

  public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
  {
    var now = clock.GetUtcNow();
    var interval = configuration.GetValue("Authentication:SessionCheckInterval", TimeSpan.FromMinutes(5));
    if (context.Properties.Items.TryGetValue(CheckedAtItem, out var value)
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var checkedAt)
        && now - checkedAt < interval)
    {
      return;
    }

    var valid = context.Principal is { } principal
      && await new SessionCheck(mediator).IsValidAsync(principal, context.HttpContext.RequestAborted);

    if (!valid)
    {
      context.RejectPrincipal();
      await SessionCookie.SignOutAsync(context.HttpContext);
      return;
    }

    context.Properties.Items[CheckedAtItem] = now.ToString("O", CultureInfo.InvariantCulture);
    context.ShouldRenew = true;
  }
}
