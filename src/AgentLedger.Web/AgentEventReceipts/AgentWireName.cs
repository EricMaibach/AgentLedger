using System.Text.RegularExpressions;

namespace AgentLedger.Web.AgentEventReceipts;

// Agents are kebab-case on the wire (matching `agentledger hook claude-code`) and PascalCase
// AgentKind names in the domain: claude-code ↔ ClaudeCode, vs-code-copilot ↔ VsCodeCopilot.
internal static partial class AgentWireName
{
  public static bool TryToAgentKindName(string wireName, out string agentKindName)
  {
    agentKindName = string.Empty;
    if (!KebabCase().IsMatch(wireName))
    {
      return false;
    }

    agentKindName = string.Concat(wireName.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    return true;
  }

  [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
  private static partial Regex KebabCase();
}
