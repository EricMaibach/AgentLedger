using AgentLedger.UseCases.AgentEventReceipts.ListRawEvents;
using AgentLedger.Web.Extensions;
using AgentLedger.Web.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentLedger.Web.AgentEventReceipts;

/// <summary>Lists raw events, newest first, with filters and keyset paging (ADR 0016).</summary>
public sealed class ListRawEvents(IMediator mediator)
  : Endpoint<ListRawEventsRequest, Results<Ok<ListRawEventsResponse>, ValidationProblem, ProblemHttpResult>>
{
  public override void Configure()
  {
    Get(ListRawEventsRequest.Route);
    Policies(AuthenticationConfig.ReadApiPolicy); // a signed-in browser or a read key (ADR 0019)
    Summary(s =>
    {
      s.Summary = "List raw events, newest first";
      s.Description = "One row per event by default (its first receipt, with the number of receipts). Page with the returned nextCursor.";
      s.Responses[200] = "A page of events";
      s.Responses[400] = "Invalid parameters; errors are keyed by parameter";
    });
  }

  public override async Task<Results<Ok<ListRawEventsResponse>, ValidationProblem, ProblemHttpResult>>
    ExecuteAsync(ListRawEventsRequest request, CancellationToken cancellationToken)
  {
    var filter = RawEventFilterParameters.ToFilter(
      request.Agent, request.EventType, request.Session, request.Host, request.Repo, request.From, request.To);
    if (!filter.IsSuccess)
    {
      return Result<RawEventPage>.Invalid(filter.ValidationErrors).ToOkResult(ToResponse);
    }

    RawEventCursor? after = null;
    if (request.After is not null && !RawEventCursorCodec.TryDecode(request.After, out after))
    {
      return Result<RawEventPage>.Invalid(new ValidationError("after", "Not a cursor returned by this API.")).ToOkResult(ToResponse);
    }

    var result = await mediator.Send(new ListRawEventsQuery(filter.Value, request.Distinct, after, request.Limit), cancellationToken);
    return result.ToOkResult(ToResponse);
  }

  private static ListRawEventsResponse ToResponse(RawEventPage page) => new(
    [.. page.Items.Select(item => item with { Agent = AgentWireName.FromAgentKindName(item.Agent) })],
    page.Next is null ? null : RawEventCursorCodec.Encode(page.Next));
}
