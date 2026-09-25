using AgentLedger.Cli.Configuration;

namespace AgentLedger.Cli.Tests.Configuration;

public sealed class SettingsLoaderLoad : IDisposable
{
  private readonly TempDirectory _dir = new();
  private readonly FakeHostEnvironment _env = new();
  private readonly string _project;
  private readonly string _userConfigFile;

  public SettingsLoaderLoad()
  {
    _env.UserConfigDirectory = _dir.CreateDirectory("config");
    _env.LocalDataDirectory = _dir.CreateDirectory("data");
    _project = _dir.CreateDirectory("repo");
    _userConfigFile = Path.Combine(_env.UserConfigDirectory, "agentledger", "config.json");
  }

  public void Dispose() => _dir.Dispose();

  private CliSettings Load(string? fromDirectory = null) => new SettingsLoader(_env).Load(fromDirectory ?? _project);

  private string WriteProjectFile(string json) => _dir.Write("repo/.agentledger.json", json);

  private void WriteUserFile(string json) => _dir.Write(Path.GetRelativePath(_dir.Path, _userConfigFile), json);

  [Fact]
  public void UsesDefaultsWhenNothingIsConfigured()
  {
    var settings = Load();

    settings.Url.ShouldBe(new Setting<Uri>(new Uri("http://localhost:57678"), "default"));
    settings.ApiKey.Value.ShouldBeNull();
    settings.Timeout.ShouldBe(new Setting<TimeSpan>(TimeSpan.FromSeconds(1), "default"));
    settings.SpoolDirectory.Value.ShouldBe(Path.Combine(_env.LocalDataDirectory, "agentledger", "spool"));
    settings.Enabled.ShouldBe(new Setting<bool>(true, "default"));
    settings.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void PrefersTheEnvironmentOverBothFiles()
  {
    _env.Variables["AGENTLEDGER_URL"] = "http://from-env:1";
    WriteProjectFile("""{ "url": "http://from-project:2" }""");
    WriteUserFile("""{ "url": "http://from-user:3" }""");

    Load().Url.ShouldBe(new Setting<Uri>(new Uri("http://from-env:1"), "environment (AGENTLEDGER_URL)"));
  }

  [Fact]
  public void PrefersTheProjectFileOverTheUserFile()
  {
    var projectFile = WriteProjectFile("""{ "url": "http://from-project:2" }""");
    WriteUserFile("""{ "url": "http://from-user:3" }""");

    Load().Url.ShouldBe(new Setting<Uri>(new Uri("http://from-project:2"), $"project ({projectFile})"));
  }

  [Fact]
  public void FindsTheProjectFileFromASubdirectory()
  {
    WriteProjectFile("""{ "url": "http://from-project:2" }""");
    var nested = _dir.CreateDirectory("repo/src/deep");

    Load(nested).Url.Value.ShouldBe(new Uri("http://from-project:2"));
  }

  [Fact]
  public void UsesTheUserFileWhenThereIsNoProjectFile()
  {
    WriteUserFile("""{ "url": "http://from-user:3" }""");

    Load().Url.ShouldBe(new Setting<Uri>(new Uri("http://from-user:3"), $"user ({_userConfigFile})"));
  }

  [Fact]
  public void ResolvesEachSettingSeparately()
  {
    WriteProjectFile("""{ "url": "http://from-project:2" }""");
    WriteUserFile("""{ "timeoutMs": 2500 }""");

    var settings = Load();

    settings.Url.Value.ShouldBe(new Uri("http://from-project:2"));
    settings.Timeout.ShouldBe(new Setting<TimeSpan>(TimeSpan.FromMilliseconds(2500), $"user ({_userConfigFile})"));
  }

  [Fact]
  public void NeverReadsTheApiKeyFromTheProjectFile()
  {
    // The project file is committed to git; a key in it would be published (ADR 0012).
    WriteProjectFile("""{ "apiKey": "leaked-key" }""");
    WriteUserFile("""{ "apiKey": "user-key" }""");

    var settings = Load();

    settings.ApiKey.Value.ShouldBe("user-key");
    settings.Warnings.ShouldContain(w => w.Contains("apiKey") && w.Contains(".agentledger.json"));
  }

  [Fact]
  public void ReadsTheApiKeyFromTheEnvironment()
  {
    _env.Variables["AGENTLEDGER_API_KEY"] = "env-key";

    Load().ApiKey.ShouldBe(new Setting<string?>("env-key", "environment (AGENTLEDGER_API_KEY)"));
  }

  [Fact]
  public void ReadsTheSpoolDirectoryOverride()
  {
    _env.Variables["AGENTLEDGER_SPOOL_DIR"] = "/custom/spool";

    Load().SpoolDirectory.Value.ShouldBe("/custom/spool");
  }

  [Fact]
  public void ReadsAProjectOptOut()
  {
    // A sensitive project can opt out of user-scope tracing (ADR 0013).
    var projectFile = WriteProjectFile("""{ "enabled": false }""");

    Load().Enabled.ShouldBe(new Setting<bool>(false, $"project ({projectFile})"));
  }

  [Theory]
  [InlineData("false", false)]
  [InlineData("FALSE", false)]
  [InlineData("true", true)]
  public void ReadsEnabledFromTheEnvironment(string value, bool expected)
  {
    _env.Variables["AGENTLEDGER_ENABLED"] = value;

    Load().Enabled.Value.ShouldBe(expected);
  }

  [Theory]
  [InlineData("AGENTLEDGER_URL", "not a url")]
  [InlineData("AGENTLEDGER_URL", "ftp://wrong-scheme")]
  [InlineData("AGENTLEDGER_TIMEOUT_MS", "soon")]
  [InlineData("AGENTLEDGER_TIMEOUT_MS", "0")]
  public void SkipsAnInvalidValueWithAWarning(string variable, string value)
  {
    _env.Variables[variable] = value;

    var settings = Load();

    settings.Url.Source.ShouldBe("default");
    settings.Timeout.Source.ShouldBe("default");
    settings.Warnings.ShouldContain(w => w.Contains(variable));
  }

  [Fact]
  public void SkipsAMalformedFileWithAWarning()
  {
    var projectFile = WriteProjectFile("{ this is not json");
    WriteUserFile("""{ "url": "http://from-user:3" }""");

    var settings = Load();

    settings.Url.Value.ShouldBe(new Uri("http://from-user:3"));
    settings.Warnings.ShouldContain(w => w.Contains(projectFile));
  }
}
