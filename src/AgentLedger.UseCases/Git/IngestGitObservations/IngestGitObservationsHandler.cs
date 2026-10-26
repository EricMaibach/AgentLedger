using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.GitAggregate;

namespace AgentLedger.UseCases.Git.IngestGitObservations;

// Stores a repository's newly observed commits (ADR 0021), each once. A batch is refused as a whole if any
// commit in it isn't valid: it came from one `git log`, so a bad entry means something is wrong with all of it.
public sealed class IngestGitObservationsHandler(IGitObservationStore store, TimeProvider clock)
  : ICommandHandler<IngestGitObservationsCommand, Result<GitObservationsStored>>
{
  public async ValueTask<Result<GitObservationsStored>> Handle(IngestGitObservationsCommand command, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(command.Repository) || command.Repository.Length > GitCommit.MaxRepositoryLength)
    {
      return Invalid("repository", $"A repository (up to {GitCommit.MaxRepositoryLength} characters) is required.");
    }

    if (command.Commits.Count is 0 or > Constants.MAX_GIT_COMMITS_PER_BATCH)
    {
      return Invalid("commits", $"A batch has 1 to {Constants.MAX_GIT_COMMITS_PER_BATCH} commits, but had {command.Commits.Count}.");
    }

    if (command.Commits.Select(c => c.Hash).Distinct().Count() != command.Commits.Count)
    {
      return Invalid("commits", "A commit appears more than once in the batch.");
    }

    var now = clock.GetUtcNow();
    var keyId = ApiKeyId.From(command.ApiKeyId);
    var commits = new List<GitCommit>();
    foreach (var input in command.Commits)
    {
      if (ToCommit(command.Repository, input, keyId, now) is not { } commit)
      {
        return Invalid("commits", $"Commit '{input.Hash}' isn't valid: hashes are full lowercase hex, files need a path and a change "
          + $"(added, modified, deleted, renamed, copied, typeChanged), and at most {GitCommit.MaxFiles} files are listed.");
      }

      commits.Add(commit);
    }

    var stored = await store.AddCommitsAsync(commits, cancellationToken);
    return new GitObservationsStored(stored, commits.Count - stored);
  }

  private static GitCommit? ToCommit(string repository, GitCommitInput input, ApiKeyId keyId, DateTimeOffset now)
  {
    if (!CommitHash.TryFrom(input.Hash, out var hash))
    {
      return null;
    }

    var parents = new List<CommitHash>();
    foreach (var parent in input.Parents)
    {
      if (!CommitHash.TryFrom(parent, out var parentHash))
      {
        return null;
      }

      parents.Add(parentHash);
    }

    var files = new List<GitCommitFile>();
    foreach (var file in input.Files)
    {
      if (!GitFileChange.TryFromName(file.Change, ignoreCase: true, out var change) || string.IsNullOrWhiteSpace(file.Path))
      {
        return null;
      }

      files.Add(new GitCommitFile(file.Path, change, file.PreviousPath));
    }

    try
    {
      return new GitCommit(repository, hash, parents, input.AuthoredAt, input.CommittedAt, input.Message, files, input.FilesTruncated, keyId, now);
    }
    catch (ArgumentException)
    {
      return null; // e.g. too many files, a missing time or message
    }
  }

  private static Result<GitObservationsStored> Invalid(string field, string message) =>
    Result.Invalid(new ValidationError(field, message));
}
