using System.Security.Cryptography;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.EnrollmentAggregate;
using AgentLedger.Core.EnrollmentAggregate.Specifications;
using AgentLedger.Core.UserAggregate;
using AgentLedger.Infrastructure.Data;
using AgentLedger.IntegrationTests.Data;
using AgentLedger.UseCases.Enrollment;

namespace AgentLedger.IntegrationTests.Enrollment;

// Enrollment codes (ADR 0024) against real Postgres: a code and the key it produced are stored together, once.
[Collection(nameof(PostgresCollection))]
public sealed class EnrollmentStoreTests(PostgresFixture db)
{
  [Fact]
  public async Task RoundTripsACode()
  {
    var code = await SaveNewCodeAsync();

    using var scope = db.Services.CreateScope();
    var loaded = await scope.ServiceProvider.GetRequiredService<IReadRepository<EnrollmentCode>>()
      .FirstOrDefaultAsync(new EnrollmentCodeByHashSpec(code.CodeHash));

    loaded.ShouldNotBeNull();
    (loaded.Id, loaded.OwnerId, loaded.CreatedAt, loaded.ExpiresAt, loaded.UsedAt).ShouldBe(
      (code.Id, code.OwnerId, code.CreatedAt, code.ExpiresAt, (DateTimeOffset?)null));
  }

  [Fact]
  public async Task RedeemsACodeAndStoresItsKey()
  {
    var code = await SaveNewCodeAsync();
    var key = NewKey(code.OwnerId);
    code.Use(db.Clock.GetUtcNow(), "devbox", key.Id);

    (await RedeemAsync(code, key)).ShouldBeTrue();

    using var scope = db.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var stored = await context.Set<EnrollmentCode>().SingleAsync(c => c.Id == code.Id);
    (stored.UsedAt, stored.UsedByHost, stored.ApiKeyId).ShouldBe(((DateTimeOffset?)db.Clock.GetUtcNow(), "devbox", (ApiKeyId?)key.Id));
    (await context.ApiKeys.SingleAsync(k => k.Id == key.Id)).OwnerId.ShouldBe(code.OwnerId);
  }

  [Fact]
  public async Task RedeemsACodeLoadedThroughTheSameContext()
  {
    // As in the app: the handler loads the code with the repository, and the store shares its DbContext.
    var saved = await SaveNewCodeAsync();
    using var scope = db.Services.CreateScope();
    var code = (await scope.ServiceProvider.GetRequiredService<IReadRepository<EnrollmentCode>>()
      .FirstOrDefaultAsync(new EnrollmentCodeByHashSpec(saved.CodeHash)))!;
    var key = NewKey(code.OwnerId);
    code.Use(db.Clock.GetUtcNow(), "devbox", key.Id);

    (await scope.ServiceProvider.GetRequiredService<IEnrollmentStore>().RedeemAsync(code, key, CancellationToken.None)).ShouldBeTrue();
  }

  [Fact]
  public async Task RedeemsACodeOnlyOnce()
  {
    // Two machines racing with the same code: both loaded it unused; only the first redemption counts.
    var code = await SaveNewCodeAsync();
    var (first, second) = (await LoadAsync(code), await LoadAsync(code));
    var (firstKey, secondKey) = (NewKey(code.OwnerId), NewKey(code.OwnerId));
    first.Use(db.Clock.GetUtcNow(), "devbox", firstKey.Id);
    second.Use(db.Clock.GetUtcNow(), "laptop", secondKey.Id);

    (await RedeemAsync(first, firstKey)).ShouldBeTrue();
    (await RedeemAsync(second, secondKey)).ShouldBeFalse();

    using var scope = db.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    (await context.ApiKeys.AnyAsync(k => k.Id == secondKey.Id)).ShouldBeFalse();
    (await context.Set<EnrollmentCode>().SingleAsync(c => c.Id == code.Id)).UsedByHost.ShouldBe("devbox");
  }

  private async Task<EnrollmentCode> SaveNewCodeAsync()
  {
    var owner = (await ApiKeyPersistence.SaveNewKeyAsync(db)).OwnerId;
    var code = new EnrollmentCode(EnrollmentCodeId.From(Guid.CreateVersion7()), owner, EnrollmentCodeHash.Of(EnrollmentCodeText.New().Value),
      db.Clock.GetUtcNow());
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IRepository<EnrollmentCode>>().AddAsync(code);
    return code;
  }

  private async Task<EnrollmentCode> LoadAsync(EnrollmentCode code)
  {
    using var scope = db.Services.CreateScope();
    return (await scope.ServiceProvider.GetRequiredService<IReadRepository<EnrollmentCode>>()
      .FirstOrDefaultAsync(new EnrollmentCodeByHashSpec(code.CodeHash)))!;
  }

  private ApiKey NewKey(UserId owner)
  {
    var id = ApiKeyId.From(Guid.CreateVersion7());
    var token = ApiKeyToken.Create(ApiKeyScope.Ingest, id, RandomNumberGenerator.GetBytes(ApiKeyToken.SecretBytes));
    return new ApiKey(id, owner, "devbox (connected)", ApiKeyScope.Ingest, ApiKeySecretHash.Of(token.Secret), db.Clock.GetUtcNow(), null);
  }

  private async Task<bool> RedeemAsync(EnrollmentCode code, ApiKey key)
  {
    using var scope = db.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<IEnrollmentStore>().RedeemAsync(code, key, CancellationToken.None);
  }
}
