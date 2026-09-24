using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Infrastructure;
using AgentLedger.UseCases.AgentEventReceipts.Ingest;
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
        typeof(AgentEventReceipt),                // Core
        typeof(IngestEventCommand),               // UseCases
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
