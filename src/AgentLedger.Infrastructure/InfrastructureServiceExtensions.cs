using AgentLedger.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentLedger.Infrastructure;

public static class InfrastructureServiceExtensions
{
  public static IServiceCollection AddInfrastructureServices(
    this IServiceCollection services,
    ConfigurationManager config,
    ILogger logger)
  {
    // The dev container sets ConnectionStrings__AgentLedger; tests override it with a Testcontainers instance.
    string? connectionString = config.GetConnectionString("AgentLedger");
    Guard.Against.NullOrEmpty(connectionString);

    services.TryAddSingleton(TimeProvider.System); // tests register a fake clock first
    services.AddScoped<EventDispatchInterceptor>();
    services.AddScoped<AuditTimestampsInterceptor>();
    services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();

    services.AddDbContext<AppDbContext>((provider, options) =>
    {
      options.UseNpgsql(connectionString)
             .UseSnakeCaseNamingConvention();
      options.AddInterceptors(
        provider.GetRequiredService<AuditTimestampsInterceptor>(),
        provider.GetRequiredService<EventDispatchInterceptor>());
    });

    services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>))
           .AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));

    logger.LogInformation("{Project} services registered", "Infrastructure");

    return services;
  }
}
