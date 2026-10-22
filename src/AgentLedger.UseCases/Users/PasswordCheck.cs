namespace AgentLedger.UseCases.Users;

public sealed class PasswordCheck : SmartEnum<PasswordCheck>
{
  public static readonly PasswordCheck Failed = new(nameof(Failed), 0);
  public static readonly PasswordCheck Succeeded = new(nameof(Succeeded), 1);

  // Correct, but hashed with older, weaker settings: store a fresh hash.
  public static readonly PasswordCheck SucceededRehashNeeded = new(nameof(SucceededRehashNeeded), 2);

  private PasswordCheck(string name, int value) : base(name, value) { }

  public bool IsCorrect => this != Failed;
}
