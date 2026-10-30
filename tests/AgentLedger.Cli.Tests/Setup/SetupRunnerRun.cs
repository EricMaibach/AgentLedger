using System.Net;
using System.Text.Json;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Setup;
using AgentLedger.Cli.Tests.Login;

namespace AgentLedger.Cli.Tests.Setup;

// `agentledger setup [--url] [--code]` (ADR 0024): what the installers run. It saves the ledger's address, exchanges a
// one-time code for the machine's own key, records the installed binary and installs the hooks.
public sealed class SetupRunnerRun : IDisposable
{
  private const string Url = "http://host.docker.internal:58090";
  private const string Key = "agl_ingest_0000000000000000000001_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

  private readonly TempDirectory _temp = new();
  private readonly FakeHostEnvironment _env = new();
  private readonly StringWriter _output = new();
  private readonly string _binary;

  public SetupRunnerRun()
  {
    _env.UserConfigDirectory = _temp.CreateDirectory("config");
    _env.LocalDataDirectory = _temp.CreateDirectory("data");
    _env.HomeDirectory = _temp.CreateDirectory("home");
    _env.CurrentDirectory = _temp.CreateDirectory("work");
    _env.MachineName = "devbox";
    _binary = _temp.Write("home/.local/bin/agentledger", "");
    _env.ProcessPath = _binary;
    _env.Variables["PATH"] = Path.GetDirectoryName(_binary)!;
  }

  public void Dispose() => _temp.Dispose();

  private static StubHttp Enrolling() =>
    StubHttp.Returning(HttpStatusCode.OK, $$"""{"keyId":"019a0000-0000-7000-8000-000000000001","keyName":"devbox (connected 2026-10-30)","apiKey":"{{Key}}"}""");

  private Task<int> RunAsync(StubHttp http, string? url = Url, string? code = null) =>
    SetupRunner.RunAsync(_env, http.Client, url, code, _output);

  private CliSettings Settings => new SettingsLoader(_env).Load(_env.CurrentDirectory);

  private string UserHooks => File.ReadAllText(Path.Combine(_env.HomeDirectory, ".claude", "settings.json"));

  [Fact]
  public async Task SavesTheLedgersAddress()
  {
    (await RunAsync(Enrolling())).ShouldBe(0);

    Settings.Url.Value.ShouldBe(new Uri(Url));
    Settings.Url.Source.ShouldStartWith("user");
  }

  [Theory]
  [InlineData("host.docker.internal:58090")]
  [InlineData("ftp://ledger")]
  public async Task RefusesAnAddressThatIsNotAWebAddress(string url)
  {
    (await RunAsync(Enrolling(), url)).ShouldBe(2);

    Settings.Url.Source.ShouldBe("default");
  }

  [Fact]
  public async Task ExchangesTheCodeForTheMachinesKey()
  {
    var http = Enrolling();

    (await RunAsync(http, code: "K7PX-4QHM")).ShouldBe(0);

    Settings.ApiKey.Value.ShouldBe(Key);
    http.Request!.RequestUri.ShouldBe(new Uri($"{Url}/enroll"));
    var body = JsonDocument.Parse(await http.Request.Content!.ReadAsStringAsync()).RootElement;
    (body.GetProperty("code").GetString(), body.GetProperty("host").GetString()).ShouldBe(("K7PX-4QHM", "devbox"));
    _output.ToString().ShouldContain("devbox (connected 2026-10-30)");
  }

  [Fact]
  public async Task ShowsWhyTheLedgerRefusedTheCode()
  {
    var http = StubHttp.Returning(HttpStatusCode.BadRequest,
      """{"title":"One or more validation errors occurred.","status":400,"errors":{"code":["This code isn't valid: it may have been used or have expired. Create a new one."]}}""");

    (await RunAsync(http, code: "K7PX-4QHM")).ShouldBe(1);

    Settings.ApiKey.Value.ShouldBeNull();
    _output.ToString().ShouldContain("This code isn't valid");
  }

  [Fact]
  public async Task SaysWhenTheLedgerIsUnreachable()
  {
    (await RunAsync(StubHttp.Unreachable(), code: "K7PX-4QHM")).ShouldBe(1);

    _output.ToString().ShouldContain("Couldn't reach the ledger");
  }

  [Fact]
  public async Task KeepsTheMachinesKeyWithoutACode()
  {
    // Running the installer again to update: the connection stays.
    UserConfigFile.SetApiKey(_env, Key);

    (await RunAsync(Enrolling())).ShouldBe(0);

    Settings.ApiKey.Value.ShouldBe(Key);
    _output.ToString().ShouldNotContain("Not connected");
  }

  [Fact]
  public async Task SaysHowToConnectWithoutACodeOrKey()
  {
    (await RunAsync(Enrolling())).ShouldBe(0);

    _output.ToString().ShouldContain("Not connected yet");
    _output.ToString().ShouldContain("agentledger setup --code");
  }

  [Fact]
  public async Task RecordsTheInstalledBinary()
  {
    // Self-update replaces only this file (ADR 0024).
    await RunAsync(Enrolling());

    InstalledBinary.Read(_env).ShouldBe(_binary);
  }

  [Fact]
  public async Task DoesNotRecordADevelopmentRun()
  {
    _env.ProcessPath = "/usr/share/dotnet/dotnet";

    await RunAsync(Enrolling());

    InstalledBinary.Read(_env).ShouldBeNull();
  }

  [Fact]
  public async Task InstallsTheClaudeCodeHooks()
  {
    await RunAsync(Enrolling());

    UserHooks.ShouldContain("\"agentledger hook claude-code\"");
  }

  [Fact]
  public async Task UsesTheFullPathWhenTheBinaryIsNotOnPath()
  {
    // Agents' hooks may not get the shell's PATH: with ~/.local/bin missing from it, a bare name would fail.
    _env.Variables["PATH"] = "/usr/bin";

    await RunAsync(Enrolling());

    UserHooks.ShouldContain(JsonEncodedText.Encode($"{_binary} hook claude-code").ToString());
  }
}
