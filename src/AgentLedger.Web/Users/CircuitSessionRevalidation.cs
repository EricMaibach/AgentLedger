using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace AgentLedger.Web.Users;

/// <summary>
/// An open interactive page keeps the user it connected as (ADR 0023). This rechecks their security stamp at the same
/// interval as the cookie (ADR 0018); when it fails, the page's authentication state becomes signed out.
/// </summary>
public sealed class CircuitSessionRevalidation(ILoggerFactory loggerFactory, IServiceScopeFactory scopes, IConfiguration configuration)
  : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
  // Tests set the cookie interval to zero; an open page needn't recheck more often than this.
  private static readonly TimeSpan Minimum = TimeSpan.FromSeconds(30);

  protected override TimeSpan RevalidationInterval
  {
    get
    {
      var interval = configuration.GetValue("Authentication:SessionCheckInterval", TimeSpan.FromMinutes(5));
      return interval < Minimum ? Minimum : interval;
    }
  }

  protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState authenticationState,
    CancellationToken cancellationToken)
  {
    using var scope = scopes.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<SessionCheck>().IsValidAsync(authenticationState.User, cancellationToken);
  }
}
