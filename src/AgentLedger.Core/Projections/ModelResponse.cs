namespace AgentLedger.Core.Projections;

// Token counts of one model response, by kind (ADR 0022). Thinking is part of output, reported separately.
public sealed record TokenUsage(
  long Input,
  long Output,
  long CacheWrite5m,
  long CacheWrite1h,
  long CacheRead,
  long Thinking,
  long WebSearches,
  long WebFetches)
{
  public long Input { get; private init; } = Guard.Against.Negative(Input, "input");
  public long Output { get; private init; } = Guard.Against.Negative(Output, "output");
  public long CacheWrite5m { get; private init; } = Guard.Against.Negative(CacheWrite5m, "cacheWrite5m");
  public long CacheWrite1h { get; private init; } = Guard.Against.Negative(CacheWrite1h, "cacheWrite1h");
  public long CacheRead { get; private init; } = Guard.Against.Negative(CacheRead, "cacheRead");
  public long Thinking { get; private init; } = Guard.Against.Negative(Thinking, "thinking");
  public long WebSearches { get; private init; } = Guard.Against.Negative(WebSearches, "webSearches");
  public long WebFetches { get; private init; } = Guard.Against.Negative(WebFetches, "webFetches");
}

// One model response (API call): its model and tokens, counted once however many steps it produced.
public sealed record ModelResponse
{
  public ModelResponseId Id { get; }
  public string MessageId { get; }
  public string Model { get; }
  public DateTimeOffset RespondedAt { get; }
  public TokenUsage Usage { get; }
  public Lineage Lineage { get; }

  public ModelResponse(ModelResponseId id, string messageId, string model, DateTimeOffset respondedAt, TokenUsage usage,
    Lineage lineage)
  {
    Id = id;
    MessageId = Guard.Against.NullOrWhiteSpace(messageId);
    Model = Guard.Against.NullOrWhiteSpace(model);
    RespondedAt = Guard.Against.Default(respondedAt);
    Usage = Guard.Against.Null(usage);
    Lineage = Guard.Against.Null(lineage);
  }
}
