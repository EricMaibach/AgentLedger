using AgentLedger.Web.Configurations;

var builder = WebApplication.CreateBuilder(args);

builder.AddLoggerConfigs()      // Serilog replaces the default providers; must run first
       .AddServiceDefaults();   // OpenTelemetry (incl. log export), health checks

using var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
var startupLogger = loggerFactory.CreateLogger<Program>();

startupLogger.LogInformation("Starting web host");

builder.Services.AddServiceConfigs(startupLogger, builder);

builder.Services.AddFastEndpoints()
                .SwaggerDocument(o =>
                {
                  o.DocumentSettings = s =>
                  {
                    s.Title = "AgentLedger API";
                    s.Version = "v1";
                    s.Description = "Ingests and queries activity logged by AI coding agents.";
                  };
                  o.ShortSchemaNames = true;
                });

var app = builder.Build();

await app.UseAppMiddlewareAndMigrateDatabase();

app.MapDefaultEndpoints(); // health checks (Development only)

app.Run();

// Make the implicit Program.cs class public, so integration tests can reference the correct assembly for host building
public sealed partial class Program { }
