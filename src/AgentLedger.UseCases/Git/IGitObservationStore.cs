using AgentLedger.Core.GitAggregate;

namespace AgentLedger.UseCases.Git;

// Write store for git observations (docs/architecture.md, "Write store"; ADR 0021): one batch in one transaction,
// skipping commits already stored and appending every tag observation.
public interface IGitObservationStore
{
  Task<GitStoreCounts> AddAsync(IReadOnlyList<GitCommit> commits, IReadOnlyList<GitTagObservation> tags, CancellationToken cancellationToken);
}

// How many of the batch's commits and tag observations were new.
public sealed record GitStoreCounts(int CommitsStored, int TagsStored);
