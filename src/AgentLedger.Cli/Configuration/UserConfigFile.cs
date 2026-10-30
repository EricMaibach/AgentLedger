using System.Text.Json;

namespace AgentLedger.Cli.Configuration;

/// <summary>
/// The per-user config file: <c>~/.config/agentledger/config.json</c> (or %APPDATA% on Windows). The only file
/// the API key may live in (ADR 0012); `login` and `setup` write it, readable only by the user (ADR 0019, ADR 0024).
/// </summary>
internal static class UserConfigFile
{
  public static string PathFor(IHostEnvironment environment) =>
    Path.Combine(environment.UserConfigDirectory, "agentledger", "config.json");

  /// <summary>Sets apiKey, keeping the file's other settings.</summary>
  public static void SetApiKey(IHostEnvironment environment, string apiKey) => Set(environment, "apiKey", apiKey);

  /// <summary>Sets one setting (e.g. url, saved by `setup`, ADR 0024), keeping the file's others.</summary>
  public static void Set(IHostEnvironment environment, string key, string value)
  {
    var path = PathFor(environment);
    var existing = ReadProperties(path);

    using var buffer = new MemoryStream();
    using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
    {
      writer.WriteStartObject();
      foreach (var property in existing.Where(p => p.Name != key))
      {
        property.WriteTo(writer);
      }

      writer.WriteString(key, value);
      writer.WriteEndObject();
    }

    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
    if (!OperatingSystem.IsWindows())
    {
      options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; // a new file: only the user can read it
      if (File.Exists(path))
      {
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); // an existing one: tighten it first
      }
    }

    using var file = new FileStream(path, options);
    file.Write(buffer.ToArray());
  }

  // The file's properties as JSON elements (cloned, so they outlive the document); none if it's missing or unreadable.
  private static List<JsonProperty> ReadProperties(string path)
  {
    if (!File.Exists(path))
    {
      return [];
    }

    try
    {
      using var json = JsonDocument.Parse(File.ReadAllText(path));
      return json.RootElement.ValueKind == JsonValueKind.Object
        ? [.. json.RootElement.Clone().EnumerateObject()]
        : [];
    }
    catch (JsonException)
    {
      return [];
    }
  }
}
