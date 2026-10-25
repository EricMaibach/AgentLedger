using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentLedger.Cli.Transcripts;

/// <summary>A transcript file on this machine, and its session-relative name for the ledger (ADR 0020).</summary>
/// <param name="FileName"><c>main</c>, or <c>subagents/agent-&lt;id&gt;</c>.</param>
/// <param name="MetaPath">A subagent's side file (its type, launching tool call, depth), if it has one.</param>
internal sealed record TranscriptFileRef(string Path, string SessionId, string FileName, string? MetaPath);

/// <summary>
/// The agent-specific part of reading transcripts (ADR 0020, ADR 0003): which files a hook payload points to.
/// One strategy per agent; the reader, state, batches and sending are shared.
/// </summary>
internal interface ITranscriptSource
{
  IReadOnlyList<TranscriptFileRef> FilesFor(string payload);
}

internal static class TranscriptSources
{
  /// <summary>The agent's transcript source, or null for agents whose transcripts aren't read yet.</summary>
  public static ITranscriptSource? For(string agent) => agent == "claude-code" ? new ClaudeCodeTranscriptSource() : null;
}

/// <summary>
/// Claude Code (verified from real transcripts, 2026-10-25): <c>transcript_path</c> is the session's file, and its
/// subagents' files are <c>&lt;session&gt;/subagents/agent-&lt;id&gt;.jsonl</c> next to it. Every hook returns all of
/// them: a subagent's last line can be written after its own last hook has read the file (ADR 0020, revised).
/// </summary>
internal sealed partial class ClaudeCodeTranscriptSource : ITranscriptSource
{
  public IReadOnlyList<TranscriptFileRef> FilesFor(string payload)
  {
    string? session, transcript, namedSubagent;
    try
    {
      using var json = JsonDocument.Parse(payload);
      session = Text(json.RootElement, "session_id");
      transcript = Text(json.RootElement, "transcript_path");
      namedSubagent = Text(json.RootElement, "agent_transcript_path"); // SubagentStop
    }
    catch (JsonException)
    {
      return [];
    }

    if (session is null || transcript is null)
    {
      return [];
    }

    var files = new List<TranscriptFileRef> { new(transcript, session, "main", null) };
    var subagentPaths = new List<string>();
    if (Path.GetDirectoryName(transcript) is { } projectFolder
        && Path.Combine(projectFolder, session, "subagents") is var folder
        && Directory.Exists(folder))
    {
      subagentPaths.AddRange(Directory.EnumerateFiles(folder, "agent-*.jsonl"));
    }

    if (namedSubagent is not null && !subagentPaths.Contains(namedSubagent))
    {
      subagentPaths.Add(namedSubagent);
    }

    foreach (var path in subagentPaths)
    {
      if (SubagentFile().Match(Path.GetFileName(path)) is { Success: true } match)
      {
        files.Add(new TranscriptFileRef(path, session, $"subagents/agent-{match.Groups["id"].Value}", Path.ChangeExtension(path, ".meta.json")));
      }
    }

    return files;
  }

  private static string? Text(JsonElement root, string name) =>
    root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
      && value.GetString() is { Length: > 0 } text
      ? text
      : null;

  // The same rule as the ledger's file names (ADR 0020).
  [GeneratedRegex("^agent-(?<id>[A-Za-z0-9_-]{1,64})\\.jsonl$")]
  private static partial Regex SubagentFile();
}
