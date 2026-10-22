namespace AgentLedger.UseCases.Users.SignIn;

public sealed record SignInCommand(string Username, string Password) : ICommand<Result<SignedInUser>>;
