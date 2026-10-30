namespace AgentLedger.UseCases.ApiKeys.AuthenticateApiKey;

// A command, not a query: it records when the key was last used, and the CLI version that used it (ADR 0024).
public sealed record AuthenticateApiKeyCommand(string? Token, string? ClientVersion = null) : ICommand<Result<ApiKeyHolder>>;

// Who a valid key belongs to and what it may do.
public sealed record ApiKeyHolder(Guid KeyId, string KeyName, string Scope, Guid OwnerId, string Owner);
