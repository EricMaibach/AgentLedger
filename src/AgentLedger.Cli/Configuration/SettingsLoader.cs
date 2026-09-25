using System.Text.Json;

namespace AgentLedger.Cli.Configuration;

/// <summary>
/// Resolves each setting separately; the first source that has a valid value wins (ADR 0012):
/// environment variable, then the project's .agentledger.json (found by walking up), then the user
/// config file, then the default. Invalid values are skipped with a warning rather than failing.
/// </summary>
internal sealed class SettingsLoader(IHostEnvironment environment)
{
  public const string ProjectFileName = ".agentledger.json";

  private static readonly Uri DefaultUrl = new("http://localhost:57678");
  private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

  public CliSettings Load(string startDirectory)
  {
    var warnings = new List<string>();
    var project = ReadFile(FindProjectFile(startDirectory), "project", warnings);
    var user = ReadFile(Path.Combine(environment.UserConfigDirectory, "agentledger", "config.json"), "user", warnings);

    if (project?.Values.ContainsKey("apiKey") == true)
    {
      warnings.Add($"Ignored apiKey in {project.Path}: the project file is committed to git. Use AGENTLEDGER_API_KEY or the user config file.");
    }

    var url = Resolve("AGENTLEDGER_URL", "url", ParseUrl, DefaultUrl, [project, user], warnings);
    var apiKey = Resolve<string?>("AGENTLEDGER_API_KEY", "apiKey", value => (true, value), null, [user], warnings); // never the project file
    var timeout = Resolve("AGENTLEDGER_TIMEOUT_MS", "timeoutMs", ParseTimeout, DefaultTimeout, [project, user], warnings);
    var spool = Resolve("AGENTLEDGER_SPOOL_DIR", "spoolDir", value => (true, value), Path.Combine(environment.LocalDataDirectory, "agentledger", "spool"), [project, user], warnings);
    var enabled = Resolve("AGENTLEDGER_ENABLED", "enabled", ParseBool, true, [project, user], warnings); // per-project opt-out (ADR 0013)

    return new CliSettings(url, apiKey, timeout, spool, enabled, warnings);
  }

  private Setting<T> Resolve<T>(string variable, string key, Func<string, (bool Ok, T Value)> parse, T defaultValue,
    ConfigFile?[] files, List<string> warnings)
  {
    if (environment.GetVariable(variable) is { Length: > 0 } fromEnvironment)
    {
      if (parse(fromEnvironment) is (true, var value))
      {
        return new Setting<T>(value, $"environment ({variable})");
      }

      warnings.Add($"Ignored invalid {variable}: '{fromEnvironment}'.");
    }

    foreach (var file in files)
    {
      if (file is null || !file.Values.TryGetValue(key, out var fromFile))
      {
        continue;
      }

      if (parse(fromFile) is (true, var value))
      {
        return new Setting<T>(value, $"{file.Kind} ({file.Path})");
      }

      warnings.Add($"Ignored invalid {key} in {file.Path}: '{fromFile}'.");
    }

    return new Setting<T>(defaultValue, "default");
  }

  private static (bool, Uri) ParseUrl(string value) =>
    Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
      ? (true, uri)
      : (false, DefaultUrl);

  // JSON booleans reach here as "true"/"false" text, like environment variables.
  private static (bool, bool) ParseBool(string value) =>
    bool.TryParse(value, out var parsed) ? (true, parsed) : (false, true);

  private static (bool, TimeSpan) ParseTimeout(string value) =>
    int.TryParse(value, out var ms) && ms > 0 ? (true, TimeSpan.FromMilliseconds(ms)) : (false, DefaultTimeout);

  private static string? FindProjectFile(string startDirectory)
  {
    for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
    {
      var candidate = Path.Combine(dir.FullName, ProjectFileName);
      if (File.Exists(candidate))
      {
        return candidate;
      }
    }

    return null;
  }

  // Reads a flat JSON object into strings (numbers keep their text). JsonDocument needs no
  // reflection, so this stays Native AOT-safe without a source-generated context.
  private static ConfigFile? ReadFile(string? path, string kind, List<string> warnings)
  {
    if (path is null || !File.Exists(path))
    {
      return null;
    }

    try
    {
      using var json = JsonDocument.Parse(File.ReadAllText(path));
      var values = new Dictionary<string, string>();
      foreach (var property in json.RootElement.EnumerateObject())
      {
        if (property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
          values[property.Name] = property.Value.ValueKind == JsonValueKind.String
            ? property.Value.GetString()!
            : property.Value.GetRawText(); // numbers and booleans keep their JSON text
        }
      }

      return new ConfigFile(path, kind, values);
    }
    catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException or UnauthorizedAccessException)
    {
      warnings.Add($"Ignored unreadable config file {path}: {ex.Message}");
      return null;
    }
  }

  private sealed record ConfigFile(string Path, string Kind, IReadOnlyDictionary<string, string> Values);
}
