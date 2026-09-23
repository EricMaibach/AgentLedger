using Vogen;
[assembly: VogenDefaults(staticAbstractsGeneration: StaticAbstractsGeneration.MostCommon | StaticAbstractsGeneration.InstanceMethodsAndProperties)]

namespace AgentLedger.Core.AgentEventAggregate;

[ValueObject<Guid>]
public readonly partial struct AgentEventId
{
  private static Validation Validate(Guid value) => value == Guid.Empty ? Validation.Invalid("AgentEventId cannot be empty") : Validation.Ok;
}
