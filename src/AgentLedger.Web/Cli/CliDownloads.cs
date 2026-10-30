namespace AgentLedger.Web.Cli;

/// <summary>
/// The CLI and its installers, served by the ledger (ADR 0024). Anonymous: a machine being connected has no key yet,
/// and nothing served is secret.
/// </summary>
public static class CliDownloads
{
  public const string NotIncluded = "This ledger has no CLI download for that platform: they come with released AgentLedger images.";

  public static IEndpointRouteBuilder MapCliDownloads(this IEndpointRouteBuilder endpoints)
  {
    endpoints.MapGet("/cli/{file}", (string file, CliBinaries binaries) =>
    {
      var checksum = file.EndsWith(".sha256", StringComparison.Ordinal);
      var platform = checksum ? file[..^".sha256".Length] : file;
      if (!CliBinaries.Platforms.ContainsKey(platform))
        return Results.NotFound();
      if (binaries.Find(platform) is not { } binary)
        return Results.Text(NotIncluded, statusCode: StatusCodes.Status404NotFound);
      return checksum
        ? Results.Text($"{binaries.Checksum(binary)}  {binary.Name}\n")
        : TypedResults.PhysicalFile(binary.FullName, "application/octet-stream", binary.Name);
    }).AllowAnonymous().ExcludeFromDescription();

    endpoints.MapGet("/install.sh", (HttpRequest request) => Script(request, "install.sh")).AllowAnonymous().ExcludeFromDescription();
    endpoints.MapGet("/install.ps1", (HttpRequest request) => Script(request, "install.ps1")).AllowAnonymous().ExcludeFromDescription();
    return endpoints;
  }

  // The script with the address it was downloaded from written in: the machine is set up with exactly the address it
  // reached the ledger by.
  private static Microsoft.AspNetCore.Http.IResult Script(HttpRequest request, string name)
  {
    using var stream = typeof(CliDownloads).Assembly.GetManifestResourceStream($"AgentLedger.Web.Cli.{name}")
      ?? throw new InvalidOperationException($"The {name} resource is missing.");
    using var reader = new StreamReader(stream);
    var script = reader.ReadToEnd().Replace("__AGENTLEDGER_URL__", $"{request.Scheme}://{request.Host}", StringComparison.Ordinal);
    return Results.Text(script, "text/plain; charset=utf-8");
  }
}
