using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UseCases.Users;
using AgentLedger.UseCases.Users.SignIn;
using Ardalis.Result;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.Users;

public sealed class SignInHandlerHandle
{
  private const string Password = "correct horse battery staple";

  private readonly List<User> _users = [];
  private readonly IRepository<User> _repository;
  private readonly FakeTimeProvider _clock = new(UserBuilder.Now);
  private readonly FakePasswordHasher _hasher = new();
  private readonly SignInHandler _handler;
  private readonly User _eric;

  public SignInHandlerHandle()
  {
    _repository = UserRepositoryFake.Create(_users);
    _handler = new SignInHandler(_repository, _hasher, _clock);
    _eric = new UserBuilder().WithUsername("eric").WithPasswordHash(_hasher.Hash(Password)).Build();
    _users.Add(_eric);
  }

  private ValueTask<Result<SignedInUser>> SignIn(string username, string password) =>
    _handler.Handle(new SignInCommand(username, password), CancellationToken.None);

  [Fact]
  public async Task SignsInWithTheRightPassword()
  {
    var result = await SignIn("Eric", Password); // usernames are case-insensitive

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(new SignedInUser(_eric.Id.Value, "eric", _eric.SecurityStamp.Value, _eric.IsAdmin));
  }

  [Fact]
  public async Task RejectsAWrongPasswordAndCountsIt()
  {
    var result = await SignIn("eric", "wrong password, quite long");

    result.Status.ShouldBe(ResultStatus.Unauthorized);
    _eric.FailedSignInCount.ShouldBe(1);
    await _repository.Received(1).UpdateAsync(_eric, Arg.Any<CancellationToken>());
  }

  [Theory]
  [InlineData("nobody")]
  [InlineData("not a valid username")]
  public async Task RejectsAnUnknownUsernameTheSameWayAfterCheckingAPassword(string username)
  {
    // Same result and a real password check, so neither the answer nor the timing reveals which usernames exist.
    var result = await SignIn(username, Password);

    result.Status.ShouldBe(ResultStatus.Unauthorized);
    result.Errors.ShouldBe((await SignIn("eric", "wrong password, quite long")).Errors);
    _hasher.Checked.ShouldContain(_hasher.UnusableHash);
  }

  [Fact]
  public async Task LocksTheUserAtTheFifthWrongPassword()
  {
    for (var i = 0; i < User.MaxFailedSignIns - 1; i++)
    {
      (await SignIn("eric", "wrong password, quite long")).Status.ShouldBe(ResultStatus.Unauthorized);
    }

    (await SignIn("eric", "wrong password, quite long")).Status.ShouldBe(ResultStatus.Forbidden);
  }

  [Fact]
  public async Task RefusesALockedUserEvenWithTheRightPasswordWithoutCheckingIt()
  {
    for (var i = 0; i < User.MaxFailedSignIns; i++)
    {
      _eric.RecordFailedSignIn(_clock.GetUtcNow());
    }
    _hasher.Checked.Clear();

    var result = await SignIn("eric", Password);

    result.Status.ShouldBe(ResultStatus.Forbidden);
    _hasher.Checked.ShouldBeEmpty();
  }

  [Fact]
  public async Task SignsInAgainOnceTheLockoutEnds()
  {
    for (var i = 0; i < User.MaxFailedSignIns; i++)
    {
      _eric.RecordFailedSignIn(_clock.GetUtcNow());
    }
    _clock.Advance(User.LockoutDuration);

    (await SignIn("eric", Password)).IsSuccess.ShouldBeTrue();
    _eric.FailedSignInCount.ShouldBe(0);
  }

  [Fact]
  public async Task UpgradesAnOldHashButKeepsTheSecurityStamp()
  {
    var stamp = _eric.SecurityStamp;
    _eric.UpgradePasswordHash(PasswordHash.From($"old:{Password}"));

    (await SignIn("eric", Password)).IsSuccess.ShouldBeTrue();

    _eric.PasswordHash.ShouldBe(_hasher.Hash(Password));
    _eric.SecurityStamp.ShouldBe(stamp);
    await _repository.Received(1).UpdateAsync(_eric, Arg.Any<CancellationToken>());
  }
}
