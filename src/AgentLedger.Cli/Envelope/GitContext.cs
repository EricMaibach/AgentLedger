namespace AgentLedger.Cli.Envelope;

/// <summary>Git details for the envelope. Every part is optional: agents can run outside a repository.</summary>
/// <param name="Repo">The origin remote as <c>host/owner/repo</c>, credentials stripped.</param>
/// <param name="Branch">The checked-out branch; null when HEAD is detached.</param>
/// <param name="Worktree">The working tree root, only when it is a linked worktree (<c>git worktree add</c>).</param>
internal sealed record GitContext(string? Repo, string? Branch, string? Worktree)
{
  public static readonly GitContext None = new(null, null, null);
}
