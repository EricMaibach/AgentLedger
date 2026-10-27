using AgentLedger.Infrastructure;
using AgentLedger.Web.Projections;

namespace AgentLedger.Web.Configurations;

public static class ServiceConfigs
{
  public static IServiceCollection AddServiceConfigs(this IServiceCollection services, Microsoft.Extensions.Logging.ILogger logger, WebApplicationBuilder builder)
  {
    services.AddInfrastructureServices(builder.Configuration, logger)
            .AddMediatorSourceGen(logger)
            .AddProjections(builder.Configuration);

    logger.LogInformation("{Project} services registered", "Mediator Source Generator");

    return services;
  }
}
