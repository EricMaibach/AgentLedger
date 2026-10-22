using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UseCases.Users;
using AgentLedger.UseCases.Users.GetSetupStatus;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.Users;

public sealed class GetSetupStatusHandlerHandle
{
  private readonly List<User> _users = [];
  private readonly FakeTimeProvider _clock = new(UserBuilder.Now);
  private readonly GetSetupStatusHandler _handler;

  public GetSetupStatusHandlerHandle() =>
    _handler = new GetSetupStatusHandler(UserRepositoryFake.CreateReadOnly(_users), new SetupWindow(_clock));

  [Fact]
  public async Task IsRequiredWhileThereAreNoUsers()
  {
    (await _handler.Handle(new GetSetupStatusQuery(), CancellationToken.None)).Value.ShouldBe(SetupStatus.Required);
  }

  [Fact]
  public async Task HasTimedOutWhenNoUserWasCreatedInTime()
  {
    _clock.Advance(SetupWindow.Duration);

    (await _handler.Handle(new GetSetupStatusQuery(), CancellationToken.None)).Value.ShouldBe(SetupStatus.TimedOut);
  }

  [Fact]
  public async Task IsDoneOnceAUserExists()
  {
    _users.Add(new UserBuilder().Build());
    _clock.Advance(SetupWindow.Duration); // the window doesn't matter any more

    (await _handler.Handle(new GetSetupStatusQuery(), CancellationToken.None)).Value.ShouldBe(SetupStatus.Done);
  }
}
