using AgentLedger.Core.UserAggregate;

namespace AgentLedger.UseCases.ApiKeys.ListApiKeys;

// Each user sees their own keys; admins see everyone's, so they can spot and revoke an unknown key (ADR 0019).
public sealed class ListApiKeysHandler(IReadRepository<User> users, IApiKeyQueryService queries)
  : IQueryHandler<ListApiKeysQuery, Result<IReadOnlyList<ApiKeyListItem>>>
{
  public async ValueTask<Result<IReadOnlyList<ApiKeyListItem>>> Handle(ListApiKeysQuery query, CancellationToken cancellationToken)
  {
    if (!UserId.TryFrom(query.ViewerId, out var viewerId) || await users.GetByIdAsync(viewerId, cancellationToken) is not { } viewer)
    {
      return Result.NotFound();
    }

    return Result.Success(await queries.ListAsync(viewer.IsAdmin ? null : viewer.Id, cancellationToken));
  }
}
