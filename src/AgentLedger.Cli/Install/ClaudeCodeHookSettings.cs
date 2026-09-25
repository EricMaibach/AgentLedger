using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AgentLedger.Cli.Install;

/// <summary>
/// Adds, removes and finds AgentLedger's hook entries in one Claude Code settings file (ADR 0013).
/// Merges with whatever else the file contains; entries are recognized by their command.
/// </summary>
internal static partial class ClaudeCodeHookSettings
{
  /// <summary>Every Claude Code hook event, as registered and verified by the capture probe.</summary>
  public static readonly IReadOnlyList<string> Events =
  [
    "ConfigChange", "CwdChanged", "Elicitation", "ElicitationResult", "InstructionsLoaded", "Notification",
    "PermissionDenied", "PermissionRequest", "PostCompact", "PostModelSwitch", "PostToolBatch", "PostToolUse",
    "PostToolUseFailure", "PreCompact", "PreModelSwitch", "PreToolUse", "SessionEnd", "SessionStart", "Stop",
    "StopFailure", "SubagentStart", "SubagentStop", "TaskCompleted", "TaskCreated", "TeammateIdle",
    "UserPromptExpansion", "UserPromptSubmit",
  ];

  // Async hooks can be cut off as the session ends, so SessionEnd runs synchronously with a short timeout.
  private const string SynchronousEvent = "SessionEnd";
  private const int SynchronousTimeoutSeconds = 2;

  public static void Install(string settingsFile, string command)
  {
    var root = Read(settingsFile);
    var hooks = root["hooks"] as JsonObject ?? new JsonObject();
    root["hooks"] = hooks;
    RemoveOurs(hooks); // idempotent: replace rather than duplicate

    foreach (var hookEvent in Events)
    {
      var entry = new JsonObject { ["type"] = "command", ["command"] = command };
      if (hookEvent == SynchronousEvent)
      {
        entry["timeout"] = SynchronousTimeoutSeconds;
      }
      else
      {
        entry["async"] = true;
      }

      var groups = hooks[hookEvent] as JsonArray ?? new JsonArray();
      hooks[hookEvent] = groups;
      // The JsonNode overload: the generic Add<T> serializes by reflection, which Native AOT can't do.
      groups.Add((JsonNode)new JsonObject { ["matcher"] = "*", ["hooks"] = new JsonArray(entry) });
    }

    Write(settingsFile, root);
  }

  /// <summary>Removes AgentLedger's entries and returns how many there were; other hooks are left alone.</summary>
  public static int Uninstall(string settingsFile)
  {
    if (!File.Exists(settingsFile))
    {
      return 0;
    }

    var root = Read(settingsFile);
    if (root["hooks"] is not JsonObject hooks)
    {
      return 0;
    }

    var removed = RemoveOurs(hooks);
    if (hooks.Count == 0)
    {
      root.Remove("hooks");
    }

    if (removed > 0)
    {
      Write(settingsFile, root);
    }

    return removed;
  }

  public static bool IsInstalled(string settingsFile)
  {
    try
    {
      return File.Exists(settingsFile)
             && Read(settingsFile)["hooks"] is JsonObject hooks
             && hooks.Any(evt => Hooks(evt.Value).Any(IsOursEntry));
    }
    catch (InvalidDataException)
    {
      return false;
    }
  }

  /// <summary>True for `agentledger hook claude-code`, with or without a (quoted) path to the binary.</summary>
  public static bool IsOurs(string command) => OurCommand().IsMatch(command);

  [GeneratedRegex("""^(?:"?[^"]*[\\/])?agentledger(?:\.exe)?"?\s+hook\s+claude-code\s*$""", RegexOptions.IgnoreCase)]
  private static partial Regex OurCommand();

  private static bool IsOursEntry(JsonNode? hook) =>
    hook?["command"] is JsonValue value && value.TryGetValue<string>(out var command) && IsOurs(command);

  // All hook entries under one event: [ { "matcher": ..., "hooks": [ entry, ... ] }, ... ]
  private static IEnumerable<JsonNode?> Hooks(JsonNode? groups) =>
    groups is JsonArray array
      ? array.SelectMany(group => group?["hooks"] as JsonArray ?? [])
      : [];

  private static int RemoveOurs(JsonObject hooks)
  {
    var removed = 0;
    foreach (var hookEvent in hooks.Select(evt => evt.Key).ToList())
    {
      if (hooks[hookEvent] is not JsonArray groups)
      {
        continue;
      }

      foreach (var group in groups.ToList())
      {
        if (group?["hooks"] is not JsonArray entries)
        {
          continue;
        }

        foreach (var entry in entries.Where(IsOursEntry).ToList())
        {
          entries.Remove(entry);
          removed++;
        }

        if (entries.Count == 0)
        {
          groups.Remove(group); // don't leave an empty group behind
        }
      }

      if (groups.Count == 0)
      {
        hooks.Remove(hookEvent);
      }
    }

    return removed;
  }

  private static JsonObject Read(string settingsFile)
  {
    if (!File.Exists(settingsFile))
    {
      return new JsonObject();
    }

    try
    {
      return JsonNode.Parse(File.ReadAllText(settingsFile)) as JsonObject
             ?? throw new InvalidDataException($"{settingsFile} does not contain a JSON object.");
    }
    catch (JsonException ex)
    {
      throw new InvalidDataException($"{settingsFile} is not valid JSON: {ex.Message}", ex);
    }
  }

  // Written to a temporary file and renamed, so a failure never leaves a half-written settings file.
  private static void Write(string settingsFile, JsonObject root)
  {
    Directory.CreateDirectory(Path.GetDirectoryName(settingsFile)!);
    var temp = $"{settingsFile}.tmp-{Guid.NewGuid():N}";
    using (var stream = File.Create(temp))
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
    {
      root.WriteTo(writer);
    }

    File.Move(temp, settingsFile, overwrite: true);
  }
}
