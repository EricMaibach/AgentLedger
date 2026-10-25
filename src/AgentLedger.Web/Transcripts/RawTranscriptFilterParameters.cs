using AgentLedger.UseCases.Transcripts;
using AgentLedger.Web.AgentEventReceipts;

namespace AgentLedger.Web.Transcripts;

// Turns the raw transcripts filter parameters into a TranscriptLineFilter, as RawEventFilterParameters does for
// events: the agent arrives as its kebab-case wire name and is converted to the domain name.
internal static class RawTranscriptFilterParameters
{
  public static Result<TranscriptLineFilter> ToFilter(
    string? agent, string? lineType, string? session, string? file, DateTimeOffset? from, DateTimeOffset? to)
  {
    string? agentKindName = null;
    if (agent is not null && !AgentWireName.TryToAgentKindName(agent, out agentKindName))
    {
      return Result.Invalid(new ValidationError("agent", $"Agent must be a kebab-case name such as 'claude-code', but was '{agent}'."));
    }

    return new TranscriptLineFilter(agentKindName, lineType, session, file, from, to);
  }
}
