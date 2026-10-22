namespace AgentLedger.Web.Users;

// Where to go after signing in: only a path on this site, never another site ("open redirect").
internal static class LocalReturnUrl
{
  public const string Default = "/raw/events";

  public static string OrDefault(string? returnUrl) =>
    returnUrl is ['/', ..] && !returnUrl.StartsWith("//", StringComparison.Ordinal) && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
      ? returnUrl
      : Default;
}
