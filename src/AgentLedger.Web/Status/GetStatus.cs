namespace AgentLedger.Web.Status;

public sealed record StatusResponse(string Service, string Version);

/// <summary>
/// Reports that the API is up. The CLI can call it to check the API is reachable.
/// </summary>
public sealed class GetStatus : EndpointWithoutRequest<StatusResponse>
{
  public override void Configure()
  {
    Get("/status");
    AllowAnonymous();
  }

  public override Task HandleAsync(CancellationToken ct) =>
    Send.OkAsync(new StatusResponse(
      "AgentLedger",
      typeof(GetStatus).Assembly.GetName().Version?.ToString() ?? "unknown"), ct);
}
