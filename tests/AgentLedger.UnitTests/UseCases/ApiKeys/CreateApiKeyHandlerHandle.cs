using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UnitTests.UseCases.Users;
using AgentLedger.UseCases.ApiKeys.CreateApiKey;
using Ardalis.Result;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.ApiKeys;

public sealed class CreateApiKeyHandlerHandle
{
  private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

  private readonly List<ApiKey> _keys = [];
  private readonly List<User> _users = [];
  private readonly CreateApiKeyHandler _handler;
  private readonly User _eric = new UserBuilder().WithUsername("eric").Build();

  public CreateApiKeyHandlerHandle()
  {
    _users.Add(_eric);
    _handler = new CreateApiKeyHandler(ApiKeyRepositoryFake.Create(_keys), UserRepositoryFake.CreateReadOnly(_users), new FakeTimeProvider(Now));
  }

  private ValueTask<Result<CreatedApiKey>> Create(string name = "dev container", string scope = "ingest", int? expiresInDays = null, Guid? owner = null) =>
    _handler.Handle(new CreateApiKeyCommand(owner ?? _eric.Id.Value, name, scope, expiresInDays), CancellationToken.None);

  [Fact]
  public async Task CreatesAKeyForItsOwnerAndReturnsTheTokenOnce()
  {
    var result = await Create();

    result.Status.ShouldBe(ResultStatus.Created);
    var key = _keys.ShouldHaveSingleItem();
    key.OwnerId.ShouldBe(_eric.Id);
    key.Name.ShouldBe("dev container");
    key.Scope.ShouldBe(ApiKeyScope.Ingest);
    key.IssuedAt.ShouldBe(Now);
    key.ExpiresAt.ShouldBeNull();
    result.Value.KeyId.ShouldBe(key.Id.Value);
    ApiKeyToken.TryParse(result.Value.Token, out var token).ShouldBeTrue();
    key.Matches(token).ShouldBeTrue(); // the returned token works; only its hash was stored
  }

  [Fact]
  public async Task GeneratesADifferentSecretEachTime()
  {
    var first = await Create();
    var second = await Create();

    first.Value.Token.ShouldNotBe(second.Value.Token);
  }

  [Theory]
  [InlineData(30)]
  [InlineData(90)]
  [InlineData(365)]
  public async Task ExpiresAfterTheChosenNumberOfDays(int days)
  {
    await Create(expiresInDays: days);

    _keys.ShouldHaveSingleItem().ExpiresAt.ShouldBe(Now.AddDays(days));
  }

  [Theory]
  [InlineData("ingest")]
  [InlineData("Read")]
  public async Task AcceptsEitherScopeInAnyCase(string scope)
  {
    (await Create(scope: scope)).Status.ShouldBe(ResultStatus.Created);
  }

  [Theory]
  [InlineData("admin", null, "dev container", "scope")]
  [InlineData("ingest", 7, "dev container", "expiresInDays")]
  [InlineData("ingest", null, "  ", "name")]
  public async Task RejectsInvalidInput(string scope, int? expiresInDays, string name, string field)
  {
    var result = await Create(name, scope, expiresInDays);

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe(field);
    _keys.ShouldBeEmpty();
  }

  [Fact]
  public async Task IsNotFoundForAnUnknownOwner()
  {
    (await Create(owner: Guid.CreateVersion7())).Status.ShouldBe(ResultStatus.NotFound);
  }
}
