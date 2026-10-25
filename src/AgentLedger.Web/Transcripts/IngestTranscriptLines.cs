using System.Security.Claims;
using AgentLedger.UseCases.Transcripts.IngestTranscriptBatch;
using AgentLedger.Web.AgentEventReceipts;
using AgentLedger.Web.Extensions;
using AgentLedger.Web.Users;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AgentLedger.Web.Transcripts;

/// <summary>One chunk of one transcript file, as the CLI sends it (ADR 0020).</summary>
public sealed class IngestTranscriptLinesRequest
{
  /// <summary>Kebab-case agent name, e.g. <c>claude-code</c>.</summary>
  public string Agent { get; set; } = string.Empty;

  /// <summary>The agent's native session ID.</summary>
  public string NativeSessionId { get; set; } = string.Empty;

  /// <summary><c>main</c>, or <c>subagents/agent-&lt;id&gt;</c>.</summary>
  public string File { get; set; } = string.Empty;

  /// <summary>A subagent's <c>.meta.json</c>, exactly as read; omit for main.</summary>
  public string? SubagentMeta { get; set; }

  /// <summary>The lines, each with its byte offset in the file and its exact text.</summary>
  public List<IngestTranscriptLine> Lines { get; set; } = [];
}

/// <summary>A line of a batch.</summary>
public sealed record IngestTranscriptLine(long ByteOffset, string Text);

/// <summary>How many of the batch's lines were new, and how many the ledger already had.</summary>
public sealed record IngestTranscriptLinesResponse(int Stored, int AlreadyStored);

/// <summary>POST /transcripts/lines: an ingest key (ADR 0019); gzip allowed (Content-Encoding: gzip).</summary>
public sealed class IngestTranscriptLines(IMediator mediator)
  : Endpoint<IngestTranscriptLinesRequest, Results<Ok<IngestTranscriptLinesResponse>, ValidationProblem, ProblemHttpResult>>
{
  /// <summary>
  /// The largest body accepted, after decompression: the CLI sends at most about 4 MB of line text per batch,
  /// and JSON escaping adds to that. Larger bodies are refused (413) rather than expanded into memory.
  /// </summary>
  public const long MaxBodyBytes = 8 * 1024 * 1024;

  /// <inheritdoc />
  public override void Configure()
  {
    Post("/transcripts/lines");
    Policies(AuthenticationConfig.IngestPolicy); // always an ingest key (ADR 0019)
    Options(builder => builder.WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes)));
    Summary(s =>
    {
      s.Summary = "Store a batch of transcript lines";
      s.Description = "One file's chunk; each line is stored once. Send gzipped (Content-Encoding: gzip). Up to 1,000 lines.";
      s.Responses[200] = "Counts of new and already stored lines";
      s.Responses[400] = "Invalid batch; errors are keyed by field";
      s.Responses[413] = "Body larger than 8 MB once decompressed";
    });
  }

  /// <inheritdoc />
  public override async Task<Results<Ok<IngestTranscriptLinesResponse>, ValidationProblem, ProblemHttpResult>>
    ExecuteAsync(IngestTranscriptLinesRequest request, CancellationToken cancellationToken)
  {
    if (!AgentWireName.TryToAgentKindName(request.Agent, out var agentKindName))
    {
      return Result<TranscriptBatchStored>
        .Invalid(new ValidationError("agent", $"Agent must be a kebab-case name such as 'claude-code', but was '{request.Agent}'."))
        .ToOkResult(ToResponse);
    }

    var result = await mediator.Send(new IngestTranscriptBatchCommand(
      agentKindName,
      request.NativeSessionId,
      request.File,
      request.SubagentMeta,
      [.. request.Lines.Select(l => new TranscriptLineInput(l.ByteOffset, l.Text))],
      Guid.Parse(User.FindFirstValue(ApiKeyAuthenticationHandler.KeyIdClaim)!)), cancellationToken); // the policy guarantees a key

    return result.ToOkResult(ToResponse);
  }

  private static IngestTranscriptLinesResponse ToResponse(TranscriptBatchStored stored) => new(stored.Stored, stored.AlreadyStored);
}
