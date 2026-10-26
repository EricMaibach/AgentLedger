namespace AgentLedger.UseCases.Git.IngestGitObservations;

// One repository's newly observed commits, as the CLI found them (ADR 0021). ApiKeyId: the authenticated sender.
public sealed record IngestGitObservationsCommand(string Repository, IReadOnlyList<GitCommitInput> Commits, Guid ApiKeyId)
  : ICommand<Result<GitObservationsStored>>;

public sealed record GitCommitInput(string Hash, IReadOnlyList<string> Parents, DateTimeOffset AuthoredAt, DateTimeOffset CommittedAt,
  string Message, IReadOnlyList<GitFileInput> Files, bool FilesTruncated);

// Change: added, modified, deleted, renamed, copied or typeChanged (any case).
public sealed record GitFileInput(string Path, string Change, string? PreviousPath);

public sealed record GitObservationsStored(int CommitsStored, int CommitsAlreadyStored);
