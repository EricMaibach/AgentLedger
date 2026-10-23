namespace AgentLedger.UseCases.ApiKeys.RevokeApiKey;

public sealed record RevokeApiKeyCommand(Guid ActorId, Guid KeyId) : ICommand<Result>;
