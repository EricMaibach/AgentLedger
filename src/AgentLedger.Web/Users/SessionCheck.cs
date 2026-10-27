using System.Security.Claims;
using AgentLedger.UseCases.Users.CheckSession;

namespace AgentLedger.Web.Users;

/// <summary>
/// Whether a signed-in principal's security stamp is still current (ADR 0018): checked for sign-in cookies and for
/// open interactive pages, so a password change or reset ends both.
/// </summary>
public sealed class SessionCheck(IMediator mediator)
{
  public const string SecurityStampClaim = SessionCookie.SecurityStampClaim;

  public async Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken) =>
    SessionCookie.TryRead(principal, out var userId, out var stamp)
    && (await mediator.Send(new CheckSessionQuery(userId, stamp), cancellationToken)).IsSuccess;
}
