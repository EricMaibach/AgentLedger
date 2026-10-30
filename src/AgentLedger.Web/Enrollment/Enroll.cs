using AgentLedger.UseCases.Enrollment.Enroll;
using AgentLedger.Web.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentLedger.Web.Enrollment;

public sealed record EnrollRequest(string Code, string Host);

public sealed record EnrollResponse(Guid KeyId, string KeyName, string ApiKey);

/// <summary>
/// POST /enroll (ADR 0024): a machine being connected exchanges a one-time code for its own ingest key. Open to
/// anonymous callers, since the machine has no key yet (the code is the credential), and rate-limited per address
/// like login.
/// </summary>
public sealed class Enroll(IMediator mediator) : Endpoint<EnrollRequest, Results<Ok<EnrollResponse>, ValidationProblem, ProblemHttpResult>>
{
  public const string Path = "/enroll";

  /// <inheritdoc />
  public override void Configure()
  {
    Post(Path);
    AllowAnonymous();
    Summary(s =>
    {
      s.Summary = "Connect a machine: exchange a one-time code for an ingest key";
      s.Description = "The code comes from Settings → Machines and works once, for ten minutes. The key is returned only here.";
      s.Responses[200] = "The machine's new ingest key";
      s.Responses[400] = "The code isn't valid (unknown, used or expired), or the host name is missing";
      s.Responses[429] = "Too many attempts from this address";
    });
  }

  /// <inheritdoc />
  public override async Task<Results<Ok<EnrollResponse>, ValidationProblem, ProblemHttpResult>> ExecuteAsync(EnrollRequest request,
    CancellationToken cancellationToken)
  {
    var result = await mediator.Send(new EnrollCommand(request.Code ?? "", request.Host ?? ""), cancellationToken);
    return result.ToOkResult(machine => new EnrollResponse(machine.KeyId, machine.KeyName, machine.Token));
  }
}
