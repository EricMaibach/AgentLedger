using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UseCases.Users;
using AgentLedger.UseCases.Users.ChangePassword;
using Ardalis.Result;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.Users;

public sealed class ChangePasswordHandlerHandle
{
  private const string Current = "correct horse battery staple";
  private const string New = "a completely different passphrase";

  private readonly List<User> _users = [];
  private readonly IRepository<User> _repository;
  private readonly FakeTimeProvider _clock = new(UserBuilder.Now);
  private readonly FakePasswordHasher _hasher = new();
  private readonly ChangePasswordHandler _handler;
  private readonly User _eric;

  public ChangePasswordHandlerHandle()
  {
    _repository = UserRepositoryFake.Create(_users);
    _handler = new ChangePasswordHandler(_repository, _hasher, _clock);
    _eric = new UserBuilder().WithPasswordHash(_hasher.Hash(Current)).Build();
    _users.Add(_eric);
  }

  private ValueTask<Result<SignedInUser>> Change(string current, string @new) =>
    _handler.Handle(new ChangePasswordCommand(_eric.Id.Value, current, @new), CancellationToken.None);

  [Fact]
  public async Task ChangesThePasswordAndTheSecurityStamp()
  {
    var oldStamp = _eric.SecurityStamp;

    var result = await Change(Current, New);

    result.IsSuccess.ShouldBeTrue();
    _eric.PasswordHash.ShouldBe(_hasher.Hash(New));
    _eric.SecurityStamp.ShouldNotBe(oldStamp);
    // The new stamp is returned so the current session can continue while the others end.
    result.Value.SecurityStamp.ShouldBe(_eric.SecurityStamp.Value);
    await _repository.Received(1).UpdateAsync(_eric, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task RejectsAWrongCurrentPasswordAndCountsIt()
  {
    // Counted like a wrong sign-in, so a stolen session can't be used to guess the password.
    var result = await Change("not the current password", New);

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("currentPassword");
    _eric.PasswordHash.ShouldBe(_hasher.Hash(Current));
    _eric.FailedSignInCount.ShouldBe(1);
  }

  [Fact]
  public async Task RejectsANewPasswordThatBreaksTheRules()
  {
    var result = await Change(Current, "short");

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("newPassword");
    _eric.PasswordHash.ShouldBe(_hasher.Hash(Current));
  }

  [Fact]
  public async Task RefusesALockedUser()
  {
    for (var i = 0; i < User.MaxFailedSignIns; i++)
    {
      _eric.RecordFailedSignIn(_clock.GetUtcNow());
    }

    (await Change(Current, New)).Status.ShouldBe(ResultStatus.Forbidden);
  }

  [Fact]
  public async Task IsNotFoundForAnUnknownUser()
  {
    var result = await _handler.Handle(new ChangePasswordCommand(Guid.CreateVersion7(), Current, New), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.NotFound);
  }
}
