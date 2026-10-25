using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;

namespace AgentLedger.Core.TranscriptAggregate;

// One line of an agent's transcript file, exactly as read (ADR 0020): raw truth, interpreted only by projections.
// Stored once: its ID comes from its content, so a repeat is the same line.
public sealed class TranscriptLine : EntityBase<TranscriptLine, TranscriptLineId>, IAggregateRoot
{
  public AgentKind Agent { get; private set; }
  public string NativeSessionId { get; private set; }
  public TranscriptFileName File { get; private set; }
  public string? SubagentId { get; private set; }
  public long ByteOffset { get; private set; }

  // Exactly the file's bytes (as text), valid JSON or not.
  public string Text { get; private set; }

  public ApiKeyId ApiKeyId { get; private set; }
  public DateTimeOffset ReceivedAt { get; private set; }

  public TranscriptLine(AgentKind agent, string nativeSessionId, TranscriptFileName file, long byteOffset, string text,
    ApiKeyId apiKeyId, DateTimeOffset receivedAt)
  {
    Agent = Guard.Against.Null(agent);
    NativeSessionId = Guard.Against.NullOrWhiteSpace(nativeSessionId);
    File = file;
    SubagentId = file.SubagentId;
    ByteOffset = Guard.Against.Negative(byteOffset);
    Text = Guard.Against.Null(text);
    ApiKeyId = apiKeyId;
    ReceivedAt = Guard.Against.Default(receivedAt);
    Id = TranscriptLineId.For(Agent, NativeSessionId, File, ByteOffset, Text);
  }

#pragma warning disable CS8618 // Required by EF Core: properties are set when a row is loaded
  private TranscriptLine() { }
#pragma warning restore CS8618
}

// A file within a session's transcript: the main one, or a subagent's with its side file's meta, stored raw (ADR 0020).
public sealed class TranscriptFile : EntityBase<TranscriptFile, TranscriptFileId>, IAggregateRoot
{
  public AgentKind Agent { get; private set; }
  public string NativeSessionId { get; private set; }
  public TranscriptFileName File { get; private set; }
  public string? SubagentId { get; private set; }

  // The subagent's .meta.json exactly as read (its type, the tool call that launched it, its depth); none for main.
  public string? Meta { get; private set; }

  public ApiKeyId ApiKeyId { get; private set; }
  public DateTimeOffset FirstSeenAt { get; private set; }

  public TranscriptFile(AgentKind agent, string nativeSessionId, TranscriptFileName file, string? meta, ApiKeyId apiKeyId,
    DateTimeOffset firstSeenAt)
  {
    Agent = Guard.Against.Null(agent);
    NativeSessionId = Guard.Against.NullOrWhiteSpace(nativeSessionId);
    File = file;
    SubagentId = file.SubagentId;
    Meta = meta;
    ApiKeyId = apiKeyId;
    FirstSeenAt = Guard.Against.Default(firstSeenAt);
    Id = TranscriptFileId.For(Agent, NativeSessionId, File);
  }

#pragma warning disable CS8618 // Required by EF Core: properties are set when a row is loaded
  private TranscriptFile() { }
#pragma warning restore CS8618
}
