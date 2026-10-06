using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.AgentEventReceiptAggregate.Specifications;

namespace AgentLedger.UnitTests.Core.AgentEventReceiptAggregate.Specifications;

// Specifications can be evaluated against an in-memory list, so their logic is unit-testable
// without a database. (The integration tests check that EF Core translates them to SQL.)
public sealed class ReceiptsByEventIdSpecEvaluate
{
  private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

  private static readonly AgentEventId EventA = AgentEventId.From(Guid.CreateVersion7());
  private static readonly AgentEventId EventB = AgentEventId.From(Guid.CreateVersion7());

  private static AgentEventReceipt Receipt(AgentEventId eventId, int secondsAfterT0) =>
    new AgentEventReceiptBuilder().WithEventId(eventId).WithReceivedAt(T0.AddSeconds(secondsAfterT0)).Build();

  [Fact]
  public void ReturnsOnlyTheReceiptsOfThatEvent()
  {
    var receipts = new[] { Receipt(EventA, 0), Receipt(EventB, 1), Receipt(EventA, 2) };

    var result = new ReceiptsByEventIdSpec(EventA).Evaluate(receipts).ToList();

    result.Count.ShouldBe(2);
    result.ShouldAllBe(r => r.EventId == EventA);
  }

  [Fact]
  public void ListsTheOldestReceiptFirst()
  {
    // The first receipt is the original send; later ones are resends (ADR 0010).
    var resend = Receipt(EventA, 30);
    var original = Receipt(EventA, 0);

    var result = new ReceiptsByEventIdSpec(EventA).Evaluate([resend, original]).ToList();

    result.ShouldBe([original, resend]);
  }

  [Fact]
  public void OrdersReceiptsWithTheSameTimestampConsistently()
  {
    // Several receipts can land in the same millisecond (seen when dogfooding), so the order
    // needs a tiebreaker: the receipt ID. The same input must always give the same order.
    var a = Receipt(EventA, 0);
    var b = Receipt(EventA, 0);
    var c = Receipt(EventA, 0);

    var forwards = new ReceiptsByEventIdSpec(EventA).Evaluate([a, b, c]).Select(r => r.Id).ToList();
    var backwards = new ReceiptsByEventIdSpec(EventA).Evaluate([c, b, a]).Select(r => r.Id).ToList();

    backwards.ShouldBe(forwards);
  }

  [Fact]
  public void ReturnsNothingForAnUnknownEvent()
  {
    var receipts = new[] { Receipt(EventB, 0) };

    new ReceiptsByEventIdSpec(EventA).Evaluate(receipts).ShouldBeEmpty();
  }
}
