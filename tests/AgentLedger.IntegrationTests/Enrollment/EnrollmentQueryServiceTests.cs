using System.Security.Cryptography;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.EnrollmentAggregate;
using AgentLedger.Core.UserAggregate;
using AgentLedger.IntegrationTests.Data;
using AgentLedger.UseCases.Enrollment;

namespace AgentLedger.IntegrationTests.Enrollment;

// The machines page's list (ADR 0024): keys created by enrollment, with where and when they were connected.
[Collection(nameof(PostgresCollection))]
public sealed class EnrollmentQueryServiceTests(PostgresFixture db)
{
  [Fact]
  public async Task ListsConnectedMachinesWithTheirKeys()
  {
    var (owner, key) = await ConnectAsync("devbox");
    using (var scope = db.Services.CreateScope())
    {
      var keys = scope.ServiceProvider.GetRequiredService<IRepository<ApiKey>>();
      var stored = (await keys.GetByIdAsync(key.Id))!;
      stored.RecordUse(db.Clock.GetUtcNow().AddMinutes(1), "0.6.0");
      await keys.UpdateAsync(stored);
    }

    var machines = await ListAsync(owner);

    var machine = machines.ShouldHaveSingleItem();
    (machine.KeyId, machine.Host, machine.KeyName, machine.ConnectedAt, machine.LastUsedAt, machine.ClientVersion, machine.RevokedAt)
      .ShouldBe((key.Id.Value, "devbox", key.Name, db.Clock.GetUtcNow(), (DateTimeOffset?)db.Clock.GetUtcNow().AddMinutes(1), "0.6.0",
        (DateTimeOffset?)null));
    machine.Owner.ShouldStartWith("user-");
  }

  [Fact]
  public async Task ListsOnlyTheOwnersMachinesUnlessAskedForAll()
  {
    var (owner, _) = await ConnectAsync("mine");
    var (other, _) = await ConnectAsync("theirs");

    (await ListAsync(owner)).Select(m => m.Host).ShouldBe(["mine"]);
    var all = await ListAsync(null);
    all.Select(m => m.Host).ShouldContain("mine");
    all.Select(m => m.Host).ShouldContain("theirs");
  }

  [Fact]
  public async Task LeavesOutUnusedCodesAndKeysMadeByHand()
  {
    var hand = await ApiKeyPersistence.SaveNewKeyAsync(db);
    using (var scope = db.Services.CreateScope())
    {
      await scope.ServiceProvider.GetRequiredService<IRepository<EnrollmentCode>>().AddAsync(
        new EnrollmentCode(EnrollmentCodeId.From(Guid.CreateVersion7()), hand.OwnerId, EnrollmentCodeHash.Of(EnrollmentCodeText.New().Value),
          db.Clock.GetUtcNow()));
    }

    (await ListAsync(hand.OwnerId)).ShouldBeEmpty();
  }

  private async Task<(UserId Owner, ApiKey Key)> ConnectAsync(string host)
  {
    var owner = (await ApiKeyPersistence.SaveNewKeyAsync(db)).OwnerId;
    var code = new EnrollmentCode(EnrollmentCodeId.From(Guid.CreateVersion7()), owner, EnrollmentCodeHash.Of(EnrollmentCodeText.New().Value),
      db.Clock.GetUtcNow());
    using var scope = db.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<IRepository<EnrollmentCode>>().AddAsync(code);
    var id = ApiKeyId.From(Guid.CreateVersion7());
    var token = ApiKeyToken.Create(ApiKeyScope.Ingest, id, RandomNumberGenerator.GetBytes(ApiKeyToken.SecretBytes));
    var key = new ApiKey(id, owner, $"{host} (connected)", ApiKeyScope.Ingest, ApiKeySecretHash.Of(token.Secret), db.Clock.GetUtcNow(), null);
    code.Use(db.Clock.GetUtcNow(), host, id);
    (await scope.ServiceProvider.GetRequiredService<IEnrollmentStore>().RedeemAsync(code, key, CancellationToken.None)).ShouldBeTrue();
    return (owner, key);
  }

  private async Task<IReadOnlyList<MachineListItem>> ListAsync(UserId? owner)
  {
    using var scope = db.Services.CreateScope();
    return await scope.ServiceProvider.GetRequiredService<IEnrollmentQueryService>().ListMachinesAsync(owner, CancellationToken.None);
  }
}
