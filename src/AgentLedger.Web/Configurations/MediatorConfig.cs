using AgentLedger.Infrastructure;
using Ardalis.SharedKernel;

namespace AgentLedger.Web.Configurations;

public static class MediatorConfig
{
  public static IServiceCollection AddMediatorSourceGen(this IServiceCollection services,
    Microsoft.Extensions.Logging.ILogger logger)
  {
    logger.LogInformation("Registering Mediator SourceGen and Behaviors");
    services.AddMediator(options =>
    {
      options.ServiceLifetime = ServiceLifetime.Scoped;

      // One type from each assembly to scan for handlers.
      options.Assemblies =
      [
        // Add a type from Core and from UseCases here once they contain handlers
        // (e.g. typeof(AgentEventReceipt), typeof(IngestEventCommand)). The generator rejects
        // assemblies that don't use Mediator yet, so they are left out of the empty skeleton.
        typeof(InfrastructureServiceExtensions), // Infrastructure
        typeof(MediatorConfig)                  // Web
      ];

      // Pipeline behaviors run in this order.
      options.PipelineBehaviors =
      [
        typeof(LoggingBehavior<,>)
      ];
    });

    return services;
  }
}
