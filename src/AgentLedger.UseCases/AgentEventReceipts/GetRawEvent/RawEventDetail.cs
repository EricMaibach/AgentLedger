using AgentLedger.Core.AgentEventReceiptAggregate;

namespace AgentLedger.UseCases.AgentEventReceipts.GetRawEvent;

public sealed record RawEventReceipt(Guid ReceiptId, DateTimeOffset ReceivedAt);

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
  public static RawEventDetail From(IReadOnlyList<AgentEventReceipt> receipts)
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
      Receipts: receipts.Select(r => new RawEventReceipt(r.Id.Value, r.ReceivedAt)).ToList()
    );
  }
}
