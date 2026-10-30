using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.ApiKeyAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UnitTests.UseCases.Users;
using AgentLedger.UseCases.ApiKeys.AuthenticateApiKey;
using Ardalis.Result;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.ApiKeys;

// Checks a key presented in a request (ADR 0019). Every failure is the same 401: the caller learns nothing
// about which part was wrong.
public sealed class AuthenticateApiKeyHandlerHandle
{
  private readonly List<ApiKey> _keys = [];
  private readonly List<User> _users = [];
  private readonly IRepository<ApiKey> _repository;
  private readonly FakeTimeProvider _clock = new(ApiKeyBuilder.Now.AddDays(1));
  private readonly AuthenticateApiKeyHandler _handler;
  private readonly User _owner = new UserBuilder().WithUsername("eric").Build();
  private readonly ApiKeyBuilder _builder;
  private readonly ApiKey _key;

  public AuthenticateApiKeyHandlerHandle()
  {
    _users.Add(_owner);
    _builder = new ApiKeyBuilder().WithOwner(_owner.Id).WithName("dev container").WithScope(ApiKeyScope.Ingest)
      .WithExpiresAt(ApiKeyBuilder.Now.AddDays(30));
    _key = _builder.Build();
    _keys.Add(_key);
    _repository = ApiKeyRepositoryFake.Create(_keys);
    _handler = new AuthenticateApiKeyHandler(_repository, UserRepositoryFake.CreateReadOnly(_users), _clock);
  }

  private ValueTask<Result<ApiKeyHolder>> Authenticate(string? token) =>
    _handler.Handle(new AuthenticateApiKeyCommand(token), CancellationToken.None);

  [Fact]
  public async Task AcceptsAValidKeyAndSaysWhoHoldsIt()
  {
    var result = await Authenticate(_builder.Token.Value);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(new ApiKeyHolder(_key.Id.Value, "dev container", "Ingest", _owner.Id.Value, "eric"));
  }

  [Fact]
  public async Task RecordsUseAtMostOnceAnHour()
  {
    await Authenticate(_builder.Token.Value);
    await Authenticate(_builder.Token.Value);

    _key.LastUsedAt.ShouldBe(_clock.GetUtcNow());
    await _repository.Received(1).UpdateAsync(_key, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task RecordsTheClientVersion()
  {
    await _handler.Handle(new AuthenticateApiKeyCommand(_builder.Token.Value, "0.6.0"), CancellationToken.None);

    _key.LastClientVersion.ShouldBe("0.6.0");
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("not a key")]
  public async Task RejectsAMissingOrMalformedKey(string? token)
  {
    (await Authenticate(token)).Status.ShouldBe(ResultStatus.Unauthorized);
  }

  [Fact]
  public async Task RejectsAnUnknownKey()
  {
    var unknown = new ApiKeyBuilder().WithId(ApiKeyId.From(Guid.CreateVersion7())).Token;

    (await Authenticate(unknown.Value)).Status.ShouldBe(ResultStatus.Unauthorized);
  }

  [Fact]
  public async Task RejectsAWrongSecret()
  {
    var wrong = _builder.Token with { Secret = new string('x', ApiKeyToken.SecretLength) };

    (await Authenticate(wrong.Value)).Status.ShouldBe(ResultStatus.Unauthorized);
  }

  [Fact]
  public async Task RejectsARevokedKey()
  {
    _key.Revoke(_owner.Id, _clock.GetUtcNow());

    (await Authenticate(_builder.Token.Value)).Status.ShouldBe(ResultStatus.Unauthorized);
  }

  [Fact]
  public async Task RejectsAnExpiredKey()
  {
    _clock.SetUtcNow(ApiKeyBuilder.Now.AddDays(30));

    (await Authenticate(_builder.Token.Value)).Status.ShouldBe(ResultStatus.Unauthorized);
  }

  [Fact]
  public async Task RejectsAKeyWhoseOwnerIsGone()
  {
    _users.Clear();

    (await Authenticate(_builder.Token.Value)).Status.ShouldBe(ResultStatus.Unauthorized);
  }
}
