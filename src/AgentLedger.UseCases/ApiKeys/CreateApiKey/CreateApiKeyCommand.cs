namespace AgentLedger.UseCases.ApiKeys.CreateApiKey;

public sealed record CreateApiKeyCommand(Guid OwnerId, string Name, string Scope, int? ExpiresInDays) : ICommand<Result<CreatedApiKey>>;

// The token is only ever available here, once (ADR 0019).
public sealed record CreatedApiKey(Guid KeyId, string Token);
