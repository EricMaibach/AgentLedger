namespace AgentLedger.UseCases.Users;

public sealed class SetupStatus : SmartEnum<SetupStatus>
{
  // No users yet, and the setup window is open: the setup page creates the first admin.
  public static readonly SetupStatus Required = new(nameof(Required), 0);

  // No users yet, but the window closed: restarting the app opens it again.
  public static readonly SetupStatus TimedOut = new(nameof(TimedOut), 1);

  public static readonly SetupStatus Done = new(nameof(Done), 2);

  private SetupStatus(string name, int value) : base(name, value) { }
}
