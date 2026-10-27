using System.Security.Claims;
using AgentLedger.UseCases.Users.CheckSession;
using AgentLedger.Web.Users;
using Ardalis.Result;

namespace AgentLedger.UnitTests.Web;

// The security stamp check shared by sign-in cookies and open interactive pages (ADR 0018, ADR 0023): a password
// change or reset ends both.
public sealed class SessionCheckIsValid
{
  private readonly IMediator _mediator = Substitute.For<IMediator>();
  private static readonly Guid User = Guid.NewGuid();
  private static readonly Guid Stamp = Guid.NewGuid();

  [Fact]
  public async Task WhenTheStampIsCurrent()
  {
    _mediator.Send(new CheckSessionQuery(User, Stamp), Arg.Any<CancellationToken>()).Returns(Result.Success());

    (await new SessionCheck(_mediator).IsValidAsync(Principal(User, Stamp), CancellationToken.None)).ShouldBeTrue();
  }

  [Fact]
  public async Task NotWhenTheStampChanged()
  {
    _mediator.Send(new CheckSessionQuery(User, Stamp), Arg.Any<CancellationToken>()).Returns(Result.Unauthorized());

    (await new SessionCheck(_mediator).IsValidAsync(Principal(User, Stamp), CancellationToken.None)).ShouldBeFalse();
  }

  [Fact]
  public async Task NotWithoutTheClaims()
  {
    (await new SessionCheck(_mediator).IsValidAsync(new ClaimsPrincipal(new ClaimsIdentity()), CancellationToken.None)).ShouldBeFalse();
    await _mediator.DidNotReceive().Send(Arg.Any<CheckSessionQuery>(), Arg.Any<CancellationToken>());
  }

  private static ClaimsPrincipal Principal(Guid user, Guid stamp) =>
    new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(SessionCheck.SecurityStampClaim, stamp.ToString())],
      "Cookies"));
}
