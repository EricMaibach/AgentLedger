using AgentLedger.Infrastructure.Data;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
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

  /// <summary>A controllable clock for this app instance, when <see cref="UseFakeClock"/> was called.</summary>
  public FakeTimeProvider? Clock { get; private set; }

  private readonly Dictionary<string, string> _settings = new()
  {
    // Re-check sign-in cookies on every request, so tests see a password change end other sessions at once.
    ["Authentication:SessionCheckInterval"] = "00:00:00",
    // Tests sign in far more often than people do; LoginRateLimitTests sets the real limit.
    ["Authentication:LoginAttemptsPerMinute"] = "10000",
  };

  /// <summary>Overrides a setting for this app instance. Call before the first request: the app starts on first use.</summary>
  public void UseSetting(string key, string value) => _settings[key] = value;

  /// <summary>Gives the app a fake clock. Call before the first request: the app starts on first use.</summary>
  public void UseFakeClock() => Clock ??= new FakeTimeProvider(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    // UseSetting, not ConfigureAppConfiguration: Program reads the connection string while registering
    // services, before ConfigureAppConfiguration overrides apply. Environment variables (the dev
    // container sets ConnectionStrings__AgentLedger) would otherwise win, and tests would hit the dev database.
    builder.UseSetting("ConnectionStrings:AgentLedger", ConnectionString);

    foreach (var (key, value) in _settings)
    {
      builder.UseSetting(key, value);
    }

    if (Clock is not null)
    {
      builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
    }
  }
}
