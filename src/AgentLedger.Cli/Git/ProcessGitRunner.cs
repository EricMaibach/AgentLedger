using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace AgentLedger.Cli.Git;

internal sealed record GitRunResult(int ExitCode, string Output);

/// <summary>Runs git. Behind an interface so a failing git can be simulated; tests otherwise use real git.</summary>
internal interface IGitRunner
{
  Task<GitRunResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, TimeSpan timeout);
}

/// <summary>
/// Runs git as a process (ADR 0021): only when HEAD moved, never on the normal hook path. Never throws: a missing
/// git or a timeout is a result, so the hook carries on without commit records.
/// </summary>
internal sealed class ProcessGitRunner(string executable = "git") : IGitRunner
{
  /// <summary>The exit code reported when git couldn't be started or was stopped for taking too long.</summary>
  public const int NotRun = -1;

  public async Task<GitRunResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, TimeSpan timeout)
  {
    var start = new ProcessStartInfo(executable)
    {
      WorkingDirectory = workingDirectory,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      StandardOutputEncoding = Encoding.UTF8,
    };
    foreach (var argument in arguments)
    {
      start.ArgumentList.Add(argument);
    }

    Process? process;
    try
    {
      process = Process.Start(start);
    }
    catch (Win32Exception)
    {
      return new GitRunResult(NotRun, ""); // git isn't installed
    }

    if (process is null)
    {
      return new GitRunResult(NotRun, "");
    }

    using (process)
    {
      using var cancellation = new CancellationTokenSource(timeout);
      try
      {
        var output = process.StandardOutput.ReadToEndAsync(cancellation.Token);
        var error = process.StandardError.ReadToEndAsync(cancellation.Token); // drained so git never blocks on a full pipe
        await process.WaitForExitAsync(cancellation.Token);
        await error;
        return new GitRunResult(process.ExitCode, await output);
      }
      catch (OperationCanceledException)
      {
        process.Kill(entireProcessTree: true);
        return new GitRunResult(NotRun, "");
      }
    }
  }
}
