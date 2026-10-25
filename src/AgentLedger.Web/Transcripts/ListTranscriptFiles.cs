using AgentLedger.UseCases.Transcripts;
using AgentLedger.UseCases.Transcripts.ListTranscriptFiles;
using AgentLedger.Web.AgentEventReceipts;
using AgentLedger.Web.Extensions;
using AgentLedger.Web.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentLedger.Web.Transcripts;

/// <summary>Query parameters for GET /api/raw/transcripts.</summary>
public sealed class ListTranscriptFilesRequest
{
  /// <summary>The agent's native session ID.</summary>
  public string Session { get; set; } = string.Empty;
}

/// <param name="Files">Main first, then subagents in the order they appeared. <c>agent</c> uses the wire name.</param>
public sealed record ListTranscriptFilesResponse(IReadOnlyList<TranscriptFileItem> Files);

/// <summary>Lists a session's transcript files (ADR 0020).</summary>
public sealed class ListTranscriptFiles(IMediator mediator)
  : Endpoint<ListTranscriptFilesRequest, Results<Ok<ListTranscriptFilesResponse>, ValidationProblem, ProblemHttpResult>>
{
  /// <inheritdoc />
  public override void Configure()
  {
    Get("/api/raw/transcripts");
    Policies(AuthenticationConfig.ReadApiPolicy); // a signed-in browser or a read key (ADR 0019)
    Summary(s =>
    {
      s.Summary = "List a session's transcript files";
      s.Description = "The main transcript and each subagent's, with line counts and the subagent's raw meta.";
    });
  }

  /// <inheritdoc />
  public override async Task<Results<Ok<ListTranscriptFilesResponse>, ValidationProblem, ProblemHttpResult>>
    ExecuteAsync(ListTranscriptFilesRequest request, CancellationToken cancellationToken) =>
    (await mediator.Send(new ListTranscriptFilesQuery(request.Session), cancellationToken))
      .ToOkResult(files => new ListTranscriptFilesResponse(
        [.. files.Select(f => f with { Agent = AgentWireName.FromAgentKindName(f.Agent) })]));
}
