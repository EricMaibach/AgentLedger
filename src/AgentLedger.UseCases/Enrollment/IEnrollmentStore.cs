using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.Core.EnrollmentAggregate;

namespace AgentLedger.UseCases.Enrollment;

// Exchanging a code for a key (ADR 0024), as one transaction: the code is marked used only if no one else used it
// first, and the key is stored with it. False when another request got there first.
public interface IEnrollmentStore
{
  Task<bool> RedeemAsync(EnrollmentCode usedCode, ApiKey newKey, CancellationToken cancellationToken);
}
