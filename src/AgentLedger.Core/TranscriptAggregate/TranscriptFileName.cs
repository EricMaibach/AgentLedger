using System.Text.RegularExpressions;
using Vogen;

namespace AgentLedger.Core.TranscriptAggregate;

// A file within a session, named relative to the session (ADR 0020): "main" for the session's own transcript,
// "subagents/agent-<id>" for a subagent's. Not a machine path: paths differ between machines.
[ValueObject<string>]
public readonly partial struct TranscriptFileName
{
  public const int MaxSubagentIdLength = 64;
  private const string SubagentPrefix = "subagents/agent-";

  public static readonly TranscriptFileName Main = From("main");

  // The subagent's ID, or null for the main transcript.
  public string? SubagentId => Value.StartsWith(SubagentPrefix, StringComparison.Ordinal) ? Value[SubagentPrefix.Length..] : null;

  public static TranscriptFileName ForSubagent(string subagentId) => From(SubagentPrefix + subagentId);

  private static Validation Validate(string value) =>
    value == "main" || SubagentPattern().IsMatch(value)
      ? Validation.Ok
      : Validation.Invalid($"A transcript file is 'main' or '{SubagentPrefix}<id>' (letters, digits, _ and -; up to {MaxSubagentIdLength}).");

  [GeneratedRegex("^subagents/agent-[A-Za-z0-9_-]{1,64}$")]
  private static partial Regex SubagentPattern();
}
