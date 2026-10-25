using AgentLedger.UseCases.ApiKeys;
using AgentLedger.UseCases.Transcripts.GetTranscriptLine;
using AgentLedger.Web.AgentEventReceipts;
using AgentLedger.Web.Extensions;
using AgentLedger.Web.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentLedger.Web.Transcripts;

/// <summary>Route parameters for GET /api/raw/transcripts/{lineId}.</summary>
public sealed class GetRawTranscriptLineRequest
{
  /// <summary>The line's ID.</summary>
  public Guid LineId { get; set; }
}

/// <summary>One transcript line exactly as stored, with who sent it and its file's meta (a subagent's, if any).</summary>
public sealed record RawTranscriptLineResponse(Guid LineId, string Agent, string? LineType, string NativeSessionId, string File,
  string? SubagentId, long ByteOffset, string Text, DateTimeOffset ReceivedAt, ApiKeySender? SentWith, string? FileMeta);

/// <summary>Gets one transcript line (ADR 0020, like GET /api/raw/events/{eventId}).</summary>
public sealed class GetRawTranscriptLine(IMediator mediator)
  : Endpoint<GetRawTranscriptLineRequest, Results<Ok<RawTranscriptLineResponse>, NotFound, ValidationProblem, ProblemHttpResult>>
{
  /// <inheritdoc />
  public override void Configure()
  {
    // :guid: anything else is not this route, so it gets a 404.
    Get("/api/raw/transcripts/{LineId:guid}");
    Policies(AuthenticationConfig.ReadApiPolicy); // a signed-in browser or a read key (ADR 0019)
    Summary(s =>
    {
      s.Summary = "Get one transcript line";
      s.Description = "The exact stored text, who sent it, and the subagent's meta for a subagent's line.";
      s.Responses[200] = "The line";
      s.Responses[404] = "No line with this ID";
    });
  }

  /// <inheritdoc />
  public override async Task<Results<Ok<RawTranscriptLineResponse>, NotFound, ValidationProblem, ProblemHttpResult>>
    ExecuteAsync(GetRawTranscriptLineRequest request, CancellationToken cancellationToken) =>
    (await mediator.Send(new GetTranscriptLineQuery(request.LineId), cancellationToken)).ToGetByIdResult(detail =>
    {
      var line = detail.Line;
      return new RawTranscriptLineResponse(line.LineId, AgentWireName.FromAgentKindName(line.Agent), line.LineType, line.NativeSessionId,
        line.File, line.SubagentId, line.ByteOffset, line.Text, line.ReceivedAt, detail.SentWith, line.FileMeta);
    });
}
