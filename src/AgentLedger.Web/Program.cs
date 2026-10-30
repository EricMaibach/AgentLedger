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

// The web UI: Blazor components rendered on the server, static by default (ADR 0015); the work pages are interactive
// over a server connection (ADR 0023).
builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents()
                // When an interactive page connects, the browser sends back the data it was prerendered with
                // ([PersistentState], ADR 0023). SignalR's default 32 KB limit refused it for long sessions, so the page
                // never became interactive; a few hundred KB is typical. Only signed-in users can connect.
                .AddHubOptions(options => options.MaximumReceiveMessageSize = 4 * 1024 * 1024);
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
