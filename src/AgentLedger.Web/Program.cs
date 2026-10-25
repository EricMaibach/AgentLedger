using AgentLedger.UseCases.Users;
using AgentLedger.Web.Configurations;
using AgentLedger.Web.Users;

var builder = WebApplication.CreateBuilder(args);

builder.AddLoggerConfigs()      // Serilog replaces the default providers; must run first
       .AddServiceDefaults();   // OpenTelemetry (incl. log export), health checks

using var loggerFactory = LoggerFactory.Create(config => config.AddConsole());
var startupLogger = loggerFactory.CreateLogger<Program>();

startupLogger.LogInformation("Starting web host");

builder.Services.AddServiceConfigs(startupLogger, builder);

// The web UI: Blazor components rendered on the server (static SSR; no interactive render modes, ADR 0015).
builder.Services.AddRazorComponents();
builder.Services.AddLedgerAuthentication(builder.Configuration);
builder.Services.AddRequestDecompression(); // gzipped transcript batches (ADR 0020)

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

// `reset-password <username>` runs instead of the web server (ADR 0018).
if (ResetPasswordCommandLine.IsInvokedBy(args))
{
  return await ResetPasswordCommandLine.RunAsync(app.Services, args, Console.Out);
}

await app.UseAppMiddlewareAndMigrateDatabase();

app.MapDefaultEndpoints(); // health checks (Development only)

// First-run setup is open for a few minutes from now (ADR 0018).
app.Services.GetRequiredService<SetupWindow>();

app.Run();
return 0;

// Make the implicit Program.cs class public, so integration tests can reference the correct assembly for host building
public sealed partial class Program { }
