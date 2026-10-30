using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.Enrollment.ListMachines;

// Connected machines (ADR 0024): each user sees their own; admins see everyone's, as with keys (ADR 0019).
public sealed class ListMachinesHandler(IReadRepository<User> users, IEnrollmentQueryService queries)
  : IQueryHandler<ListMachinesQuery, Result<IReadOnlyList<MachineListItem>>>
{
  public async ValueTask<Result<IReadOnlyList<MachineListItem>>> Handle(ListMachinesQuery query, CancellationToken cancellationToken)
  {
    if (!UserId.TryFrom(query.ViewerId, out var viewerId) || await users.GetByIdAsync(viewerId, cancellationToken) is not { } viewer)
    {
      return Result.NotFound();
    }

    return Result.Success(await queries.ListMachinesAsync(viewer.IsAdmin ? null : viewer.Id, cancellationToken));
  }
}
