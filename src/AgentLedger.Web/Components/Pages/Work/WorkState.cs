using AgentLedger.UseCases.Work;

namespace AgentLedger.Web.Components.Pages.Work;

/// <summary>
/// What the work page shows (ADR 0023): the loaded session list and the selected session, turn and step with their data.
/// Handed from the prerendered response to the interactive page, so the data isn't loaded twice.
/// </summary>
public sealed record WorkState(
  IReadOnlyList<SessionListItem> Sessions,
  SessionCursor? MoreSessions,
  SessionDetail? Session,
  IReadOnlyList<TurnListItem>? Turns,
  Guid? ExpandedTurnId,
  IReadOnlyList<StepListItem>? Steps,
  StepDetail? Step)
{
  public Guid? SessionId => Session?.Session.SessionId;
}
