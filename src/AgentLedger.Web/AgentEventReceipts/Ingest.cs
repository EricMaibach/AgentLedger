using System.Security.Claims;
using System.Text.Json;
using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.UseCases.AgentEventReceipts.Ingest;
using AgentLedger.Web.Extensions;
using AgentLedger.Web.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentLedger.Web.AgentEventReceipts;

/// <summary>
/// Records one hook invocation sent by the agentledger CLI. Every accepted message becomes a new
/// receipt, resends included; deduplication happens downstream (ADR 0010).
/// </summary>
public sealed class Ingest(IMediator mediator)
  : Endpoint<IngestEventRequest, Results<Created<IngestEventResponse>, ValidationProblem, ProblemHttpResult>>
{
  public override void Configure()
  {
    Post(IngestEventRequest.Route);
    Policies(AuthenticationConfig.IngestPolicy); // always an ingest key (ADR 0019)
    Summary(s =>
    {
      s.Summary = "Record a captured agent event";
      s.Responses[201] = "Receipt stored";
      s.Responses[400] = "Invalid envelope; errors are keyed by request field";
      s.Responses[500] = "Server error; the client should keep the event and retry";
    });
  }

  public override async Task<Results<Created<IngestEventResponse>, ValidationProblem, ProblemHttpResult>>
    ExecuteAsync(IngestEventRequest request, CancellationToken cancellationToken)
  {
    if (!AgentWireName.TryToAgentKindName(request.Agent, out var agentKindName))
    {
      return Result<ReceiptId>
        .Invalid(new ValidationError("agent", $"Agent must be a kebab-case name such as 'claude-code', but was '{request.Agent}'."))
        .ToCreatedResult(ToResponse);
    }

    var command = new IngestEventCommand(
      request.EventId,
      agentKindName,
      request.EventType,
      request.NativeSessionId,
      request.CapturedAt,
      // Host, User and ProjectDir may be missing from the JSON; CaptureContext's guards reject null with the field name.
      request.Host!,
      request.User!,
      request.ProjectDir!,
      request.GitRepo,
      request.GitBranch,
      request.GitWorktree,
      request.Tags ?? [],
      RawPayload(request.Payload),
      SendingKeyId());

    var result = await mediator.Send(command, cancellationToken);

    return result.ToCreatedResult(ToResponse);
  }

  private static IngestEventResponse ToResponse(ReceiptId id) => new(id.Value);

  // The authenticated key this event arrived with; the ingest policy guarantees there is one.
  private Guid SendingKeyId() => Guid.Parse(User.FindFirstValue(ApiKeyAuthenticationHandler.KeyIdClaim)!);

  // The payload's exact original text. A missing or null payload becomes empty, which the domain rejects.
  private static string RawPayload(JsonElement payload) =>
    payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? string.Empty : payload.GetRawText();
}
