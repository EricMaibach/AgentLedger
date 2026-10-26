using AgentLedger.Core.GitAggregate;

namespace AgentLedger.UseCases.Git;

// Write store for git observations (docs/architecture.md, "Write store"; ADR 0021): a batch in one statement,
// skipping commits already stored.
public interface IGitObservationStore
{
  // Inserts the commits not stored yet; returns how many were new.
  Task<int> AddCommitsAsync(IReadOnlyList<GitCommit> commits, CancellationToken cancellationToken);
}
