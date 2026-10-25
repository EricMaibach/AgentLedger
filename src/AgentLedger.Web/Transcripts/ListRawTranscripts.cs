using AgentLedger.UseCases;
using AgentLedger.UseCases.Transcripts;
using AgentLedger.UseCases.Transcripts.ListTranscriptLines;
using AgentLedger.Web.AgentEventReceipts;
using AgentLedger.Web.Extensions;
using AgentLedger.Web.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentLedger.Web.Transcripts;

/// <summary>Query parameters for GET /api/raw/transcripts. Every filter is optional.</summary>
public sealed class ListRawTranscriptsRequest
{
  /// <summary>Kebab-case agent name, e.g. <c>claude-code</c>.</summary>
  public string? Agent { get; set; }

  /// <summary>The line's own top-level <c>type</c> field, e.g. <c>assistant</c>.</summary>
  public string? LineType { get; set; }

  /// <summary>The agent's native session ID.</summary>
  public string? Session { get; set; }

  /// <summary><c>main</c>, or <c>subagents/agent-&lt;id&gt;</c>.</summary>
  public string? File { get; set; }

  /// <summary>Received at or after (inclusive).</summary>
  public DateTimeOffset? From { get; set; }

  /// <summary>Received before (exclusive).</summary>
  public DateTimeOffset? To { get; set; }

  /// <summary>Lines per page.</summary>
  public int Limit { get; set; } = Constants.DEFAULT_PAGE_SIZE;

  /// <summary>The <c>nextCursor</c> from the previous page; omit for the first page.</summary>
  public string? After { get; set; }
}

/// <param name="Items">The lines, newest first. <c>agent</c> uses the wire name.</param>
/// <param name="NextCursor">Pass as <c>after</c> to get the next (older) page; null on the last page.</param>
public sealed record ListRawTranscriptsResponse(IReadOnlyList<TranscriptLineListItem> Items, string? NextCursor);

/// <summary>Lists transcript lines, newest first, with filters and keyset paging (ADR 0020, like raw events).</summary>
public sealed class ListRawTranscripts(IMediator mediator)
  : Endpoint<ListRawTranscriptsRequest, Results<Ok<ListRawTranscriptsResponse>, ValidationProblem, ProblemHttpResult>>
{
  /// <inheritdoc />
  public override void Configure()
  {
    Get("/api/raw/transcripts");
    Policies(AuthenticationConfig.ReadApiPolicy); // a signed-in browser or a read key (ADR 0019)
    Summary(s =>
    {
      s.Summary = "List transcript lines, newest first";
      s.Description = "Each line with a short preview; within a batch, lines are in file order. Page with the returned nextCursor.";
      s.Responses[200] = "A page of lines";
      s.Responses[400] = "Invalid parameters; errors are keyed by parameter";
    });
  }

  /// <inheritdoc />
  public override async Task<Results<Ok<ListRawTranscriptsResponse>, ValidationProblem, ProblemHttpResult>>
    ExecuteAsync(ListRawTranscriptsRequest request, CancellationToken cancellationToken)
  {
    var filter = RawTranscriptFilterParameters.ToFilter(request.Agent, request.LineType, request.Session, request.File, request.From, request.To);
    if (!filter.IsSuccess)
    {
      return Result<TranscriptLinePage>.Invalid(filter.ValidationErrors).ToOkResult(ToResponse);
    }

    TranscriptLineCursor? after = null;
    if (request.After is not null && !TranscriptCursorCodec.TryDecode(request.After, out after))
    {
      return Result<TranscriptLinePage>.Invalid(new ValidationError("after", "Not a cursor returned by this API.")).ToOkResult(ToResponse);
    }

    var result = await mediator.Send(new ListTranscriptLinesQuery(filter.Value, after, request.Limit), cancellationToken);
    return result.ToOkResult(ToResponse);
  }

  private static ListRawTranscriptsResponse ToResponse(TranscriptLinePage page) => new(
    [.. page.Items.Select(item => item with { Agent = AgentWireName.FromAgentKindName(item.Agent) })],
    page.Next is null ? null : TranscriptCursorCodec.Encode(page.Next));
}
