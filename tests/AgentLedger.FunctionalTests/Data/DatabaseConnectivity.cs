using AgentLedger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentLedger.FunctionalTests.Data;

// The app's own DI wiring connects to the test's Postgres container.
public sealed class DatabaseConnectivity(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
  [Fact]
  public async Task AppDbContextUsesTheTestContainer()
  {
    using var scope = factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Must be the throwaway container, never the dev database from the environment.
    db.Database.GetConnectionString().ShouldBe(factory.ConnectionString);
    (await db.Database.CanConnectAsync()).ShouldBeTrue();
  }
}
