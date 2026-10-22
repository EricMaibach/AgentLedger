namespace AgentLedger.UseCases.Users.ResetPassword;

public sealed record ResetPasswordCommand(string Username) : ICommand<Result<string>>;
