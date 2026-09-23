using Serilog;

namespace AgentLedger.Web.Configurations;

public static class LoggerConfigs
{
  public static WebApplicationBuilder AddLoggerConfigs(this WebApplicationBuilder builder)
  {
    // Serilog owns console output; sinks come from the "Serilog" configuration section.
    // Clearing first removes the default console provider, which would print every line twice.
    // OpenTelemetry's log provider is added afterwards by AddServiceDefaults.
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(new LoggerConfiguration()
      .ReadFrom.Configuration(builder.Configuration)
      .Enrich.FromLogContext()
      .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
      .CreateLogger());

    return builder;
  }
}
