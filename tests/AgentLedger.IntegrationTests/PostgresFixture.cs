using AgentLedger.Infrastructure;
using AgentLedger.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace AgentLedger.IntegrationTests;

// One Postgres container per test run, shared by every test class in the collection.
// Services are built with the app's own AddInfrastructureServices, so tests exercise the real
// wiring (naming convention, interceptors, provider) rather than a hand-built DbContext.
public sealed class PostgresFixture : IAsyncLifetime
{
  private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

  public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));

  public IServiceProvider Services { get; private set; } = default!;

  public async Task InitializeAsync()
  {
    await _container.StartAsync();

    var config = new ConfigurationManager();
    config["ConnectionStrings:AgentLedger"] = _container.GetConnectionString();

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<TimeProvider>(Clock);
    services.AddInfrastructureServices(config, NullLogger.Instance);
    // Domain event dispatch needs Mediator, which is wired in Web; it isn't under test here.
    services.AddScoped(_ => Substitute.For<IDomainEventDispatcher>());
    Services = services.BuildServiceProvider();

    using var scope = Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
  }

  public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
