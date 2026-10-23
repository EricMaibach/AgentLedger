namespace AgentLedger.UseCases.ApiKeys.ListApiKeys;

public sealed record ListApiKeysQuery(Guid ViewerId) : IQuery<Result<IReadOnlyList<ApiKeyListItem>>>;
