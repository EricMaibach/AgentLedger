using System.Security.Claims;
using AgentLedger.UseCases.Git.IngestGitObservations;
using AgentLedger.Web.Extensions;
using AgentLedger.Web.Users;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AgentLedger.Web.Git;

/// <summary>A repository's newly observed commits, as the CLI found them with <c>git log</c> (ADR 0021).</summary>
public sealed class IngestGitObservationsRequest
{
  /// <summary>The repository: its normalized remote (e.g. <c>github.com/owner/repo</c>), or host and folder without one.</summary>
  public string Repository { get; set; } = string.Empty;

  /// <summary>Up to 200 commits, oldest first.</summary>
  public List<IngestGitCommit> Commits { get; set; } = [];
}

/// <summary>One commit: full hashes in lowercase hex, the message exactly as git reports it, up to 1,000 changed files.</summary>
public sealed record IngestGitCommit(string Hash, List<string>? Parents, DateTimeOffset AuthoredAt, DateTimeOffset CommittedAt,
  string Message, List<IngestGitFile>? Files, bool FilesTruncated);

/// <summary>A changed file: <c>added</c>, <c>modified</c>, <c>deleted</c>, <c>renamed</c>, <c>copied</c> or <c>typeChanged</c>.</summary>
public sealed record IngestGitFile(string Path, string Change, string? PreviousPath);

/// <summary>How many commits were new, and how many the ledger already had.</summary>
public sealed record IngestGitObservationsResponse(int CommitsStored, int CommitsAlreadyStored);

/// <summary>POST /git/observations: an ingest key (ADR 0019); gzip allowed.</summary>
public sealed class IngestGitObservations(IMediator mediator)
  : Endpoint<IngestGitObservationsRequest, Results<Ok<IngestGitObservationsResponse>, ValidationProblem, ProblemHttpResult>>
{
  /// <summary>The largest body accepted, after decompression.</summary>
  public const long MaxBodyBytes = 8 * 1024 * 1024;

  /// <inheritdoc />
  public override void Configure()
  {
    Post("/git/observations");
    Policies(AuthenticationConfig.IngestPolicy); // always an ingest key (ADR 0019)
    Options(builder => builder.WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes)));
    Summary(s =>
    {
      s.Summary = "Store newly observed git commits";
      s.Description = "One repository's commits since the CLI last looked, each stored once. Up to 200 per batch.";
      s.Responses[200] = "Counts of new and already stored commits";
      s.Responses[400] = "Invalid batch; errors are keyed by field";
    });
  }

  /// <inheritdoc />
  public override async Task<Results<Ok<IngestGitObservationsResponse>, ValidationProblem, ProblemHttpResult>>
    ExecuteAsync(IngestGitObservationsRequest request, CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new IngestGitObservationsCommand(
      request.Repository,
      [.. request.Commits.Select(c => new GitCommitInput(c.Hash, c.Parents ?? [], c.AuthoredAt, c.CommittedAt, c.Message,
        [.. (c.Files ?? []).Select(f => new GitFileInput(f.Path, f.Change, f.PreviousPath))], c.FilesTruncated))],
      Guid.Parse(User.FindFirstValue(ApiKeyAuthenticationHandler.KeyIdClaim)!)), cancellationToken); // the policy guarantees a key

    return result.ToOkResult(stored => new IngestGitObservationsResponse(stored.CommitsStored, stored.CommitsAlreadyStored));
  }
}
