using System.Text.Json;

namespace AgentLedger.Cli.Envelope;

/// <summary>Builds the envelope for one hook invocation from the payload and the environment (ADR 0012).</summary>
internal sealed class EnvelopeBuilder(IHostEnvironment environment, TimeProvider clock)
{
  private const string TagPrefix = "AGENTLEDGER_TAG_";
  private const string UnknownEventType = "unknown";

  public EventEnvelope Build(string agent, string payload)
  {
    var capturedAt = clock.GetUtcNow();
    var profile = AgentProfile.For(agent);
    var (eventType, sessionId) = ReadLabels(payload, profile);
    var projectDir = profile.ResolveProjectDirectory(environment);
    var git = GitContextReader.Read(projectDir);

    return new EventEnvelope(
      EventId: Guid.CreateVersion7(capturedAt),
      Agent: agent,
      EventType: eventType,
      NativeSessionId: sessionId,
      CapturedAt: capturedAt,
      Host: NonEmpty(environment.GetVariable("AGENTLEDGER_HOST")) ?? environment.MachineName,
      User: ResolveUser(projectDir),
      ProjectDir: projectDir,
      GitRepo: git.Repo,
      GitBranch: git.Branch,
      GitWorktree: git.Worktree,
      Tags: ReadTags(),
      Payload: payload);
  }

  // Reads only the two labelling fields; the payload itself is never interpreted or altered.
  private static (string EventType, string? SessionId) ReadLabels(string payload, AgentProfile profile)
  {
    try
    {
      using var json = JsonDocument.Parse(payload);
      if (json.RootElement.ValueKind != JsonValueKind.Object)
      {
        return (UnknownEventType, null);
      }

      return (StringField(json.RootElement, profile.EventTypeField) ?? UnknownEventType,
              StringField(json.RootElement, profile.SessionIdField));
    }
    catch (JsonException)
    {
      return (UnknownEventType, null); // never lose an event over a missing label
    }
  }

  private static string? StringField(JsonElement root, string name) =>
    root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? NonEmpty(value.GetString()) : null;

  // AGENTLEDGER_USER, then git user.email (repository, then global), then the OS user.
  private string ResolveUser(string projectDir)
  {
    return NonEmpty(environment.GetVariable("AGENTLEDGER_USER"))
           ?? ReadEmail(GitContextReader.FindConfigFile(projectDir))
           ?? ReadEmail(Path.Combine(environment.HomeDirectory, ".gitconfig"))
           ?? environment.UserName;

    static string? ReadEmail(string? configFile) =>
      configFile is null ? null : NonEmpty(GitConfigFile.Read(configFile, "user", "email"))?.Trim();
  }

  // AGENTLEDGER_TAG_<key>=<value>; keys lowercased (variable names are case-sensitive on Linux only).
  private Dictionary<string, string> ReadTags()
  {
    var tags = new Dictionary<string, string>();
    foreach (var (name, value) in environment.GetVariables().OrderBy(v => v.Key, StringComparer.Ordinal))
    {
      if (name.StartsWith(TagPrefix, StringComparison.Ordinal) && name.Length > TagPrefix.Length && value.Length > 0)
      {
        tags.TryAdd(name[TagPrefix.Length..].ToLowerInvariant(), value);
      }
    }

    return tags;
  }

  private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
