using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.Projections;
using AgentLedger.Core.TranscriptAggregate;

namespace AgentLedger.UseCases.Projections;

// Interprets one agent's raw evidence into the agent-neutral projection model (ADR 0003, ADR 0022): the
// anti-corruption layer between a vendor's payloads and everything else. Pure: no I/O, the same evidence always gives
// the same session.
public interface IAgentAdapter
{
  AgentKind Agent { get; }

  ProjectedSession Project(SessionEvidence evidence);
}

// Everything the ledger holds for one session: its hook event receipts (duplicates included, ADR 0010) and its
// transcript lines (every file, in file and byte order).
public sealed record SessionEvidence(
  AgentKind Agent,
  string NativeSessionId,
  IReadOnlyList<AgentEventReceipt> Receipts,
  IReadOnlyList<TranscriptLine> Lines);
