using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UseCases.Users;
using AgentLedger.UseCases.Users.SetUpFirstAdmin;
using Ardalis.Result;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.Users;

public sealed class SetUpFirstAdminHandlerHandle
{
  private const string Password = "correct horse battery staple";

  private readonly List<User> _users = [];
  private readonly FakeTimeProvider _clock = new(UserBuilder.Now);
  private readonly FakePasswordHasher _hasher = new();
  private readonly SetUpFirstAdminHandler _handler;

  public SetUpFirstAdminHandlerHandle() =>
    _handler = new SetUpFirstAdminHandler(UserRepositoryFake.Create(_users), _hasher, new SetupWindow(_clock));

  [Fact]
  public async Task CreatesAnAdminWithAHashedPassword()
  {
    var result = await _handler.Handle(new SetUpFirstAdminCommand("Eric", Password), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Created);
    var user = _users.ShouldHaveSingleItem();
    user.Username.ShouldBe(Username.From("eric"));
    user.IsAdmin.ShouldBeTrue();
    user.PasswordHash.ShouldBe(_hasher.Hash(Password));
    result.Value.ShouldBe(new SignedInUser(user.Id.Value, "eric", user.SecurityStamp.Value, IsAdmin: true));
  }

  [Fact]
  public async Task IsAConflictOnceAUserExists()
  {
    _users.Add(new UserBuilder().Build());

    var result = await _handler.Handle(new SetUpFirstAdminCommand("mallory", Password), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Conflict);
    _users.Count.ShouldBe(1);
  }

  [Fact]
  public async Task IsUnavailableAfterTheSetupWindowCloses()
  {
    _clock.Advance(SetupWindow.Duration);

    var result = await _handler.Handle(new SetUpFirstAdminCommand("eric", Password), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Unavailable);
    _users.ShouldBeEmpty();
  }

  [Fact]
  public async Task RejectsAnInvalidUsername()
  {
    var result = await _handler.Handle(new SetUpFirstAdminCommand("eric maibach", Password), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("username");
    _users.ShouldBeEmpty();
  }

  [Fact]
  public async Task RejectsAPasswordThatBreaksTheRules()
  {
    var result = await _handler.Handle(new SetUpFirstAdminCommand("eric", "short"), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("password");
    _users.ShouldBeEmpty();
  }
}
