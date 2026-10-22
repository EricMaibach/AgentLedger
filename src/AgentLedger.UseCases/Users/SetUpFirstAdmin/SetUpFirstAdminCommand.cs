namespace AgentLedger.UseCases.Users.SetUpFirstAdmin;

public sealed record SetUpFirstAdminCommand(string Username, string Password) : ICommand<Result<SignedInUser>>;
