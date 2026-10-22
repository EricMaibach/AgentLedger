using AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;

namespace AgentLedger.Web.AgentEventReceipts;

// Turns the filter query parameters shared by the raw event endpoints into a RawEventFilter.
// The agent arrives as its kebab-case wire name (ADR 0011) and is converted to the domain name;
// whether that agent exists is checked by the use case.
internal static class RawEventFilterParameters
{
  public static Result<RawEventFilter> ToFilter(
    string? agent, string? eventType, string? session, string? host, string? repo, DateTimeOffset? from, DateTimeOffset? to)
  {
    string? agentKindName = null;
    if (agent is not null && !AgentWireName.TryToAgentKindName(agent, out agentKindName))
    {
      return Result.Invalid(new ValidationError("agent", $"Agent must be a kebab-case name such as 'claude-code', but was '{agent}'."));
    }

    return new RawEventFilter(agentKindName, eventType, session, host, repo, from, to);
  }
}
