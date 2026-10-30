using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace AgentLedger.Web.Cli;

/// <summary>
/// The CLI binaries this ledger serves (ADR 0024): one per platform under <c>Cli:Directory</c>, by default
/// <c>cli-binaries/&lt;platform&gt;/</c> next to the app, where the release build puts them (the project publishes its
/// <c>cli-binaries</c> folder). None when running from source.
/// </summary>
public sealed class CliBinaries(IConfiguration configuration)
{
  // Platforms by their .NET runtime identifier, with the binary's file name.
  public static readonly IReadOnlyDictionary<string, string> Platforms = new Dictionary<string, string>
  {
    ["linux-x64"] = "agentledger",
    ["linux-arm64"] = "agentledger",
    ["osx-arm64"] = "agentledger",
    ["osx-x64"] = "agentledger",
    ["win-x64"] = "agentledger.exe",
  };

  private readonly string _directory = configuration["Cli:Directory"] is { Length: > 0 } configured
    ? configured
    : Path.Combine(AppContext.BaseDirectory, "cli-binaries");

  private readonly ConcurrentDictionary<(string Path, DateTime Written), string> _checksums = new();

  /// <summary>The binary for a known platform, if this ledger has it.</summary>
  public FileInfo? Find(string platform) =>
    Platforms.TryGetValue(platform, out var name) && new FileInfo(Path.Combine(_directory, platform, name)) is { Exists: true } file
      ? file
      : null;

  /// <summary>SHA-256 in lowercase hex, computed once per file version.</summary>
  public string Checksum(FileInfo binary) =>
    _checksums.GetOrAdd((binary.FullName, binary.LastWriteTimeUtc), key =>
    {
      using var stream = File.OpenRead(key.Path);
      return Convert.ToHexStringLower(SHA256.HashData(stream));
    });
}
