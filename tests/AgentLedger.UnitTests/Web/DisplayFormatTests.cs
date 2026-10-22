using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

public sealed class DisplayFormatTests
{
  private static readonly DateTimeOffset Moment = new(2026, 10, 1, 14, 3, 22, 417, TimeSpan.Zero);

  [Fact]
  public void FormatsTimestampsInUtcWithoutMillisecondsForLists()
  {
    DisplayFormat.Timestamp(Moment).ShouldBe("2026-10-01 14:03:22 UTC");
  }

  [Fact]
  public void FormatsTimestampsWithMillisecondsForDetailPages()
  {
    DisplayFormat.Timestamp(Moment, withMilliseconds: true).ShouldBe("2026-10-01 14:03:22.417 UTC");
  }

  [Fact]
  public void ConvertsOtherOffsetsToUtc()
  {
    var ohioTime = Moment.ToOffset(TimeSpan.FromHours(-4));

    DisplayFormat.Timestamp(ohioTime).ShouldBe("2026-10-01 14:03:22 UTC");
  }

  [Fact]
  public void GivesTheMachineReadableIsoValueForTimeElements()
  {
    DisplayFormat.IsoTimestamp(Moment).ShouldBe("2026-10-01T14:03:22.4170000Z");
  }

  [Fact]
  public void ShortensIdsToTheirFirstEightCharacters()
  {
    DisplayFormat.ShortId(Guid.Parse("01a0d664-9d9a-7880-bf96-fc316632a19d")).ShouldBe("01a0d664");
    DisplayFormat.ShortId("3829bce8-0000-0000-0000-000000000000").ShouldBe("3829bce8");
    DisplayFormat.ShortId("abc").ShouldBe("abc"); // shorter values stay whole
  }
}
