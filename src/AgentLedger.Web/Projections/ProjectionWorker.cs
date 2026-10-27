using System.Diagnostics;
using AgentLedger.UseCases.Projections;
using AgentLedger.UseCases.Projections.ClaudeCode;
using AgentLedger.UseCases.Projections.RunProjections;
using Microsoft.Extensions.Options;

namespace AgentLedger.Web.Projections;

// Settings under "Projections" (ADR 0022). Lag: how long a raw row may take to commit after its updated_at was taken
// (docs/architecture.md, incremental processing). Interval: the pause between passes when there's nothing to rebuild.
// RetryDelay: the pause after a failed pass, or while another instance holds the lock.
public sealed class ProjectionOptions
{
  public bool Enabled { get; set; } = true;
  public TimeSpan Lag { get; set; } = TimeSpan.FromSeconds(5);
  public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(5);
  public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);
}

public static class ProjectionsConfig
{
  public static IServiceCollection AddProjections(this IServiceCollection services, IConfiguration configuration)
  {
    // One adapter per agent (ADR 0003); agents without one are left unprojected.
    services.AddSingleton<IAgentAdapter, ClaudeCodeAdapter>();
    services.Configure<ProjectionOptions>(configuration.GetSection("Projections"));
    if (configuration.GetValue("Projections:Enabled", true))
      services.AddHostedService<ProjectionWorker>();
    return services;
  }
}

// Keeps projections up to date in the background (ADR 0022): while it holds the projection lock, it runs a pass every
// few seconds, back to back while a rebuild is under way. Polling rather than a signal from ingest: the commit lag means
// new rows can't be projected sooner anyway, and ingest stays unaware of projections. Errors are logged and retried.
public sealed class ProjectionWorker(IServiceScopeFactory scopes, IProjectionLock projectionLock,
  IOptions<ProjectionOptions> options, ILogger<ProjectionWorker> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var settings = options.Value;
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        await using var held = await projectionLock.TryAcquireAsync(stoppingToken);
        if (held is null)
        {
          logger.LogDebug("Another instance runs the projections");
          await Task.Delay(settings.RetryDelay, stoppingToken);
          continue;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
          var summary = await RunPassAsync(settings, stoppingToken);
          if (summary.Rebuilt < RunProjectionsHandler.RebuildBatchSize)
            await Task.Delay(settings.Interval, stoppingToken);
        }
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
      {
        return;
      }
      catch (Exception ex)
      {
        logger.LogError(ex, "Projection pass failed; retrying in {Delay}", settings.RetryDelay);
        await Task.Delay(settings.RetryDelay, stoppingToken);
      }
    }
  }

  private async Task<ProjectionRunSummary> RunPassAsync(ProjectionOptions settings, CancellationToken stoppingToken)
  {
    using var scope = scopes.CreateScope();
    var started = Stopwatch.GetTimestamp();
    var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RunProjectionsCommand(settings.Lag), stoppingToken);
    var summary = result.Value;
    // With the time taken: a session is rebuilt whole on every change (ADR 0022), so long sessions cost the most.
    if (summary.Projected + summary.Failed + summary.Rebuilt > 0)
      logger.LogInformation("Projected {Projected} sessions, rebuilt {Rebuilt}, failed {Failed} in {Elapsed:F0} ms", summary.Projected,
        summary.Rebuilt, summary.Failed, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    return summary;
  }
}
