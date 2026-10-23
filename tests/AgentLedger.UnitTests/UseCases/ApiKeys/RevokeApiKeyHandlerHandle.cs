using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.ApiKeyAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UnitTests.UseCases.Users;
using AgentLedger.UseCases.ApiKeys.RevokeApiKey;
using Ardalis.Result;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.ApiKeys;

// Owners revoke their own keys; admins can revoke anyone's (ADR 0019).
public sealed class RevokeApiKeyHandlerHandle
{
  private static readonly DateTimeOffset Now = new(2026, 10, 23, 15, 0, 0, TimeSpan.Zero);

  private readonly List<ApiKey> _keys = [];
  private readonly List<User> _users = [];
  private readonly IRepository<ApiKey> _repository;
  private readonly RevokeApiKeyHandler _handler;
  private readonly User _owner = new UserBuilder().WithUsername("owner").WithIsAdmin(false).Build();
  private readonly User _otherMember = new UserBuilder().WithUsername("other").WithIsAdmin(false).Build();
  private readonly User _admin = new UserBuilder().WithUsername("admin").WithIsAdmin(true).Build();
  private readonly ApiKey _key;

  public RevokeApiKeyHandlerHandle()
  {
    _users.AddRange([_owner, _otherMember, _admin]);
    _key = new ApiKeyBuilder().WithOwner(_owner.Id).Build();
    _keys.Add(_key);
    _repository = ApiKeyRepositoryFake.Create(_keys);
    _handler = new RevokeApiKeyHandler(_repository, UserRepositoryFake.CreateReadOnly(_users), new FakeTimeProvider(Now));
  }

  private ValueTask<Result> Revoke(User actor, Guid? keyId = null) =>
    _handler.Handle(new RevokeApiKeyCommand(actor.Id.Value, keyId ?? _key.Id.Value), CancellationToken.None);

  [Fact]
  public async Task LetsTheOwnerRevokeTheirKey()
  {
    (await Revoke(_owner)).IsSuccess.ShouldBeTrue();

    _key.RevokedAt.ShouldBe(Now);
    _key.RevokedBy.ShouldBe(_owner.Id);
    await _repository.Received(1).UpdateAsync(_key, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task LetsAnAdminRevokeAnyonesKey()
  {
    (await Revoke(_admin)).IsSuccess.ShouldBeTrue();

    _key.RevokedBy.ShouldBe(_admin.Id);
  }

  [Fact]
  public async Task HidesOtherPeoplesKeysFromMembers()
  {
    // Not found rather than forbidden: members can't see other people's keys at all.
    (await Revoke(_otherMember)).Status.ShouldBe(ResultStatus.NotFound);

    _key.RevokedAt.ShouldBeNull();
  }

  [Fact]
  public async Task IsNotFoundForAnUnknownKey()
  {
    (await Revoke(_owner, Guid.CreateVersion7())).Status.ShouldBe(ResultStatus.NotFound);
  }
}
