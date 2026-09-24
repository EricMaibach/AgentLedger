using AgentLedger.Core.AgentEventReceiptAggregate;

namespace AgentLedger.UseCases.AgentEventReceipts.Ingest;

public sealed class IngestEventHandler(IRepository<AgentEventReceipt> repository, TimeProvider clock) : ICommandHandler<IngestEventCommand, Result<ReceiptId>>
{
  public async ValueTask<Result<ReceiptId>> Handle(IngestEventCommand command, CancellationToken cancellationToken)
  {
    AgentEventReceipt receipt;

    if (!AgentKind.TryFromName(command.Agent, out var agentKind))
    {
      return Result.Invalid(new ValidationError("agent", $"Unknown agent: {command.Agent}"));
    }

    if (!AgentEventId.TryFrom(command.EventId, out var eventId))
    {
      return Result.Invalid(new ValidationError("eventId", $"Invalid event ID: {command.EventId}"));
    }

    try
    {
      var context = new CaptureContext(command.Host, command.User, command.ProjectDir, command.GitRepo, command.GitBranch,
      command.GitWorktree);

      receipt = new AgentEventReceipt(
          ReceiptId.From(Guid.CreateVersion7()),
          eventId,
          agentKind,
          command.EventType,
          command.NativeSessionId,
          command.CapturedAt,
          clock.GetUtcNow(),
          context,
          command.Tags,
          command.Payload);
    }
    catch (ArgumentException aex)
    {
      return Result.Invalid(new ValidationError(aex.ParamName, aex.Message));
    }

    await repository.AddAsync(receipt, cancellationToken);

    return Result.Created(receipt.Id);
  }
}
