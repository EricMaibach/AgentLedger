namespace AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;

public sealed class ListRawEventsHandler(IRawEventQueryService rawEvents)
  : IQueryHandler<ListRawEventsQuery, Result<RawEventPage>>
{
  public async ValueTask<Result<RawEventPage>> Handle(ListRawEventsQuery query, CancellationToken cancellationToken)
  {
    var errors = RawEventFilterValidator.Validate(query.Filter);
    if (query.Limit is < 1 or > Constants.MAX_PAGE_SIZE)
    {
      errors.Add(new ValidationError("limit", $"Limit must be between 1 and {Constants.MAX_PAGE_SIZE}."));
    }

    if (errors.Count > 0)
    {
      return Result.Invalid(errors);
    }

    return Result.Success(await rawEvents.ListAsync(query.Filter, query.Distinct, query.After, query.Limit, cancellationToken));
  }
}
