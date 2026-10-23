using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.UseCases.ApiKeys;

namespace AgentLedger.UseCases.AgentEventReceipts.GetRawEvent;

// SentWith: the key that delivered the receipt and its owner (ADR 0019); none for receipts from before keys.
public sealed record RawEventReceipt(Guid ReceiptId, DateTimeOffset ReceivedAt, ApiKeySender? SentWith);

public sealed record RawEventDetail(Guid EventId,
                                    string Agent,
                                    string EventType,
                                    string? NativeSessionId,
                                    DateTimeOffset CapturedAt,
                                    string Host,
                                    string User,
                                    string ProjectDir,
                                    string? GitRepo,
                                    string? GitBranch,
                                    string? GitWorktree,
                                    IReadOnlyDictionary<string, string> Tags,
                                    string Payload,
                                    IReadOnlyList<RawEventReceipt> Receipts)
{
  public static RawEventDetail From(IReadOnlyList<AgentEventReceipt> receipts, IReadOnlyDictionary<ApiKeyId, ApiKeySender> senders)
  {
    var original = receipts[0];

    return new RawEventDetail(
      EventId: original.EventId.Value,
      Agent: original.Agent.Name,
      EventType: original.EventType,
      NativeSessionId: original.NativeSessionId,
      CapturedAt: original.CapturedAt,
      Host: original.Context.Host,
      User: original.Context.User,
      ProjectDir: original.Context.ProjectDir,
      GitRepo: original.Context.GitRepo,
      GitBranch: original.Context.GitBranch,
      GitWorktree: original.Context.GitWorktree,
      Tags: original.Tags,
      Payload: original.Payload,
      Receipts: receipts
        .Select(r => new RawEventReceipt(r.Id.Value, r.ReceivedAt, r.ApiKeyId is { } key ? senders.GetValueOrDefault(key) : null))
        .ToList()
    );
  }
}
