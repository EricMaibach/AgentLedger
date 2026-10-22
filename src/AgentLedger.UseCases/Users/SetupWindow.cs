namespace AgentLedger.UseCases.Users;

// First-run setup is only possible for a short time after startup (ADR 0018, as Portainer does), which limits
// the window in which someone else on the network could claim a new installation. Registered as a singleton
// and created at startup, so the window starts when the app does.
public sealed class SetupWindow(TimeProvider clock)
{
  public static readonly TimeSpan Duration = TimeSpan.FromMinutes(10);

  private readonly DateTimeOffset _closesAt = clock.GetUtcNow() + Duration;

  public bool IsOpen => clock.GetUtcNow() < _closesAt;
}
