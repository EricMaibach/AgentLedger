namespace AgentLedger.UseCases.Users.ChangePassword;

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : ICommand<Result<SignedInUser>>;
