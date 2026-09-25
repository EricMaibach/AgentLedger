using System.Reflection;

namespace AgentLedger.Web.Status;

public sealed record StatusResponse(string Service, string Version);

/// <summary>
/// Reports that the API is up, and which build is running. The CLI's `status` command calls it.
/// </summary>
public sealed class GetStatus : EndpointWithoutRequest<StatusResponse>
{
  // The informational version: the release version plus the commit, e.g. "0.2.0+3f7a9c1…",
  // stamped in by the release workflow (see Dockerfile).
  private static readonly string BuildVersion =
    typeof(GetStatus).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

  public override void Configure()
  {
    Get("/status");
    AllowAnonymous();
  }

  public override Task HandleAsync(CancellationToken ct) =>
    Send.OkAsync(new StatusResponse("AgentLedger", BuildVersion), ct);
}
