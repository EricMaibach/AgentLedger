namespace AgentLedger.Web.Status;

public sealed record StatusResponse(string Service, string Version);

/// <summary>
/// Reports that the API is up, and which build is running. The CLI's `status` command calls it.
/// </summary>
public sealed class GetStatus : EndpointWithoutRequest<StatusResponse>
{
  public override void Configure()
  {
    Get("/status");
    AllowAnonymous();
  }

  public override Task HandleAsync(CancellationToken ct) =>
    Send.OkAsync(new StatusResponse("AgentLedger", BuildInfo.Version), ct);
}
