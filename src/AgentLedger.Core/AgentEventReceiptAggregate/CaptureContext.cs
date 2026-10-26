namespace AgentLedger.Core.AgentEventReceiptAggregate;

// Where an event was captured. Git details are optional: agents can run outside a repository.
public sealed record CaptureContext(
  string Host,
  string User,
  string ProjectDir,
  string? GitRepo,
  string? GitBranch,
  string? GitWorktree,
  string? GitCommit = null)
{
  // Redeclaring a positional property lets its initializer validate the constructor argument.
  // Parameter names are given explicitly so errors name the camelCase request field.
  // `private init`: EF only maps properties with a setter; private keeps `with` from bypassing the guards.
  public string Host { get; private init; } = Guard.Against.NullOrWhiteSpace(Host, "host");
  public string User { get; private init; } = Guard.Against.NullOrWhiteSpace(User, "user");
  public string ProjectDir { get; private init; } = Guard.Against.NullOrWhiteSpace(ProjectDir, "projectDir");

  // The checked-out commit the work was based on (ADR 0021): a full SHA-1 or SHA-256 hash in lowercase hex, as git writes it.
  public string? GitCommit { get; private init; } = GitCommit is null || IsCommitHash(GitCommit)
    ? GitCommit
    : throw new ArgumentException($"gitCommit must be a full commit hash in lowercase hex, but was '{GitCommit}'.", "gitCommit");

  private static bool IsCommitHash(string value) => value.Length is 40 or 64 && value.All(char.IsAsciiHexDigitLower);
}
