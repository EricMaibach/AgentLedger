namespace AgentLedger.Cli.Logging;

internal interface ICliLog
{
  void Info(string message);
  void Error(string message);
}

/// <summary>
/// The CLI's diagnostics. `hook` must stay silent (agents treat hook output as instructions), so this
/// file is where problems surface; `status` shows its last lines. Logging never throws.
/// </summary>
internal sealed class FileLog(string path, TimeProvider clock, long maxBytes = FileLog.DefaultMaxBytes) : ICliLog
{
  public const long DefaultMaxBytes = 1024 * 1024;

  public string Path => path;

  public void Info(string message) => Write("INFO ", message);

  public void Error(string message) => Write("ERROR", message);

  public IReadOnlyList<string> ReadLast(int count)
  {
    try
    {
      return File.Exists(path) ? File.ReadLines(path).TakeLast(count).ToList() : [];
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      return [];
    }
  }

  private void Write(string level, string message)
  {
    try
    {
      var line = $"{clock.GetUtcNow():O} {level} {message}{Environment.NewLine}";
      Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
      RotateIfFull(line.Length);

      // Append mode with shared access: several hook processes may log at the same moment.
      var options = new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write, Share = FileShare.ReadWrite | FileShare.Delete };
      if (!OperatingSystem.IsWindows())
      {
        options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
      }

      using var stream = new FileStream(path, options);
      using var writer = new StreamWriter(stream);
      writer.Write(line);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
    {
      // Nowhere left to report it; losing a log line must never break the agent's hook.
    }
  }

  // Keeps one previous file (.1) so the log can't grow without bound.
  private void RotateIfFull(int incomingBytes)
  {
    var current = new FileInfo(path);
    if (current.Exists && current.Length + incomingBytes > maxBytes)
    {
      File.Move(path, path + ".1", overwrite: true);
    }
  }
}
