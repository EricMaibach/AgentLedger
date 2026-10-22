using AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;
using AgentLedger.Web.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentLedger.Web.AgentEventReceipts;

/// <summary>Query parameters for GET /api/raw/events/counts: the same filters as the list.</summary>
public sealed class CountRawEventTypesRequest
{
  public const string Route = "/api/raw/events/counts";

  public string? Agent { get; set; }
  public string? EventType { get; set; }
  public string? Session { get; set; }
  public string? Host { get; set; }
  public string? Repo { get; set; }
  public DateTimeOffset? From { get; set; }
  public DateTimeOffset? To { get; set; }
}

/// <summary>Counts matching events by event type, e.g. to check a session captured every kind of event.</summary>
public sealed class CountRawEventTypes(IMediator mediator)
  : Endpoint<CountRawEventTypesRequest, Results<Ok<IReadOnlyList<EventTypeCount>>, ValidationProblem, ProblemHttpResult>>
{
  public override void Configure()
  {
    Get(CountRawEventTypesRequest.Route);
    AllowAnonymous(); // until login (roadmap, Phase 2)
    Summary(s =>
    {
      s.Summary = "Count events by type";
      s.Description = "Counts events, not receipts: resends don't count twice. Most frequent first.";
      s.Responses[200] = "Counts per event type";
      s.Responses[400] = "Invalid parameters; errors are keyed by parameter";
    });
  }

  public override async Task<Results<Ok<IReadOnlyList<EventTypeCount>>, ValidationProblem, ProblemHttpResult>>
    ExecuteAsync(CountRawEventTypesRequest request, CancellationToken cancellationToken)
  {
    var filter = RawEventFilterParameters.ToFilter(
      request.Agent, request.EventType, request.Session, request.Host, request.Repo, request.From, request.To);
    if (!filter.IsSuccess)
    {
      return Result<IReadOnlyList<EventTypeCount>>.Invalid(filter.ValidationErrors).ToOkResult(counts => counts);
    }

    var result = await mediator.Send(new CountRawEventTypesQuery(filter.Value), cancellationToken);
    return result.ToOkResult(counts => counts);
  }
}
