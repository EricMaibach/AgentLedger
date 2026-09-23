using AgentLedger.Infrastructure.Data;

namespace AgentLedger.FunctionalTests.Data;

// Smoke test for the skeleton: the app's own DI wiring can reach a Postgres Testcontainer.
public sealed class DatabaseConnectivity(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  [Fact]
  public async Task AppDbContextConnectsToPostgres()
  {
    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    (await db.Database.CanConnectAsync()).ShouldBeTrue();
    db.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
  }
}
