using AgentLedger.Cli.Envelope;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.Cli.Tests.Envelope;

public sealed class EnvelopeBuilderBuild : IDisposable
{
  private const string ClaudeCodePayload = """{"session_id":"3829bce8","hook_event_name":"PreToolUse","cwd":"/elsewhere"}""";
  private static readonly DateTimeOffset Now = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

  private readonly TempDirectory _dir = new();
  private readonly FakeHostEnvironment _env = new();
  private readonly EnvelopeBuilder _builder;

  public EnvelopeBuilderBuild()
  {
    _env.CurrentDirectory = _dir.CreateDirectory("cwd");
    _env.HomeDirectory = _dir.CreateDirectory("home");
    _builder = new EnvelopeBuilder(_env, new FakeTimeProvider(Now));
  }

  public void Dispose() => _dir.Dispose();

  private EventEnvelope Build(string agent = "claude-code", string payload = ClaudeCodePayload) => _builder.Build(agent, payload);

  [Fact]
  public void GivesEachInvocationANewVersion7EventId()
  {
    var first = Build();
    var second = Build();

    first.EventId.Version.ShouldBe(7);
    second.EventId.ShouldNotBe(first.EventId);
  }

  [Fact]
  public void TakesCapturedAtFromTheClock()
  {
    Build().CapturedAt.ShouldBe(Now);
  }

  [Fact]
  public void KeepsTheAgentAndPayloadAsGiven()
  {
    var envelope = Build("claude-code", ClaudeCodePayload);

    envelope.Agent.ShouldBe("claude-code");
    envelope.Payload.ShouldBe(ClaudeCodePayload);
  }

  [Fact]
  public void ReadsEventTypeAndSessionFromTheClaudeCodePayload()
  {
    var envelope = Build();

    envelope.EventType.ShouldBe("PreToolUse");
    envelope.NativeSessionId.ShouldBe("3829bce8");
  }

  [Theory]
  [InlineData("""{"something":"else"}""")]  // fields missing
  [InlineData("not json at all")]            // never lose an event over a missing label
  public void FallsBackWhenThePayloadLacksTheFields(string payload)
  {
    var envelope = Build(payload: payload);

    envelope.EventType.ShouldBe("unknown");
    envelope.NativeSessionId.ShouldBeNull();
  }

  [Fact]
  public void UsesClaudeProjectDirForClaudeCode()
  {
    // Not the payload's cwd, which changes whenever the agent runs `cd`.
    _env.Variables["CLAUDE_PROJECT_DIR"] = "/work/project";

    Build().ProjectDir.ShouldBe("/work/project");
  }

  [Fact]
  public void FallsBackToTheCurrentDirectoryForProjectDir()
  {
    Build().ProjectDir.ShouldBe(_env.CurrentDirectory);
  }

  [Fact]
  public void UsesTheHostOverrideWhenSet()
  {
    _env.Variables["AGENTLEDGER_HOST"] = "eric-devcontainer";

    Build().Host.ShouldBe("eric-devcontainer");
  }

  [Fact]
  public void FallsBackToTheMachineNameForHost()
  {
    Build().Host.ShouldBe(_env.MachineName);
  }

  [Fact]
  public void CollectsTagsWithLowercasedKeys()
  {
    _env.Variables["AGENTLEDGER_TAG_Story"] = "123";
    _env.Variables["AGENTLEDGER_TAG_workflow_state"] = "in-dev";
    _env.Variables["AGENTLEDGER_TAG_empty"] = "";        // skipped
    _env.Variables["AGENTLEDGER_URL"] = "http://x";       // not a tag
    _env.Variables["PATH"] = "/usr/bin";                  // not a tag

    Build().Tags.ShouldBe(new Dictionary<string, string> { ["story"] = "123", ["workflow_state"] = "in-dev" }, ignoreOrder: true);
  }

  [Fact]
  public void ReadsGitContextFromTheProjectDirectory()
  {
    _dir.Write("cwd/.git/HEAD", "ref: refs/heads/main\n");
    _dir.Write("cwd/.git/config", "[remote \"origin\"]\n\turl = git@github.com:owner/repo.git\n");

    var envelope = Build();

    envelope.GitRepo.ShouldBe("github.com/owner/repo");
    envelope.GitBranch.ShouldBe("main");
  }

  [Fact]
  public void PrefersTheUserOverride()
  {
    _env.Variables["AGENTLEDGER_USER"] = "orchestrator@example.com";
    _dir.Write("home/.gitconfig", "[user]\n\temail = eric@example.com\n");

    Build().User.ShouldBe("orchestrator@example.com");
  }

  [Fact]
  public void UsesTheRepositoryGitEmailBeforeTheGlobalOne()
  {
    _dir.Write("cwd/.git/HEAD", "ref: refs/heads/main\n");
    _dir.Write("cwd/.git/config", "[user]\n\temail = work@example.com\n");
    _dir.Write("home/.gitconfig", "[user]\n\temail = personal@example.com\n");

    Build().User.ShouldBe("work@example.com");
  }

  [Fact]
  public void UsesTheGlobalGitEmail()
  {
    _dir.Write("home/.gitconfig", "[user]\n\tname = Eric\n\temail = eric@example.com\n");

    Build().User.ShouldBe("eric@example.com");
  }

  [Fact]
  public void TrimsWhitespaceAroundTheGitEmail()
  {
    // Seen in a real ~/.gitconfig: email = " someone@example.com"
    _dir.Write("home/.gitconfig", "[user]\n\temail = \" eric@example.com \"\n");

    Build().User.ShouldBe("eric@example.com");
  }

  [Fact]
  public void FallsBackToTheOperatingSystemUser()
  {
    Build().User.ShouldBe(_env.UserName);
  }
}
