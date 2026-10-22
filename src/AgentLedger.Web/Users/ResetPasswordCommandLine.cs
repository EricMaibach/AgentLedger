using AgentLedger.UseCases.Users.ResetPassword;

namespace AgentLedger.Web.Users;

/// <summary>
/// <c>dotnet AgentLedger.Web.dll reset-password &lt;username&gt;</c>: the way back in for a forgotten password
/// (ADR 0018). Run inside the container, so only someone with Docker access on the host can use it.
/// </summary>
public static class ResetPasswordCommandLine
{
  private const string Name = "reset-password";

  /// <summary>Whether the app was started to run this command instead of serving requests.</summary>
  public static bool IsInvokedBy(string[] args) => args is [Name, ..];

  /// <summary>Resets the password and prints the new one. Returns the process exit code.</summary>
  public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output)
  {
    if (args is not [Name, var username])
    {
      await output.WriteLineAsync($"Usage: {Name} <username>");
      return 2;
    }

    using var scope = services.CreateScope();
    var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ResetPasswordCommand(username));
    if (!result.IsSuccess)
    {
      await output.WriteLineAsync(result.Errors.FirstOrDefault() ?? $"Could not reset the password for '{username}'.");
      return 1;
    }

    await output.WriteLineAsync($"New password for '{username}' (their other sessions are signed out):");
    await output.WriteLineAsync(result.Value);
    await output.WriteLineAsync("Sign in with it, then choose your own under \"Change password\".");
    return 0;
  }
}
