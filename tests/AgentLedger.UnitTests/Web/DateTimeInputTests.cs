using AgentLedger.Web.Components.Formatting;

namespace AgentLedger.UnitTests.Web;

// The list's time-range inputs: HTML datetime-local values have no time zone; the UI is UTC throughout.
public sealed class DateTimeInputTests
{
  [Theory]
  [InlineData("2026-10-22T09:30")]           // what a datetime-local input sends
  [InlineData("2026-10-22T09:30:00Z")]       // ISO, as in links copied from the API
  [InlineData("2026-10-22T05:30:00-04:00")]  // another offset, converted
  public void ReadsValuesAsUtc(string input)
  {
    DateTimeInput.TryParse(input, out var value).ShouldBeTrue();

    value.ShouldBe(new DateTimeOffset(2026, 10, 22, 9, 30, 0, TimeSpan.Zero));
    value!.Value.Offset.ShouldBe(TimeSpan.Zero);
  }

  [Fact]
  public void TreatsAMissingValueAsNoFilter()
  {
    DateTimeInput.TryParse(null, out var value).ShouldBeTrue();
    DateTimeInput.TryParse("", out var empty).ShouldBeTrue();

    value.ShouldBeNull();
    empty.ShouldBeNull();
  }

  [Fact]
  public void RejectsTextThatIsNotADate()
  {
    DateTimeInput.TryParse("yesterday", out _).ShouldBeFalse();
  }

  [Fact]
  public void FormatsForADatetimeLocalInput()
  {
    DateTimeInput.Format(new DateTimeOffset(2026, 10, 22, 9, 30, 15, TimeSpan.Zero)).ShouldBe("2026-10-22T09:30");
  }
}
