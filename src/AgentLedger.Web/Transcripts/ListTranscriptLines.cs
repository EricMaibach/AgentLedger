using AgentLedger.UseCases;
using AgentLedger.UseCases.Transcripts;
using AgentLedger.UseCases.Transcripts.ListTranscriptLines;
using AgentLedger.Web.Extensions;
using AgentLedger.Web.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentLedger.Web.Transcripts;

/// <summary>Query parameters for GET /api/raw/transcripts/lines.</summary>
public sealed class ListTranscriptLinesRequest
{
  /// <summary>The agent's native session ID.</summary>
  public string Session { get; set; } = string.Empty;

  /// <summary><c>main</c> (the default), or <c>subagents/agent-&lt;id&gt;</c>.</summary>
  public string File { get; set; } = "main";

  /// <summary>Lines per page.</summary>
  public int Limit { get; set; } = Constants.DEFAULT_PAGE_SIZE;

  /// <summary>The <c>nextCursor</c> from the previous page; omit for the first page.</summary>
  public string? After { get; set; }
}

/// <param name="Items">The lines in file order, each with its byte offset and exact text.</param>
/// <param name="NextCursor">Pass as <c>after</c> to get the next page; null on the last page.</param>
public sealed record ListTranscriptLinesResponse(IReadOnlyList<TranscriptLineItem> Items, string? NextCursor);

/// <summary>Pages through one transcript file's lines in order (ADR 0020, keyset paging per ADR 0016).</summary>
public sealed class ListTranscriptLines(IMediator mediator)
  : Endpoint<ListTranscriptLinesRequest, Results<Ok<ListTranscriptLinesResponse>, ValidationProblem, ProblemHttpResult>>
{
  /// <inheritdoc />
  public override void Configure()
  {
    Get("/api/raw/transcripts/lines");
    Policies(AuthenticationConfig.ReadApiPolicy); // a signed-in browser or a read key (ADR 0019)
    Summary(s =>
    {
      s.Summary = "Page through a transcript file's lines";
      s.Description = "In file order, exactly as stored. Page with the returned nextCursor.";
    });
  }

  /// <inheritdoc />
  public override async Task<Results<Ok<ListTranscriptLinesResponse>, ValidationProblem, ProblemHttpResult>>
    ExecuteAsync(ListTranscriptLinesRequest request, CancellationToken cancellationToken)
  {
    TranscriptLineCursor? after = null;
    if (request.After is not null && !TranscriptCursorCodec.TryDecode(request.After, out after))
    {
      return Result<TranscriptLinePage>.Invalid(new ValidationError("after", "Not a cursor returned by this API.")).ToOkResult(ToResponse);
    }

    var result = await mediator.Send(new ListTranscriptLinesQuery(request.Session, request.File, after, request.Limit), cancellationToken);
    return result.ToOkResult(ToResponse);
  }

  private static ListTranscriptLinesResponse ToResponse(TranscriptLinePage page) =>
    new(page.Items, page.Next is null ? null : TranscriptCursorCodec.Encode(page.Next));
}
