using AgentLedger.Infrastructure.Data;

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

    services.AddScoped<EventDispatchInterceptor>();
    services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();

    services.AddDbContext<AppDbContext>((provider, options) =>
    {
      options.UseNpgsql(connectionString);
      options.AddInterceptors(provider.GetRequiredService<EventDispatchInterceptor>());
    });

    services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>))
           .AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));

    logger.LogInformation("{Project} services registered", "Infrastructure");

    return services;
  }
}
