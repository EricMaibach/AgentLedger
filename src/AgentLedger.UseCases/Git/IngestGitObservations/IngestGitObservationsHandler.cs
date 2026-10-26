using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.GitAggregate;

namespace AgentLedger.UseCases.Git.IngestGitObservations;

// Stores a repository's newly observed commits (ADR 0021), each once, and its tag changes, appended. A batch is refused as a whole if any
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

    if (command.Commits.Count == 0 && command.Tags.Count == 0)
    {
      return Invalid("commits", "A batch has commits, tags, or both.");
    }

    if (command.Commits.Count > Constants.MAX_GIT_COMMITS_PER_BATCH)
    {
      return Invalid("commits", $"A batch has at most {Constants.MAX_GIT_COMMITS_PER_BATCH} commits, but had {command.Commits.Count}.");
    }

    if (command.Tags.Count > Constants.MAX_GIT_TAGS_PER_BATCH)
    {
      return Invalid("tags", $"A batch has at most {Constants.MAX_GIT_TAGS_PER_BATCH} tags, but had {command.Tags.Count}.");
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

    var tags = new List<GitTagObservation>();
    foreach (var input in command.Tags)
    {
      if (ToTag(command.Repository, input, keyId, now) is not { } tag)
      {
        return Invalid("tags", $"Tag '{input.Name}' isn't valid: the change is existing, created, moved or deleted; existing and created "
          + "have a commit; moved has a commit and a previous commit; deleted has only a previous commit; hashes are full lowercase hex.");
      }

      tags.Add(tag);
    }

    var stored = await store.AddAsync(commits, tags, cancellationToken);
    return new GitObservationsStored(stored, commits.Count - stored, tags.Count);
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

  private static GitTagObservation? ToTag(string repository, GitTagInput input, ApiKeyId keyId, DateTimeOffset now)
  {
    if (!GitTagChange.TryFromName(input.Change, ignoreCase: true, out var change)
        || !TryHash(input.Commit, out var commit)
        || !TryHash(input.PreviousCommit, out var previous))
    {
      return null;
    }

    try
    {
      return new GitTagObservation(GitTagObservationId.From(Guid.CreateVersion7()), repository, input.Name, change, commit, previous,
        input.ObservedAt, keyId, now);
    }
    catch (ArgumentException)
    {
      return null; // e.g. commits that don't fit the change, or a missing name
    }
  }

  // Absent is fine (whether it's needed depends on the change); present must be a full hash.
  private static bool TryHash(string? value, out CommitHash? hash)
  {
    hash = null;
    if (value is null)
    {
      return true;
    }

    if (!CommitHash.TryFrom(value, out var parsed))
    {
      return false;
    }

    hash = parsed;
    return true;
  }

  private static Result<GitObservationsStored> Invalid(string field, string message) =>
    Result.Invalid(new ValidationError(field, message));
}
