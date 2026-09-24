namespace AgentLedger.Core.AgentEventReceiptAggregate;

// Where an event was captured. Git details are optional: agents can run outside a repository.
public sealed record CaptureContext(
  string Host,
  string User,
  string ProjectDir,
  string? GitRepo,
  string? GitBranch,
  string? GitWorktree)
{
  // Redeclaring a positional property lets its initializer validate the constructor argument.
  // Parameter names are given explicitly so errors name the camelCase request field.
  // `private init`: EF only maps properties with a setter; private keeps `with` from bypassing the guards.
  public string Host { get; private init; } = Guard.Against.NullOrWhiteSpace(Host, "host");
  public string User { get; private init; } = Guard.Against.NullOrWhiteSpace(User, "user");
  public string ProjectDir { get; private init; } = Guard.Against.NullOrWhiteSpace(ProjectDir, "projectDir");
}
