using System.Globalization;

namespace AgentLedger.Web.Components.Formatting;

/// <summary>Reads and writes the time-range inputs. Values without an offset are UTC, like everything in the UI.</summary>
public static class DateTimeInput
{
  /// <summary>Empty means "no filter" (true, null). Text that isn't a date is false.</summary>
  public static bool TryParse(string? input, out DateTimeOffset? value)
  {
    value = null;
    if (string.IsNullOrWhiteSpace(input))
    {
      return true;
    }

    if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture,
          DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
    {
      value = parsed.ToUniversalTime();
      return true;
    }

    return false;
  }

  /// <summary>The <c>yyyy-MM-ddTHH:mm</c> form a datetime-local input expects, in UTC.</summary>
  public static string Format(DateTimeOffset value) =>
    value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
}
