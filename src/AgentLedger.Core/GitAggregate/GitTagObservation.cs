using AgentLedger.Core.ApiKeyAggregate;
using Vogen;

namespace AgentLedger.Core.GitAggregate;

[ValueObject<Guid>]
public readonly partial struct GitTagObservationId
{
  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("GitTagObservationId cannot be empty.") : Validation.Ok;
}

// What the CLI saw happen to a tag: already there when it first saw the repository, created, moved or deleted.
public sealed class GitTagChange : SmartEnum<GitTagChange>
{
  public static readonly GitTagChange Existing = new(nameof(Existing), 1);
  public static readonly GitTagChange Created = new(nameof(Created), 2);
  public static readonly GitTagChange Moved = new(nameof(Moved), 3);
  public static readonly GitTagChange Deleted = new(nameof(Deleted), 4);

  private GitTagChange(string name, int value) : base(name, value) { }
}

// A tag seen at a moment (ADR 0021). Tags can be moved or deleted, so observations are appended and never updated;
// a tag's current commit is derived by projections. Each change carries the commits that describe it.
public sealed class GitTagObservation : EntityBase<GitTagObservation, GitTagObservationId>, IAggregateRoot
{
  public const int MaxTagNameLength = 255;

  public string Repository { get; private set; }
  public string TagName { get; private set; }
  public GitTagChange Change { get; private set; }

  // The commit the tag points at; none once deleted.
  public CommitHash? Commit { get; private set; }

  // The commit it pointed at before: for a move or a deletion.
  public CommitHash? PreviousCommit { get; private set; }

  public DateTimeOffset ObservedAt { get; private set; }
  public ApiKeyId ApiKeyId { get; private set; }
  public DateTimeOffset ReceivedAt { get; private set; }

  public GitTagObservation(GitTagObservationId id, string repository, string tagName, GitTagChange change, CommitHash? commit,
    CommitHash? previousCommit, DateTimeOffset observedAt, ApiKeyId apiKeyId, DateTimeOffset receivedAt)
  {
    Id = id;
    Repository = Guard.Against.StringTooLong(Guard.Against.NullOrWhiteSpace(repository), GitCommit.MaxRepositoryLength, nameof(repository));
    TagName = Guard.Against.StringTooLong(Guard.Against.NullOrWhiteSpace(tagName), MaxTagNameLength, nameof(tagName));
    Change = Guard.Against.Null(change);

    var (needsCommit, needsPrevious) = Change.Name switch
    {
      nameof(GitTagChange.Moved) => (true, true),
      nameof(GitTagChange.Deleted) => (false, true),
      _ => (true, false), // existing, created
    };
    if (commit.HasValue != needsCommit || previousCommit.HasValue != needsPrevious)
    {
      throw new ArgumentException($"A tag that was {Change.Name.ToLowerInvariant()} " +
        $"{(needsCommit ? "has" : "has no")} commit and {(needsPrevious ? "a" : "no")} previous commit.", nameof(change));
    }

    Commit = commit;
    PreviousCommit = previousCommit;
    ObservedAt = Guard.Against.Default(observedAt);
    ApiKeyId = apiKeyId;
    ReceivedAt = Guard.Against.Default(receivedAt);
  }

#pragma warning disable CS8618 // Required by EF Core: properties are set when a row is loaded
  private GitTagObservation() { }
#pragma warning restore CS8618
}
