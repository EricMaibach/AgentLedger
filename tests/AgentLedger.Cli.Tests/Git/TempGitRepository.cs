using System.Diagnostics;

namespace AgentLedger.Cli.Tests.Git;

// A real git repository in a temporary folder, for testing against git itself rather than a fake.
public sealed class TempGitRepository : IDisposable
{
  private readonly TempDirectory _dir = new();

  public TempGitRepository()
  {
    Path = System.IO.Path.Combine(_dir.Path, "repo");
    Directory.CreateDirectory(Path);
    Git("init", "-q", "-b", "main");
    Git("config", "user.email", "test@example.com");
    Git("config", "user.name", "Test");
    Git("config", "commit.gpgsign", "false");
  }

  public string Path { get; }

  // Commits a file change; returns the new commit's hash. A date makes the commit look that old.
  public string Commit(string message, string file = "file.txt", string? content = null, DateTimeOffset? date = null)
  {
    File.WriteAllText(System.IO.Path.Combine(Path, file), content ?? Guid.NewGuid().ToString());
    Git("add", "--all");
    var environment = date is { } d
      ? new Dictionary<string, string> { ["GIT_AUTHOR_DATE"] = d.ToString("O"), ["GIT_COMMITTER_DATE"] = d.ToString("O") }
      : null;
    Git(environment, "commit", "-q", "-m", message);
    return Head();
  }

  public string Head() => Git("rev-parse", "HEAD").Trim();

  public string Git(params string[] args) => Git(null, args);

  public string Git(Dictionary<string, string>? environment, params string[] args)
  {
    var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path };
    foreach (var arg in args)
    {
      start.ArgumentList.Add(arg);
    }

    foreach (var (name, value) in environment ?? [])
    {
      start.Environment[name] = value;
    }

    using var process = Process.Start(start)!;
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    return process.ExitCode == 0 ? output : throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {error}");
  }

  public void Dispose() => _dir.Dispose();
}
