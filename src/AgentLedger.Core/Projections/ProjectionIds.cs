using AgentLedger.Core.AgentEventReceiptAggregate;
using Vogen;

namespace AgentLedger.Core.Projections;

// Projected rows get IDs derived from the raw identifiers they come from (ADR 0022), so a rebuild produces the same
// IDs, and links, bookmarks and IDs handed to agents survive it. Never change these derivations.

[ValueObject<Guid>]
public readonly partial struct SessionId
{
  public static SessionId For(AgentKind agent, string nativeSessionId) =>
    From(ContentDerivedId.Derive("session", agent.Name, Guard.Against.NullOrWhiteSpace(nativeSessionId)));

  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("SessionId cannot be empty.") : Validation.Ok;
}

// A turn is one prompt: its agent's own prompt ID within the session.
[ValueObject<Guid>]
public readonly partial struct TurnId
{
  public static TurnId For(AgentKind agent, string nativeSessionId, string promptId) =>
    From(ContentDerivedId.Derive("turn", agent.Name, Guard.Against.NullOrWhiteSpace(nativeSessionId),
      Guard.Against.NullOrWhiteSpace(promptId)));

  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("TurnId cannot be empty.") : Validation.Ok;
}

// The native key is chosen by the agent's adapter: whatever identifies the step in that agent's raw data
// (a tool call's ID, a transcript line and block position, an event ID).
[ValueObject<Guid>]
public readonly partial struct StepId
{
  public static StepId For(AgentKind agent, string nativeSessionId, StepKind kind, string nativeKey) =>
    From(ContentDerivedId.Derive("step", agent.Name, Guard.Against.NullOrWhiteSpace(nativeSessionId), kind.Name,
      Guard.Against.NullOrWhiteSpace(nativeKey)));

  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("StepId cannot be empty.") : Validation.Ok;
}

// One model response (an API call), by the agent's own message ID.
[ValueObject<Guid>]
public readonly partial struct ModelResponseId
{
  public static ModelResponseId For(AgentKind agent, string nativeSessionId, string messageId) =>
    From(ContentDerivedId.Derive("model-response", agent.Name, Guard.Against.NullOrWhiteSpace(nativeSessionId),
      Guard.Against.NullOrWhiteSpace(messageId)));

  private static Validation Validate(Guid value) =>
    value == Guid.Empty ? Validation.Invalid("ModelResponseId cannot be empty.") : Validation.Ok;
}
