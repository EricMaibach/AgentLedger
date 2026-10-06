using AgentLedger.UseCases.AgentEventReceipts.GetRawEvent;
using AgentLedger.Web.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentLedger.Web.AgentEventReceipts;

/// <summary>
/// Retrieve a raw event with all its receipts
/// </summary>
/// <param name="mediator"></param>
public sealed class GetRawEvent(IMediator mediator)
  : Endpoint<GetRawEventRequest, Results<Ok<RawEventDetail>, NotFound, ValidationProblem, ProblemHttpResult>>
{
  public override void Configure()
  {
    Get(GetRawEventRequest.Route);
    AllowAnonymous();
    Summary(s =>
    {
      s.Summary = "Get one raw event with all its receipts";
      s.Description = "The envelope comes from the events's first receipt, the payload is the exact stored test.";
      s.Responses[200] = "The event";
      s.Responses[400] = "Invalid event Id: errors are keyed by field";
      s.Responses[404] = "No event with this Id";
    });
  }

  public override async Task<Results<Ok<RawEventDetail>, NotFound, ValidationProblem, ProblemHttpResult>> ExecuteAsync(GetRawEventRequest request, CancellationToken cancellationToken)
  {
    var query = new GetRawEventQuery(request.EventId);

    var result = await mediator.Send(query, cancellationToken);
    return result.ToGetByIdResult(detail => detail with { Agent = AgentWireName.FromAgentKindName(detail.Agent) });
  }
}
