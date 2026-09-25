using AgentLedger.Cli.Envelope;

namespace AgentLedger.Cli.Tests.Envelope;

public sealed class GitConfigFileRead : IDisposable
{
  private readonly TempDirectory _dir = new();

  public void Dispose() => _dir.Dispose();

  private string? Read(string content, string section = "user", string key = "email") =>
    GitConfigFile.Read(_dir.Write("config", content), section, key);

  [Fact]
  public void ReadsAPlainValue()
  {
    Read("[user]\n\temail = eric@example.com\n").ShouldBe("eric@example.com");
  }

  [Fact]
  public void RemovesQuotesLikeGitDoes()
  {
    // Git allows quoted values and strips the quotes; whitespace inside the quotes is kept.
    Read("[user]\n\temail = \" eric@example.com\"\n").ShouldBe(" eric@example.com");
  }

  [Fact]
  public void UnescapesQuotedCharacters()
  {
    Read("[user]\n\tname = \"Eric \\\"E\\\" M\"\n", key: "name").ShouldBe("Eric \"E\" M");
  }

  [Fact]
  public void IgnoresATrailingComment()
  {
    Read("[user]\n\temail = eric@example.com ; work address\n").ShouldBe("eric@example.com");
  }

  [Fact]
  public void ReadsFromTheRightSubsection()
  {
    var config = "[remote \"upstream\"]\n\turl = https://a/x\n[remote \"origin\"]\n\turl = https://b/y\n";

    Read(config, "remote \"origin\"", "url").ShouldBe("https://b/y");
  }

  [Fact]
  public void ReturnsNullWhenTheKeyIsMissing()
  {
    Read("[user]\n\tname = Eric\n").ShouldBeNull();
  }
}
