using AgentLedger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AgentLedger.FunctionalTests;

public sealed class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>, IAsyncLifetime where TProgram : class
{
  // Match the dev container's Postgres major version so tests exercise the same engine.
  private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:18")
    .Build();

  public Task InitializeAsync() => _dbContainer.StartAsync();

  public new async Task DisposeAsync()
  {
    await _dbContainer.DisposeAsync();
    await base.DisposeAsync();
  }

  /// <summary>
  /// Overriding CreateHost to avoid creating a separate ServiceProvider per this thread:
  /// https://github.com/dotnet-architecture/eShopOnWeb/issues/465
  /// </summary>
  /// <param name="builder"></param>
  /// <returns></returns>
  protected override IHost CreateHost(IHostBuilder builder)
  {
    builder.UseEnvironment("Testing");
    var host = builder.Build();
    host.Start();

    // Build the schema in the fresh container.
    using var scope = host.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

    return host;
  }

  public string ConnectionString => _dbContainer.GetConnectionString();

  protected override void ConfigureWebHost(IWebHostBuilder builder) =>
    // UseSetting, not ConfigureAppConfiguration: Program reads the connection string while registering
    // services, before ConfigureAppConfiguration overrides apply. Environment variables (the dev
    // container sets ConnectionStrings__AgentLedger) would otherwise win, and tests would hit the dev database.
    builder.UseSetting("ConnectionStrings:AgentLedger", ConnectionString);
}
