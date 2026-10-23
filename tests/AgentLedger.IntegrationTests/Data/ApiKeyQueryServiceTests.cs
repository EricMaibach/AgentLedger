using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.UserAggregate;
using AgentLedger.UseCases.ApiKeys;

namespace AgentLedger.IntegrationTests.Data;

// API key read models, joined with their owners (ADR 0019). Tests share the database, so each creates its own
// users and checks only their keys.
[Collection(nameof(PostgresCollection))]
public sealed class ApiKeyQueryServiceTests(PostgresFixture db)
{
  private static readonly DateTimeOffset T0 = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task ListsOneOwnersKeysNewestFirstWithNames()
  {
    var alice = await NewUserAsync("alice");
    var bob = await NewUserAsync("bob");
    var older = NewKey(alice, "laptop", ApiKeyScope.Ingest, issuedMinutes: 0);
    var newer = NewKey(alice, "scripts", ApiKeyScope.Read, issuedMinutes: 5, expiresInDays: 30);
    newer.RecordUse(T0.AddHours(1));
    newer.Revoke(bob.Id, T0.AddHours(2)); // say an admin revoked it
    await SaveAsync(older, newer, NewKey(bob, "bob's key", ApiKeyScope.Ingest, issuedMinutes: 10));

    var items = await ServiceAsync(s => s.ListAsync(alice.Id, CancellationToken.None));

    items.ShouldBe([
      new ApiKeyListItem(newer.Id.Value, "scripts", "Read", alice.Username.Value, T0.AddMinutes(5), T0.AddMinutes(5).AddDays(30),
        T0.AddHours(1), T0.AddHours(2), bob.Username.Value),
      new ApiKeyListItem(older.Id.Value, "laptop", "Ingest", alice.Username.Value, T0, null, null, null, null)]);
  }

  [Fact]
  public async Task ListsEveryonesKeysWithoutAnOwner()
  {
    var carol = await NewUserAsync("carol");
    var dave = await NewUserAsync("dave");
    var carols = NewKey(carol, "c", ApiKeyScope.Ingest, issuedMinutes: 0);
    var daves = NewKey(dave, "d", ApiKeyScope.Ingest, issuedMinutes: 1);
    await SaveAsync(carols, daves);

    var items = await ServiceAsync(s => s.ListAsync(null, CancellationToken.None));

    items.Select(i => i.KeyId).ShouldContain(carols.Id.Value);
    items.Select(i => i.KeyId).ShouldContain(daves.Id.Value);
  }

  [Fact]
  public async Task SaysWhoSentWithEachKey()
  {
    var erin = await NewUserAsync("erin");
    var key = NewKey(erin, "dev container", ApiKeyScope.Ingest, issuedMinutes: 0);
    var other = NewKey(erin, "unused", ApiKeyScope.Ingest, issuedMinutes: 1);
    await SaveAsync(key, other);

    var senders = await ServiceAsync(s => s.GetSendersAsync([key.Id], CancellationToken.None));

    senders.ShouldBe(new Dictionary<ApiKeyId, ApiKeySender>
    {
      [key.Id] = new ApiKeySender(key.Id.Value, "dev container", erin.Username.Value),
    });
  }

  private static ApiKey NewKey(User owner, string name, ApiKeyScope scope, int issuedMinutes, int? expiresInDays = null)
  {
    var issued = T0.AddMinutes(issuedMinutes);
    return new ApiKey(ApiKeyId.From(Guid.CreateVersion7()), owner.Id, name, scope, ApiKeySecretHash.Of(name), issued,
      expiresInDays is { } d ? issued.AddDays(d) : null);
  }

  private async Task<User> NewUserAsync(string name)
  {
    var user = new User(UserId.From(Guid.CreateVersion7()), Username.From($"{name}-{Guid.NewGuid():N}"[..30]),
      PasswordHash.From("AQAAAAIAAYagAAAAEhash"), SecurityStamp.From(Guid.CreateVersion7()), isAdmin: false);
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IRepository<User>>().AddAsync(user);
    return user;
  }

  private async Task SaveAsync(params ApiKey[] keys)
  {
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IRepository<ApiKey>>().AddRangeAsync(keys);
  }

  private async Task<T> ServiceAsync<T>(Func<IApiKeyQueryService, Task<T>> query)
  {
    using var scope = db.Services.CreateScope();
    return await query(scope.ServiceProvider.GetRequiredService<IApiKeyQueryService>());
  }
}
