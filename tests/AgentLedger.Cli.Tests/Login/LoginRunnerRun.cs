using System.Net;
using AgentLedger.Cli.Configuration;
using AgentLedger.Cli.Login;

namespace AgentLedger.Cli.Tests.Login;

// `agentledger login`: checks a key with the ledger, then stores it (ADR 0019).
public sealed class LoginRunnerRun : IDisposable
{
  private const string IngestKey = "agl_ingest_0000000000000000000001_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
  private static readonly Uri ApiUrl = new("http://api.test:5000/");

  private readonly TempDirectory _temp = new();
  private readonly FakeHostEnvironment _env = new();
  private readonly StringWriter _output = new();

  public LoginRunnerRun() => _env.UserConfigDirectory = _temp.Path;

  public void Dispose() => _temp.Dispose();

  private static StubHttp Accepting(string scope = "ingest") =>
    StubHttp.Returning(HttpStatusCode.OK, $$"""{"keyId":"x","name":"laptop","scope":"{{scope}}","owner":"eric"}""");

  private string? SavedKey() => new SettingsLoader(_env).Load(_temp.Path).ApiKey.Value;

  [Fact]
  public async Task SavesAWorkingIngestKey()
  {
    var exitCode = await LoginRunner.RunAsync(_env, Accepting().Client, ApiUrl, IngestKey, _output);

    exitCode.ShouldBe(0);
    SavedKey().ShouldBe(IngestKey);
    _output.ToString().ShouldContain("'laptop' (ingest, owner eric)");
  }

  [Fact]
  public async Task TrimsTheKey()
  {
    await LoginRunner.RunAsync(_env, Accepting().Client, ApiUrl, $"  {IngestKey}\n", _output);

    SavedKey().ShouldBe(IngestKey);
  }

  [Fact]
  public async Task RefusesAReadKeyWithoutAskingTheLedger()
  {
    // The CLI sends events; a read key would have every event refused.
    var http = Accepting("read");

    var exitCode = await LoginRunner.RunAsync(_env, http.Client, ApiUrl, IngestKey.Replace("_ingest_", "_read_"), _output);

    exitCode.ShouldBe(1);
    SavedKey().ShouldBeNull();
    http.Request.ShouldBeNull();
    _output.ToString().ShouldContain("ingest key");
  }

  [Theory]
  [InlineData("")]
  [InlineData("ghp_somethingelse")]
  public async Task RefusesSomethingThatIsNotAKey(string key)
  {
    var exitCode = await LoginRunner.RunAsync(_env, Accepting().Client, ApiUrl, key, _output);

    exitCode.ShouldBe(2);
    SavedKey().ShouldBeNull();
  }

  [Fact]
  public async Task DoesNotSaveAKeyTheLedgerRejects()
  {
    var exitCode = await LoginRunner.RunAsync(_env, StubHttp.Returning(HttpStatusCode.Unauthorized).Client, ApiUrl, IngestKey, _output);

    exitCode.ShouldBe(1);
    SavedKey().ShouldBeNull();
    _output.ToString().ShouldContain("rejected");
  }

  [Fact]
  public async Task DoesNotSaveWhenTheLedgerCannotBeReached()
  {
    var exitCode = await LoginRunner.RunAsync(_env, StubHttp.Unreachable().Client, ApiUrl, IngestKey, _output);

    exitCode.ShouldBe(1);
    SavedKey().ShouldBeNull();
    _output.ToString().ShouldContain("http://api.test:5000/");
  }

  [Fact]
  public async Task WarnsWhenTheEnvironmentVariableWins()
  {
    _env.Variables["AGENTLEDGER_API_KEY"] = "agl_ingest_other_key";

    await LoginRunner.RunAsync(_env, Accepting().Client, ApiUrl, IngestKey, _output);

    _output.ToString().ShouldContain("AGENTLEDGER_API_KEY");
  }
}
