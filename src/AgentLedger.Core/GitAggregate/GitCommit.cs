using AgentLedger.Core.ApiKeyAggregate;
using Vogen;

namespace AgentLedger.Core.GitAggregate;

// A full commit hash: SHA-1 (40) or SHA-256 (64) in lowercase hex, as git writes it.
[ValueObject<string>]
public readonly partial struct CommitHash
{
  private static Validation Validate(string value) =>
    value.Length is 40 or 64 && value.All(char.IsAsciiHexDigitLower)
      ? Validation.Ok
      : Validation.Invalid("A commit hash is 40 or 64 lowercase hex characters.");
}

// A commit's identity in the ledger: its repository and hash (ADR 0021). A hash is the commit's content, so the
// same hash in the same repository is the same commit; a fork has its own record.
[ValueObject<Guid>]
public readonly partial struct GitCommitId
{
  public static GitCommitId For(string repository, CommitHash hash) =>
    From(ContentDerivedId.Derive("git-commit", repository, hash.Value));

  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("GitCommitId cannot be empty.") : Validation.Ok;
}

// How a commit changed a file, as git's --name-status reports it (A, M, D, R, C, T).
public sealed class GitFileChange : SmartEnum<GitFileChange>
{
  public static readonly GitFileChange Added = new(nameof(Added), 1);
  public static readonly GitFileChange Modified = new(nameof(Modified), 2);
  public static readonly GitFileChange Deleted = new(nameof(Deleted), 3);
  public static readonly GitFileChange Renamed = new(nameof(Renamed), 4);
  public static readonly GitFileChange Copied = new(nameof(Copied), 5);
  public static readonly GitFileChange TypeChanged = new(nameof(TypeChanged), 6);

  private GitFileChange(string name, int value) : base(name, value) { }
}

// A file a commit changed; PreviousPath for renames and copies.
public sealed record GitCommitFile(string Path, GitFileChange Change, string? PreviousPath)
{
  public string Path { get; private init; } = Guard.Against.NullOrWhiteSpace(Path, "path");
  public GitFileChange Change { get; private init; } = Guard.Against.Null(Change, "change");
}

// A commit as observed in a repository where an agent ran (ADR 0021): raw facts from git, stored once.
// Which agent work went into it is inferred later, by projections.
public sealed class GitCommit : EntityBase<GitCommit, GitCommitId>, IAggregateRoot
{
  public const int MaxFiles = 1000;
  public const int MaxRepositoryLength = 500;

  public string Repository { get; private set; }
  public CommitHash Hash { get; private set; }
  public IReadOnlyList<CommitHash> ParentHashes { get; private set; }
  public DateTimeOffset AuthoredAt { get; private set; }
  public DateTimeOffset CommittedAt { get; private set; }

  // The full message, exactly as git reports it.
  public string Message { get; private set; }

  public IReadOnlyList<GitCommitFile> Files { get; private set; }

  // True when the commit changed more files than are listed (MaxFiles).
  public bool FilesTruncated { get; private set; }

  public ApiKeyId ApiKeyId { get; private set; }
  public DateTimeOffset ReceivedAt { get; private set; }

  public GitCommit(string repository, CommitHash hash, IReadOnlyList<CommitHash> parentHashes, DateTimeOffset authoredAt,
    DateTimeOffset committedAt, string message, IReadOnlyList<GitCommitFile> files, bool filesTruncated, ApiKeyId apiKeyId,
    DateTimeOffset receivedAt)
  {
    Repository = Guard.Against.StringTooLong(Guard.Against.NullOrWhiteSpace(repository), MaxRepositoryLength, nameof(repository));
    Hash = hash;
    ParentHashes = [.. Guard.Against.Null(parentHashes)];
    AuthoredAt = Guard.Against.Default(authoredAt);
    CommittedAt = Guard.Against.Default(committedAt);
    Message = Guard.Against.Null(message);
    Files = [.. Guard.Against.Null(files)];
    if (Files.Count > MaxFiles)
    {
      throw new ArgumentException($"At most {MaxFiles} files are listed per commit.", nameof(files));
    }

    FilesTruncated = filesTruncated;
    ApiKeyId = apiKeyId;
    ReceivedAt = Guard.Against.Default(receivedAt);
    Id = GitCommitId.For(Repository, Hash);
  }

#pragma warning disable CS8618 // Required by EF Core: properties are set when a row is loaded
  private GitCommit() { }
#pragma warning restore CS8618
}
