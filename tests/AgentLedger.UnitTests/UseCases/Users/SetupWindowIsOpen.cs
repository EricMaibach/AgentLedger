using AgentLedger.UseCases.Users;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.UnitTests.UseCases.Users;

// Setup closes 10 minutes after startup (ADR 0018).
public sealed class SetupWindowIsOpen
{
  [Fact]
  public void IsOpenForTenMinutesAfterStartup()
  {
    var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));
    var window = new SetupWindow(clock);

    clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromTicks(1));
    window.IsOpen.ShouldBeTrue();

    clock.Advance(TimeSpan.FromTicks(1));
    window.IsOpen.ShouldBeFalse();
  }
}
