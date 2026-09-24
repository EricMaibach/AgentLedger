using AgentLedger.Core.AgentEventReceiptAggregate;
using Vogen;

namespace AgentLedger.UnitTests.Core.AgentEventReceiptAggregate;

public sealed class ReceiptIdFrom
{
  [Fact]
  public void AcceptsAServerGeneratedGuid()
  {
    var guid = Guid.CreateVersion7();

    ReceiptId.From(guid).Value.ShouldBe(guid);
  }

  [Fact]
  public void RejectsEmptyGuid()
  {
    Should.Throw<ValueObjectValidationException>(() => ReceiptId.From(Guid.Empty));
  }
}
