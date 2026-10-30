using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.Enrollment;

// Reading connected machines (ADR 0024): keys created by enrollment, with where and when each was connected.
public interface IEnrollmentQueryService
{
  // Most recently connected first; one owner's, or everyone's when owner is null.
  Task<IReadOnlyList<MachineListItem>> ListMachinesAsync(UserId? owner, CancellationToken cancellationToken);
}

public sealed record MachineListItem(Guid KeyId, string KeyName, string Host, string Owner, DateTimeOffset ConnectedAt,
  DateTimeOffset? LastUsedAt, string? ClientVersion, DateTimeOffset? RevokedAt, DateTimeOffset? ExpiresAt);
