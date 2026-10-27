using AgentLedger.Infrastructure.Data;
using AgentLedger.Web.Users;
using Scalar.AspNetCore;

namespace AgentLedger.Web.Configurations;

public static class MiddlewareConfig
{
  public static async Task<IApplicationBuilder> UseAppMiddlewareAndMigrateDatabase(this WebApplication app)
  {
    if (app.Environment.IsDevelopment())
    {
      app.UseDeveloperExceptionPage();
    }
    else
    {
      app.UseDefaultExceptionHandler(); // from FastEndpoints
      app.UseHsts();
    }

    // Login (ADR 0018): limit login attempts, then authenticate and authorize every request (deny by default).
    // Requests the server finds bad while reading the body (e.g. a decompressed body over its limit: 413) carry
    // their own status; the general exception handler above would turn them into 500.
    app.Use(async (context, next) =>
    {
      try
      {
        await next(context);
      }
      catch (BadHttpRequestException ex) when (!context.Response.HasStarted)
      {
        await Results.Problem(statusCode: ex.StatusCode, title: ex.Message).ExecuteAsync(context);
      }
    });

    app.UseRequestDecompression(); // size limits apply to the decompressed body (ADR 0020)
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery(); // required by the Razor components endpoints and their forms

    app.UseFastEndpoints();

    // The web UI (ADR 0015): static assets (open, so the login page is styled), server-rendered pages,
    // sign-out, and / → the raw event list until projection pages exist.
    app.MapStaticAssets().AllowAnonymous();
    app.MapRazorComponents<AgentLedger.Web.Components.App>()
       .AddInteractiveServerRenderMode(); // the work pages (ADR 0023)
    app.MapSignOut();

    // The first transcript page (v0.4.0) was per session; its URL keeps working (docs/ui.md, permanent URLs).
    app.MapGet("/raw/sessions/{session}/transcript", (string session, string? file) =>
        Results.Redirect(AgentLedger.Web.Components.Formatting.RawTranscriptsLink.To(
          new AgentLedger.Web.Components.Formatting.RawTranscriptsQueryString(Session: session, File: file is null or "main" ? null : file))))
      .ExcludeFromDescription();
    app.MapGet("/", () => Results.Redirect(AgentLedger.Web.Components.Formatting.WorkLink.Path)).ExcludeFromDescription();

    if (app.Environment.IsDevelopment())
    {
      app.UseOpenApi(options => options.Path = "/openapi/{documentName}.json");

      app.MapScalarApiReference(options =>
      {
        options.WithTitle("AgentLedger API");
        options.WithOpenApiRoutePattern("/openapi/{documentName}.json");
      });
    }

    app.UseHttpsRedirection(); // Note this will drop Authorization headers

    // Run migrations in Development or when explicitly requested via configuration
    var shouldMigrate = app.Environment.IsDevelopment() ||
                        app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");

    if (shouldMigrate)
    {
      await MigrateDatabaseAsync(app);
    }

    return app;
  }

  static async Task MigrateDatabaseAsync(WebApplication app)
  {
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
      logger.LogInformation("Applying database migrations...");
      var context = services.GetRequiredService<AppDbContext>();
      await context.Database.MigrateAsync();
      logger.LogInformation("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "An error occurred migrating the DB. {exceptionMessage}", ex.Message);
      throw; // Re-throw to make startup fail if migrations fail
    }
  }
}
