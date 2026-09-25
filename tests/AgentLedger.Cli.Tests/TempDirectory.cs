namespace AgentLedger.Cli.Tests;

// A throwaway folder for tests that need real files; deleted when disposed.
public sealed class TempDirectory : IDisposable
{
  public string Path { get; } = Directory.CreateTempSubdirectory("agentledger-tests-").FullName;

  public string Write(string relativePath, string content)
  {
    var fullPath = System.IO.Path.Combine(Path, relativePath);
    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
    File.WriteAllText(fullPath, content);
    return fullPath;
  }

  public string CreateDirectory(string relativePath) =>
    Directory.CreateDirectory(System.IO.Path.Combine(Path, relativePath)).FullName;

  public void Dispose() => Directory.Delete(Path, recursive: true);
}
