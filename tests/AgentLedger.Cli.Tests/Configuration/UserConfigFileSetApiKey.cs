using AgentLedger.Cli.Configuration;

namespace AgentLedger.Cli.Tests.Configuration;

// `login` stores the key in the user config file, never the project file (ADR 0012, ADR 0019).
public sealed class UserConfigFileSetApiKey : IDisposable
{
  private readonly TempDirectory _temp = new();
  private readonly FakeHostEnvironment _env = new();

  public UserConfigFileSetApiKey() => _env.UserConfigDirectory = _temp.Path;

  public void Dispose() => _temp.Dispose();

  [Fact]
  public void CreatesTheFileSoTheCliUsesTheKey()
  {
    UserConfigFile.SetApiKey(_env, "agl_ingest_abc_def");

    var settings = new SettingsLoader(_env).Load(_temp.Path);
    settings.ApiKey.Value.ShouldBe("agl_ingest_abc_def");
    settings.ApiKey.Source.ShouldStartWith("user");
  }

  [Fact]
  public void KeepsTheOtherSettings()
  {
    _temp.Write("agentledger/config.json", """{"url":"http://ledger.test:58090","timeoutMs":2500}""");

    UserConfigFile.SetApiKey(_env, "agl_ingest_abc_def");

    var settings = new SettingsLoader(_env).Load(_temp.Path);
    settings.Url.Value.ShouldBe(new Uri("http://ledger.test:58090"));
    settings.Timeout.Value.ShouldBe(TimeSpan.FromMilliseconds(2500));
  }

  [Fact]
  public void ReplacesAnEarlierKey()
  {
    UserConfigFile.SetApiKey(_env, "agl_ingest_old_key");

    UserConfigFile.SetApiKey(_env, "agl_ingest_new_key");

    new SettingsLoader(_env).Load(_temp.Path).ApiKey.Value.ShouldBe("agl_ingest_new_key");
  }

  [Fact]
  public void IsReadableOnlyByTheUser()
  {
    if (OperatingSystem.IsWindows())
    {
      return; // the user's profile folder already restricts access there
    }

    UserConfigFile.SetApiKey(_env, "agl_ingest_abc_def");

    File.GetUnixFileMode(UserConfigFile.PathFor(_env)).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
  }
}

// `setup` saves the ledger's address in the user config file (ADR 0024), keeping everything else.
public sealed class UserConfigFileSet : IDisposable
{
  private readonly TempDirectory _temp = new();
  private readonly FakeHostEnvironment _env = new();

  public UserConfigFileSet() => _env.UserConfigDirectory = _temp.Path;

  public void Dispose() => _temp.Dispose();

  [Fact]
  public void SavesTheAddressNextToTheKey()
  {
    UserConfigFile.SetApiKey(_env, "agl_ingest_abc_def");

    UserConfigFile.Set(_env, "url", "http://host.docker.internal:58090");

    var settings = new SettingsLoader(_env).Load(_temp.Path);
    settings.Url.Value.ShouldBe(new Uri("http://host.docker.internal:58090"));
    settings.Url.Source.ShouldStartWith("user");
    settings.ApiKey.Value.ShouldBe("agl_ingest_abc_def");
  }
}
