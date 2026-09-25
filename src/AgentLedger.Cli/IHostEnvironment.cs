namespace AgentLedger.Cli;

/// <summary>The process environment, behind an interface so tests don't touch real, process-wide variables.</summary>
internal interface IHostEnvironment
{
  string? GetVariable(string name);
  IReadOnlyDictionary<string, string> GetVariables();
  string MachineName { get; }
  string UserName { get; }
  string CurrentDirectory { get; }
  string HomeDirectory { get; }

  /// <summary>Per-user config folder: <c>~/.config</c> on Linux/macOS, <c>%APPDATA%</c> on Windows.</summary>
  string UserConfigDirectory { get; }

  /// <summary>Per-user local data folder: <c>~/.local/share</c>, <c>~/Library/Application Support</c>, <c>%LOCALAPPDATA%</c>.</summary>
  string LocalDataDirectory { get; }

  /// <summary>The running executable, for `install --absolute-path`.</summary>
  string? ProcessPath { get; }
}

internal sealed class SystemHostEnvironment : IHostEnvironment
{
  public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);

  public IReadOnlyDictionary<string, string> GetVariables()
  {
    var variables = new Dictionary<string, string>();
    foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
    {
      variables[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
    }

    return variables;
  }

  public string MachineName => Environment.MachineName;
  public string UserName => Environment.UserName;
  public string CurrentDirectory => Environment.CurrentDirectory;
  public string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
  public string UserConfigDirectory => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
  public string LocalDataDirectory => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  public string? ProcessPath => Environment.ProcessPath;
}
