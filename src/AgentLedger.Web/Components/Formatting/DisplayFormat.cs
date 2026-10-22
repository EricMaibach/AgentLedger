using System.Globalization;

namespace AgentLedger.Web.Components.Formatting;

/// <summary>How the UI shows times and identifiers (docs/ui.md, "Building blocks").</summary>
public static class DisplayFormat
{
  public const int ShortIdLength = 8;

  /// <summary><c>2026-10-01 14:03:22 UTC</c>, or with milliseconds on detail pages. Always UTC.</summary>
  public static string Timestamp(DateTimeOffset value, bool withMilliseconds = false) =>
    value.UtcDateTime.ToString(withMilliseconds ? "yyyy-MM-dd HH:mm:ss.fff" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";

  /// <summary>The full ISO 8601 value for a <c>&lt;time datetime&gt;</c> attribute.</summary>
  public static string IsoTimestamp(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

  public static string ShortId(Guid value) => ShortId(value.ToString());

  public static string ShortId(string value) => value.Length <= ShortIdLength ? value : value[..ShortIdLength];
}
