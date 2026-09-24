using AgentLedger.Core.AgentEventReceiptAggregate;

namespace AgentLedger.UseCases.AgentEventReceipts.Ingest;

public sealed record IngestEventCommand(
    Guid EventId, string Agent, string EventType, string? NativeSessionId,
    DateTimeOffset CapturedAt, string Host, string User, string ProjectDir,
    string? GitRepo, string? GitBranch, string? GitWorktree, IReadOnlyDictionary<string, string> Tags,
    string Payload) : ICommand<Result<ReceiptId>>;
