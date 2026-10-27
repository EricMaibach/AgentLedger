using AgentLedger.UseCases.Projections;

namespace AgentLedger.IntegrationTests.Projections;

// One projection worker at a time, even with several API instances (ADR 0022): a Postgres advisory lock.
[Collection(nameof(PostgresCollection))]
public sealed class ProjectionLockTests(PostgresFixture db)
{
  [Fact]
  public async Task IsHeldByOneHolderAtATime()
  {
    var projectionLock = db.Services.GetRequiredService<IProjectionLock>();

    var first = await projectionLock.TryAcquireAsync(CancellationToken.None);
    var second = await projectionLock.TryAcquireAsync(CancellationToken.None);

    first.ShouldNotBeNull();
    second.ShouldBeNull();

    await first.DisposeAsync();
    var third = await projectionLock.TryAcquireAsync(CancellationToken.None);
    third.ShouldNotBeNull();
    await third.DisposeAsync();
  }
}
