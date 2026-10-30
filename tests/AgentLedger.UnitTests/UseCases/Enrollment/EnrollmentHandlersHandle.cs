using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.EnrollmentAggregate;
using AgentLedger.Core.UserAggregate;
using AgentLedger.UnitTests.Core.UserAggregate;
using AgentLedger.UnitTests.UseCases.Users;
using AgentLedger.UseCases.Enrollment;
using AgentLedger.UseCases.Enrollment.CreateEnrollmentCode;
using AgentLedger.UseCases.Enrollment.Enroll;
using Ardalis.Result;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.Enrollment;

// Connecting a machine (ADR 0024): a signed-in user creates a one-time code; a machine exchanges it for an ingest key
// owned by that user.
public sealed class EnrollmentHandlersHandle
{
  private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

  private readonly FakeTimeProvider _clock = new(Now);
  private readonly List<EnrollmentCode> _codes = [];
  private readonly List<User> _users = [];
  private readonly IEnrollmentStore _store = Substitute.For<IEnrollmentStore>();
  private readonly User _eric = new UserBuilder().WithUsername("eric").Build();

  public EnrollmentHandlersHandle()
  {
    _users.Add(_eric);
    _store.RedeemAsync(Arg.Any<EnrollmentCode>(), Arg.Any<ApiKey>(), Arg.Any<CancellationToken>()).Returns(true);
  }

  private IRepository<EnrollmentCode> Codes =>
    RepositoryFake.Create(_codes, c => c.Id, EnrollmentCodeId.From(Guid.CreateVersion7()));

  private async Task<string> CreateCodeAsync()
  {
    var result = await new CreateEnrollmentCodeHandler(Codes, UserRepositoryFake.CreateReadOnly(_users), _clock)
      .Handle(new CreateEnrollmentCodeCommand(_eric.Id.Value), CancellationToken.None);
    return result.Value.Code;
  }

  private ValueTask<Result<EnrolledMachine>> EnrollAsync(string code, string host = "devbox") =>
    new EnrollHandler(RepositoryFake.CreateReadOnly(_codes, c => c.Id, EnrollmentCodeId.From(Guid.CreateVersion7())), _store, _clock).Handle(new EnrollCommand(code, host), CancellationToken.None);

  [Fact]
  public async Task CreatesACodeThatIsStoredOnlyAsAHash()
  {
    var code = await CreateCodeAsync();

    var stored = _codes.ShouldHaveSingleItem();
    stored.OwnerId.ShouldBe(_eric.Id);
    stored.CodeHash.ShouldBe(EnrollmentCodeHash.Of(code));
    stored.ExpiresAt.ShouldBe(Now.AddMinutes(10));
  }

  [Fact]
  public async Task NeedsAnExistingUserToCreateACode()
  {
    var result = await new CreateEnrollmentCodeHandler(Codes, UserRepositoryFake.CreateReadOnly(_users), _clock)
      .Handle(new CreateEnrollmentCodeCommand(Guid.NewGuid()), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.NotFound);
  }

  [Fact]
  public async Task ExchangesACodeForAnIngestKeyOwnedByItsCreator()
  {
    var code = await CreateCodeAsync();

    var result = await EnrollAsync(code.ToLowerInvariant(), "agentledger-devcontainer");

    result.IsSuccess.ShouldBeTrue();
    ApiKeyToken.TryParse(result.Value.Token, out var token).ShouldBeTrue();
    token.Scope.ShouldBe(ApiKeyScope.Ingest);
    await _store.Received(1).RedeemAsync(
      Arg.Is<EnrollmentCode>(c => c.UsedByHost == "agentledger-devcontainer" && c.ApiKeyId == token.KeyId),
      Arg.Is<ApiKey>(k => k.Id == token.KeyId && k.OwnerId == _eric.Id && k.Scope == ApiKeyScope.Ingest && k.ExpiresAt == null
                          && k.Name == "agentledger-devcontainer (connected 2026-10-30)" && k.Matches(token)),
      Arg.Any<CancellationToken>());
    result.Value.KeyName.ShouldBe("agentledger-devcontainer (connected 2026-10-30)");
  }

  [Fact]
  public async Task RefusesAnUnknownCode()
  {
    (await EnrollAsync("AAAA-AAAA")).Status.ShouldBe(ResultStatus.Invalid);
  }

  [Fact]
  public async Task RefusesAnExpiredCode()
  {
    var code = await CreateCodeAsync();
    _clock.Advance(TimeSpan.FromMinutes(10));

    (await EnrollAsync(code)).Status.ShouldBe(ResultStatus.Invalid);
    await _store.DidNotReceive().RedeemAsync(Arg.Any<EnrollmentCode>(), Arg.Any<ApiKey>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task RefusesACodeAnotherMachineJustUsed()
  {
    // Two machines racing with the same code: the store's conditional update lets only one through.
    var code = await CreateCodeAsync();
    _store.RedeemAsync(Arg.Any<EnrollmentCode>(), Arg.Any<ApiKey>(), Arg.Any<CancellationToken>()).Returns(false);

    (await EnrollAsync(code)).Status.ShouldBe(ResultStatus.Invalid);
  }

  [Fact]
  public async Task GivesTheSameAnswerForEveryUnusableCode()
  {
    // So the answer doesn't reveal whether a guessed code exists.
    var code = await CreateCodeAsync();
    _clock.Advance(TimeSpan.FromMinutes(10));

    var expired = await EnrollAsync(code);
    var unknown = await EnrollAsync("AAAA-AAAA");

    expired.ValidationErrors.Select(e => (e.Identifier, e.ErrorMessage))
      .ShouldBe(unknown.ValidationErrors.Select(e => (e.Identifier, e.ErrorMessage)));
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public async Task NeedsAHostName(string host)
  {
    var code = await CreateCodeAsync();

    var result = await EnrollAsync(code, host);

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.ShouldContain(e => e.Identifier == "host");
  }
}
