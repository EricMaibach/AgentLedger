namespace AgentLedger.UseCases.ApiKeys.AuthenticateApiKey;

// A command, not a query: it records when the key was last used.
public sealed record AuthenticateApiKeyCommand(string? Token) : ICommand<Result<ApiKeyHolder>>;

// Who a valid key belongs to and what it may do.
public sealed record ApiKeyHolder(Guid KeyId, string KeyName, string Scope, Guid OwnerId, string Owner);
