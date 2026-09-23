namespace AgentLedger.Core.AgentEventAggregate;

public sealed record CaptureContext(
    string Host,
    string User,
    string ProjectDir,
    string? GitRepo,
    string? GitBranch,
    string? GitWorktree
);
