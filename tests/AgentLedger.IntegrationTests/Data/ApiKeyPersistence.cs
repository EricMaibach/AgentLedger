using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;
using AgentLedger.Infrastructure.Data;

namespace AgentLedger.IntegrationTests.Data;

[Collection(nameof(PostgresCollection))]
public sealed class ApiKeyPersistence(PostgresFixture db)
{
  private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task RoundTripsEveryProperty()
  {
    var owner = await SaveNewUserAsync(db);
    var original = new ApiKey(ApiKeyId.From(Guid.CreateVersion7()), owner.Id, "dev container", ApiKeyScope.Read,
      ApiKeySecretHash.Of("secret"), Now, Now.AddDays(30));
    original.RecordUse(Now.AddHours(1));
    original.Revoke(owner.Id, Now.AddHours(2));

    await SaveAsync(db, original);
    using var scope = db.Services.CreateScope();
    var loaded = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<ApiKey>().AsNoTracking()
      .SingleAsync(k => k.Id == original.Id);

    loaded.OwnerId.ShouldBe(owner.Id);
    loaded.Name.ShouldBe("dev container");
    loaded.Scope.ShouldBe(ApiKeyScope.Read);
    loaded.SecretHash.ShouldBe(original.SecretHash);
    loaded.IssuedAt.ShouldBe(Now);
    loaded.ExpiresAt.ShouldBe(Now.AddDays(30));
    loaded.LastUsedAt.ShouldBe(Now.AddHours(1));
    loaded.RevokedAt.ShouldBe(Now.AddHours(2));
    loaded.RevokedBy.ShouldBe(owner.Id);
  }

  [Fact]
  public async Task RejectsAnOwnerThatDoesNotExist()
  {
    var key = new ApiKey(ApiKeyId.From(Guid.CreateVersion7()), UserId.From(Guid.CreateVersion7()), "orphan",
      ApiKeyScope.Ingest, ApiKeySecretHash.Of("secret"), Now, null);

    await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(db, key));
  }

  // For other tests that need a stored key (receipts reference one).
  public static async Task<ApiKey> SaveNewKeyAsync(PostgresFixture db)
  {
    var owner = await SaveNewUserAsync(db);
    var key = new ApiKey(ApiKeyId.From(Guid.CreateVersion7()), owner.Id, "test key", ApiKeyScope.Ingest,
      ApiKeySecretHash.Of("secret"), Now, null);
    await SaveAsync(db, key);
    return key;
  }

  private static async Task<User> SaveNewUserAsync(PostgresFixture db)
  {
    var user = new User(UserId.From(Guid.CreateVersion7()), Username.From($"user-{Guid.NewGuid():N}"[..30]),
      PasswordHash.From("AQAAAAIAAYagAAAAEhash"), SecurityStamp.From(Guid.CreateVersion7()), isAdmin: false);
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IRepository<User>>().AddAsync(user);
    return user;
  }

  private static async Task SaveAsync(PostgresFixture db, ApiKey key)
  {
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IRepository<ApiKey>>().AddAsync(key);
  }
}
