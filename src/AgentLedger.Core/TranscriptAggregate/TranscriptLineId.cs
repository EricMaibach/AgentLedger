using System.Globalization;
using AgentLedger.Core.AgentEventReceiptAggregate;
using Vogen;

namespace AgentLedger.Core.TranscriptAggregate;

// A transcript line's identity: agent, session, file, byte offset and a hash of the line's exact text
// (ADR 0020). A changed line at the same offset is a different line.
[ValueObject<Guid>]
public readonly partial struct TranscriptLineId
{
  public static TranscriptLineId For(AgentKind agent, string nativeSessionId, TranscriptFileName file, long offset, string text) =>
    From(ContentDerivedId.Derive("transcript-line", agent.Name, nativeSessionId, file.Value,
      offset.ToString(CultureInfo.InvariantCulture), ContentDerivedId.Sha256Hex(text)));

  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("TranscriptLineId cannot be empty.") : Validation.Ok;
}

// A transcript file's identity within a session: agent, session and file name.
[ValueObject<Guid>]
public readonly partial struct TranscriptFileId
{
  public static TranscriptFileId For(AgentKind agent, string nativeSessionId, TranscriptFileName file) =>
    From(ContentDerivedId.Derive("transcript-file", agent.Name, nativeSessionId, file.Value));

  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("TranscriptFileId cannot be empty.") : Validation.Ok;
}
