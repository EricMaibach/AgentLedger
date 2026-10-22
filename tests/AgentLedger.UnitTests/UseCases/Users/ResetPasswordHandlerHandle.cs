using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UseCases.Users.ResetPassword;
using Ardalis.Result;

namespace AgentLedger.UnitTests.UseCases.Users;

// The admin's way back in (ADR 0018): run in the container, prints a new random password.
public sealed class ResetPasswordHandlerHandle
{
  private readonly List<User> _users = [];
  private readonly IRepository<User> _repository;
  private readonly FakePasswordHasher _hasher = new();
  private readonly ResetPasswordHandler _handler;
  private readonly User _eric;

  public ResetPasswordHandlerHandle()
  {
    _repository = UserRepositoryFake.Create(_users);
    _handler = new ResetPasswordHandler(_repository, _hasher);
    _eric = new UserBuilder().WithUsername("eric").Build();
    _users.Add(_eric);
  }

  [Fact]
  public async Task SetsANewRandomPasswordThatFollowsTheRules()
  {
    var oldStamp = _eric.SecurityStamp;

    var result = await _handler.Handle(new ResetPasswordCommand("Eric"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    PasswordRules.Check(result.Value).IsSuccess.ShouldBeTrue();
    _eric.PasswordHash.ShouldBe(_hasher.Hash(result.Value));
    _eric.SecurityStamp.ShouldNotBe(oldStamp); // signs out existing sessions
    await _repository.Received(1).UpdateAsync(_eric, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task GeneratesADifferentPasswordEachTime()
  {
    var first = await _handler.Handle(new ResetPasswordCommand("eric"), CancellationToken.None);
    var second = await _handler.Handle(new ResetPasswordCommand("eric"), CancellationToken.None);

    first.Value.ShouldNotBe(second.Value);
  }

  [Fact]
  public async Task UnlocksALockedUser()
  {
    for (var i = 0; i < User.MaxFailedSignIns; i++)
    {
      _eric.RecordFailedSignIn(UserBuilder.Now);
    }

    await _handler.Handle(new ResetPasswordCommand("eric"), CancellationToken.None);

    _eric.IsLockedOut(UserBuilder.Now).ShouldBeFalse();
  }

  [Theory]
  [InlineData("nobody")]
  [InlineData("not a valid username")]
  public async Task IsNotFoundForAnUnknownUser(string username)
  {
    (await _handler.Handle(new ResetPasswordCommand(username), CancellationToken.None)).Status.ShouldBe(ResultStatus.NotFound);
  }
}
