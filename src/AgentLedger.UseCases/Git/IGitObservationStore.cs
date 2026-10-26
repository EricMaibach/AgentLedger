using AgentLedger.Core.GitAggregate;

namespace AgentLedger.UseCases.Git;

// Write store for git observations (docs/architecture.md, "Write store"; ADR 0021): one batch in one transaction,
// skipping commits already stored and appending every tag observation.
public interface IGitObservationStore
{
  // Returns how many of the commits were new.
  Task<int> AddAsync(IReadOnlyList<GitCommit> commits, IReadOnlyList<GitTagObservation> tags, CancellationToken cancellationToken);
}
