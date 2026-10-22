using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UseCases.Users.CheckSession;
using Ardalis.Result;

namespace AgentLedger.UnitTests.UseCases.Users;

// Re-validates a sign-in cookie: it stays valid only while its security stamp matches the user's (ADR 0018).
public sealed class CheckSessionHandlerHandle
{
  private readonly List<User> _users = [];
  private readonly CheckSessionHandler _handler;
  private readonly User _eric = new UserBuilder().Build();

  public CheckSessionHandlerHandle()
  {
    _users.Add(_eric);
    _handler = new CheckSessionHandler(UserRepositoryFake.CreateReadOnly(_users));
  }

  [Fact]
  public async Task AcceptsTheCurrentStamp()
  {
    var result = await _handler.Handle(new CheckSessionQuery(_eric.Id.Value, _eric.SecurityStamp.Value), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task RejectsAnOldStamp()
  {
    var oldStamp = _eric.SecurityStamp.Value;
    _eric.ChangePassword(PasswordHash.From("hashed:new"), SecurityStamp.From(Guid.CreateVersion7()));

    var result = await _handler.Handle(new CheckSessionQuery(_eric.Id.Value, oldStamp), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Unauthorized);
  }

  [Fact]
  public async Task RejectsAnUnknownUser()
  {
    var result = await _handler.Handle(new CheckSessionQuery(Guid.CreateVersion7(), Guid.CreateVersion7()), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Unauthorized);
  }
}
