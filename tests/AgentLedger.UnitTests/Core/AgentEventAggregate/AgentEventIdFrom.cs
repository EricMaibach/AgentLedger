using AgentLedger.Core.AgentEventAggregate;
using Vogen;

namespace AgentLedger.UnitTests.Core.AgentEventAggregate;

public sealed class AgentEventIdFrom
{
  [Fact]
  public void AcceptsAClientGeneratedGuid()
  {
    var guid = Guid.CreateVersion7();

    AgentEventId.From(guid).Value.ShouldBe(guid);
  }

  [Fact]
  public void RejectsEmptyGuid()
  {
    Should.Throw<ValueObjectValidationException>(() => AgentEventId.From(Guid.Empty));
  }

  [Fact]
  public void EqualsAnotherIdWithTheSameGuid()
  {
    // Value equality is what makes a retried event recognisable as the same event.
    var guid = Guid.CreateVersion7();

    AgentEventId.From(guid).ShouldBe(AgentEventId.From(guid));
  }
}
