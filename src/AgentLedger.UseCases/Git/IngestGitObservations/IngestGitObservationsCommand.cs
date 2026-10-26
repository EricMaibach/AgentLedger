namespace AgentLedger.UseCases.Git.IngestGitObservations;

// One repository's newly observed commits and tag changes, as the CLI found them (ADR 0021). ApiKeyId: the authenticated sender.
public sealed record IngestGitObservationsCommand(string Repository, IReadOnlyList<GitCommitInput> Commits, IReadOnlyList<GitTagInput> Tags,
  Guid ApiKeyId)
  : ICommand<Result<GitObservationsStored>>;

public sealed record GitCommitInput(string Hash, IReadOnlyList<string> Parents, DateTimeOffset AuthoredAt, DateTimeOffset CommittedAt,
  string Message, IReadOnlyList<GitFileInput> Files, bool FilesTruncated);

// Change: added, modified, deleted, renamed, copied or typeChanged (any case).
public sealed record GitFileInput(string Path, string Change, string? PreviousPath);

// Change: existing, created, moved or deleted (any case). Commit and PreviousCommit as the change needs them.
public sealed record GitTagInput(string Name, string Change, string? Commit, string? PreviousCommit, DateTimeOffset ObservedAt);

public sealed record GitObservationsStored(int CommitsStored, int CommitsAlreadyStored, int TagObservationsRecorded);
