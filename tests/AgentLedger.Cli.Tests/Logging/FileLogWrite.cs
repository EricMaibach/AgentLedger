using AgentLedger.Cli.Logging;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.Cli.Tests.Logging;

public sealed class FileLogWrite : IDisposable
{
  private readonly TempDirectory _dir = new();
  private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero));

  public void Dispose() => _dir.Dispose();

  private string LogPath => Path.Combine(_dir.Path, "logs", "agentledger.log");

  [Fact]
  public void AppendsTimestampedLinesAndCreatesTheFolder()
  {
    var log = new FileLog(LogPath, _clock);

    log.Info("first");
    log.Error("second");

    File.ReadAllLines(LogPath).ShouldBe([
      "2026-09-25T10:00:00.0000000+00:00 INFO  first",
      "2026-09-25T10:00:00.0000000+00:00 ERROR second"]);
  }

  [Fact]
  public void StartsANewFileWhenTheLogGrowsTooLarge()
  {
    var log = new FileLog(LogPath, _clock, maxBytes: 100);

    for (var i = 0; i < 5; i++)
    {
      log.Info($"line {i} padded to make the file grow");
    }

    File.Exists(LogPath + ".1").ShouldBeTrue();
    new FileInfo(LogPath).Length.ShouldBeLessThanOrEqualTo(100);
  }

  [Fact]
  public void NeverThrowsWhenItCannotWrite()
  {
    // Logging failures must never break the hook. A file where the folder should be makes writing impossible.
    var blocked = _dir.Write("blocked", "not a folder");
    var log = new FileLog(Path.Combine(blocked, "agentledger.log"), _clock);

    Should.NotThrow(() => log.Error("lost"));
  }

  [Fact]
  public void ReadsTheLastLines()
  {
    var log = new FileLog(LogPath, _clock);
    for (var i = 1; i <= 5; i++)
    {
      log.Info($"line {i}");
    }

    log.ReadLast(2).Select(l => l[^6..]).ShouldBe(["line 4", "line 5"]);
  }
}
