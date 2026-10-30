namespace AgentLedger.Core.EnrollmentAggregate.Specifications;

// The code a machine presented, found by its hash (ADR 0024).
public sealed class EnrollmentCodeByHashSpec : Specification<EnrollmentCode>, ISingleResultSpecification<EnrollmentCode>
{
  public EnrollmentCodeByHashSpec(EnrollmentCodeHash hash) => Query.Where(c => c.CodeHash == hash);
}
