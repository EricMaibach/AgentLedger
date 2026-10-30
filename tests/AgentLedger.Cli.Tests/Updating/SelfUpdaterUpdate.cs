using System.Net;
using System.Security.Cryptography;
using System.Text;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Setup;
using AgentLedger.Cli.Tests.Login;
using AgentLedger.Cli.Updating;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.Cli.Tests.Updating;

// The installed CLI follows the ledger's version (ADR 0024): after a hook's work, at most once an hour, checksum-checked,
// replacing its own file atomically, then refreshing the hooks. Never anything but the binary `setup` installed.
public sealed class SelfUpdaterUpdate : IDisposable
{
  private static readonly byte[] NewBinary = Encoding.UTF8.GetBytes("the ledger's CLI");
  private const string OtherVersion = "99.0.0+abc";

  private readonly TempDirectory _temp = new();
  private readonly FakeHostEnvironment _env = new();
  private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero));
  private readonly FakeLog _log = new();
  private readonly List<string> _refreshed = [];
  private readonly string _binary;
  private string? _checksum;
  private int _requests;

  public SelfUpdaterUpdate()
  {
    _env.LocalDataDirectory = _temp.CreateDirectory("data");
    _env.UserConfigDirectory = _temp.CreateDirectory("config");
    _env.Variables["AGENTLEDGER_URL"] = "http://ledger.test:58090";
    _binary = _temp.Write("bin/agentledger", "the old CLI");
    _env.ProcessPath = _binary;
    InstalledBinary.Record(_env).ShouldBeTrue();
  }

  public void Dispose() => _temp.Dispose();

  private StubHttp Ledger() => new(request =>
  {
    _requests++;
    var path = request.RequestUri!.AbsolutePath;
    return path.EndsWith(".sha256", StringComparison.Ordinal)
      ? new HttpResponseMessage(HttpStatusCode.OK)
      { Content = new StringContent($"{_checksum ?? Convert.ToHexStringLower(SHA256.HashData(NewBinary))}  agentledger\n") }
      : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(NewBinary) };
  });

  private Task<UpdateOutcome> UpdateAsync(string? ledgerVersion, StubHttp? http = null) =>
    new SelfUpdater(_env, (http ?? Ledger()).Client, _clock, _log, path => { _refreshed.Add(path); return Task.CompletedTask; })
      .MaybeUpdateAsync(new SettingsLoader(_env).Load(_temp.Path), ledgerVersion, CancellationToken.None);

  [Fact]
  public async Task ReplacesItselfWithTheLedgersVersion()
  {
    (await UpdateAsync(OtherVersion)).ShouldBe(UpdateOutcome.Updated);

    File.ReadAllBytes(_binary).ShouldBe(NewBinary);
    if (!OperatingSystem.IsWindows())
      File.GetUnixFileMode(_binary).HasFlag(UnixFileMode.UserExecute).ShouldBeTrue();
    _refreshed.ShouldBe([_binary]);
    _log.Lines.ShouldContain(l => l.Contains("Updated") && l.Contains("99.0.0"));
    Directory.GetFiles(Path.GetDirectoryName(_binary)!).ShouldBe([_binary]); // no leftovers
  }

  [Fact]
  public async Task AsksForThisPlatformsBinary()
  {
    var http = Ledger();

    await UpdateAsync(OtherVersion, http);

    http.Request!.RequestUri!.AbsolutePath.ShouldStartWith($"/cli/{CliPlatform.Current}");
  }

  [Theory]
  [InlineData(null)]
  [InlineData("same")]
  public async Task DoesNothingWhenTheVersionsMatchOrAreUnknown(string? ledgerVersion)
  {
    var version = ledgerVersion == "same" ? CliVersion.Release(CliVersion.Current) + "+another-build" : null;

    (await UpdateAsync(version)).ShouldBe(UpdateOutcome.NotNeeded);
    _requests.ShouldBe(0);
  }

  [Fact]
  public async Task DoesNothingWhenTurnedOff()
  {
    _env.Variables["AGENTLEDGER_AUTO_UPDATE"] = "false";

    (await UpdateAsync(OtherVersion)).ShouldBe(UpdateOutcome.TurnedOff);
    File.ReadAllText(_binary).ShouldBe("the old CLI");
  }

  [Fact]
  public async Task NeverReplacesABinaryItDidNotInstall()
  {
    // E.g. a CLI built from source, like this repository's dev container builds.
    _env.ProcessPath = _temp.Write("src/bin/agentledger", "built from source");

    (await UpdateAsync(OtherVersion)).ShouldBe(UpdateOutcome.NotInstalled);
    _requests.ShouldBe(0);
  }

  [Fact]
  public async Task TriesAtMostOnceAnHour()
  {
    (await UpdateAsync(OtherVersion, StubHttp.Unreachable())).ShouldBe(UpdateOutcome.Failed);

    _clock.Advance(TimeSpan.FromMinutes(59));
    (await UpdateAsync(OtherVersion)).ShouldBe(UpdateOutcome.TooSoon);

    _clock.Advance(TimeSpan.FromMinutes(1));
    (await UpdateAsync(OtherVersion)).ShouldBe(UpdateOutcome.Updated);
  }

  [Fact]
  public async Task RefusesADownloadThatDoesNotMatchItsChecksum()
  {
    _checksum = new string('0', 64);

    (await UpdateAsync(OtherVersion)).ShouldBe(UpdateOutcome.Failed);

    File.ReadAllText(_binary).ShouldBe("the old CLI");
    Directory.GetFiles(Path.GetDirectoryName(_binary)!).ShouldBe([_binary]);
    _log.Lines.ShouldContain(l => l.StartsWith("ERROR") && l.Contains("checksum"));
    _refreshed.ShouldBeEmpty();
  }
}
