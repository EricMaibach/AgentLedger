namespace AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;

public sealed class CountRawEventTypesHandler(IRawEventQueryService rawEvents)
  : IQueryHandler<CountRawEventTypesQuery, Result<IReadOnlyList<EventTypeCount>>>
{
  public async ValueTask<Result<IReadOnlyList<EventTypeCount>>> Handle(CountRawEventTypesQuery query, CancellationToken cancellationToken)
  {
    var errors = RawEventFilterValidator.Validate(query.Filter);
    if (errors.Count > 0)
    {
      return Result.Invalid(errors);
    }

    return Result.Success(await rawEvents.CountEventTypesAsync(query.Filter, cancellationToken));
  }
}
