using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentLedger.Web.Components.Formatting;

/// <summary>How the work pages show times, sizes and names (ADR 0023).</summary>
public static partial class WorkFormat
{
  /// <summary>Time since the turn started: <c>+0:05</c>, <c>+12:03</c>, <c>+1:02:03</c>.</summary>
  public static string Offset(DateTimeOffset turnStart, DateTimeOffset at)
  {
    var elapsed = at - turnStart;
    if (elapsed < TimeSpan.Zero)
      elapsed = TimeSpan.Zero;
    return elapsed.TotalHours >= 1
      ? string.Create(CultureInfo.InvariantCulture, $"+{(int)elapsed.TotalHours}:{elapsed.Minutes:00}:{elapsed.Seconds:00}")
      : string.Create(CultureInfo.InvariantCulture, $"+{elapsed.Minutes}:{elapsed.Seconds:00}");
  }

  /// <summary>A day for date dividers: <c>Tue 27 Oct 2026</c>, in UTC like every time in the UI (docs/ui.md).</summary>
  public static string Day(DateTimeOffset value) =>
    value.UtcDateTime.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

  /// <summary>Hours and minutes in UTC: <c>01:47</c>.</summary>
  public static string TimeOfDay(DateTimeOffset value) => value.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture);

  /// <summary><c>0.4s</c>, <c>48s</c>, <c>2m 10s</c>, <c>1h 03m</c>; null without an end.</summary>
  public static string? Duration(DateTimeOffset start, DateTimeOffset? end)
  {
    if (end is not { } e)
      return null;
    var d = e - start;
    if (d < TimeSpan.Zero)
      d = TimeSpan.Zero;
    return d.TotalSeconds switch
    {
      < 10 => string.Create(CultureInfo.InvariantCulture, $"{Math.Floor(d.TotalSeconds * 10) / 10:0.0}s"),
      < 60 => string.Create(CultureInfo.InvariantCulture, $"{(int)d.TotalSeconds}s"),
      < 3600 => string.Create(CultureInfo.InvariantCulture, $"{(int)d.TotalMinutes}m {d.Seconds:00}s"),
      _ => string.Create(CultureInfo.InvariantCulture, $"{(int)d.TotalHours}h {d.Minutes:00}m"),
    };
  }

  /// <summary>Token counts at a glance: <c>412</c>, <c>2.3k</c>, <c>551k</c>, <c>1.5M</c>.</summary>
  public static string Tokens(long count) => count switch
  {
    < 1_000 => count.ToString(CultureInfo.InvariantCulture),
    < 10_000 => Scaled(count / 1_000d, "k"),
    < 1_000_000 => (count / 1_000).ToString(CultureInfo.InvariantCulture) + "k",
    < 10_000_000 => Scaled(count / 1_000_000d, "M"),
    _ => (count / 1_000_000).ToString(CultureInfo.InvariantCulture) + "M",
  };

  /// <summary>Claude model IDs as people say them (<c>claude-opus-5-5</c> → Opus 5.5); others as they are.</summary>
  public static string Model(string model)
  {
    var match = ClaudeModel().Match(model);
    if (!match.Success)
      return model;
    var family = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(match.Groups["family"].Value);
    var minor = match.Groups["minor"].Success ? "." + match.Groups["minor"].Value : "";
    return $"{family} {match.Groups["major"].Value}{minor}";
  }

  /// <summary>A repository's name: the last part of its identity (remote URL, or host and folder).</summary>
  public static string? Project(string? repository)
  {
    if (string.IsNullOrWhiteSpace(repository))
      return null;
    var name = repository.TrimEnd('/').Split('/', ':').Last();
    return name.EndsWith(".git", StringComparison.Ordinal) ? name[..^4] : name;
  }

  /// <summary>A tool input that is a flat JSON object, as names and values; null when it needs the JSON view.</summary>
  public static IReadOnlyList<(string Name, string Value)>? InputPairs(string? input)
  {
    if (input is null)
      return null;
    try
    {
      using var json = JsonDocument.Parse(input);
      if (json.RootElement.ValueKind != JsonValueKind.Object)
        return null;
      var pairs = new List<(string, string)>();
      foreach (var property in json.RootElement.EnumerateObject())
      {
        if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
          return null;
        pairs.Add((property.Name, property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : property.Value.GetRawText()));
      }
      return pairs;
    }
    catch (JsonException)
    {
      return null;
    }
  }

  /// <summary>The first <paramref name="lines"/> lines, and how many more there are.</summary>
  public static (string Shown, int Hidden) Preview(string text, int lines)
  {
    var all = text.Split('\n');
    return all.Length <= lines ? (text, 0) : (string.Join('\n', all.Take(lines)), all.Length - lines);
  }

  private static string Scaled(double value, string unit) =>
    (Math.Floor(value * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture) + unit;

  [GeneratedRegex(@"^claude-(?<family>[a-z]+)-(?<major>\d+)(?:-(?<minor>\d{1,2}))?(?:-\d{8})?$")]
  private static partial Regex ClaudeModel();
}
