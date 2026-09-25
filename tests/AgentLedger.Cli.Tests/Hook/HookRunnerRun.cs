using AgentLedger.Cli.Envelope;
using AgentLedger.Cli.Hook;
using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Spooling;
using Microsoft.Extensions.Time.Testing;

namespace AgentLedger.Cli.Tests.Hook;

public sealed class HookRunnerRun : IDisposable
{
  private const string Payload = """{"session_id":"s1","hook_event_name":"PreToolUse"}""";

  private readonly TempDirectory _dir = new();
  private readonly EventSpool _spool;
  private readonly FakeSender _sender = new();
  private readonly FakeLog _log = new();

  public HookRunnerRun() => _spool = new EventSpool(Path.Combine(_dir.Path, "spool"), SpoolLimits.Default);

  public void Dispose() => _dir.Dispose();

  private HookRunner NewRunner(EventSpool? spool = null) => new(
    new EnvelopeBuilder(new FakeHostEnvironment { CurrentDirectory = _dir.Path }, new FakeTimeProvider(DateTimeOffset.UtcNow)),
    spool ?? _spool, _sender, _log, resendLimit: 5);

  private Task RunAsync(HookRunner? runner = null) => (runner ?? NewRunner()).RunAsync("claude-code", Payload, CancellationToken.None);

  // Events left over from an earlier run while the API was down.
  private void SeedSpool(int count)
  {
    for (var i = 0; i < count; i++)
    {
      _spool.Write(Guid.CreateVersion7(DateTimeOffset.UtcNow.AddMinutes(-10 + i)), """{"earlier":true}""");
    }
  }

  [Fact]
  public async Task SendsTheEnvelopeAndLeavesNothingInTheSpool()
  {
    await RunAsync();

    _sender.Sent.ShouldHaveSingleItem().ShouldContain(Payload);
    _spool.List().ShouldBeEmpty();
  }

  [Fact]
  public async Task WritesTheEventToTheSpoolBeforeSendingIt()
  {
    // If the agent kills the hook mid-request, the event must already be safe on disk (ADR 0012).
    var spooledWhileSending = 0;
    _sender.OnSend = _ => spooledWhileSending = _spool.List().Count;

    await RunAsync();

    spooledWhileSending.ShouldBe(1);
  }

  [Fact]
  public async Task KeepsTheEventInTheSpoolWhenTheApiIsUnavailable()
  {
    _sender.Respond = _ => new SendOutcome(SendResult.Failed, "Connection refused");

    await RunAsync();

    _spool.List().ShouldHaveSingleItem();
    _log.Lines.ShouldContain(l => l.Contains("Connection refused"));
  }

  [Fact]
  public async Task RemovesARejectedEventAndLogsWhy()
  {
    _sender.Respond = _ => new SendOutcome(SendResult.Rejected, "400 Bad Request: unknown agent");

    await RunAsync();

    _spool.List().ShouldBeEmpty();
    _log.Lines.ShouldContain(l => l.StartsWith("ERROR") && l.Contains("unknown agent"));
  }

  [Fact]
  public async Task ResendsWaitingEventsAfterASuccessfulSend()
  {
    SeedSpool(2);

    await RunAsync();

    _sender.Sent.Count.ShouldBe(3);
    _spool.List().ShouldBeEmpty();
  }

  [Fact]
  public async Task DoesNotResendWhileTheApiIsUnavailable()
  {
    SeedSpool(2);
    _sender.Respond = _ => new SendOutcome(SendResult.Failed, "down");

    await RunAsync();

    _sender.Sent.Count.ShouldBe(1);   // only the new event was tried
    _spool.List().Count.ShouldBe(3);
  }

  [Fact]
  public async Task StopsResendingOnceTheApiFails()
  {
    SeedSpool(3);
    var calls = 0;
    _sender.Respond = _ => ++calls <= 2 ? new SendOutcome(SendResult.Accepted, "201") : new SendOutcome(SendResult.Failed, "down");

    await RunAsync();

    _sender.Sent.Count.ShouldBe(3);   // new event, one resend, then the failure stops it
    _spool.List().Count.ShouldBe(2);
  }

  [Fact]
  public async Task ResendsAtMostTheLimitPerRun()
  {
    // Keeps each hook quick; `flush` or later runs send the rest.
    SeedSpool(10);

    await RunAsync();

    _sender.Sent.Count.ShouldBe(1 + 5);
    _spool.List().Count.ShouldBe(5);
  }

  [Fact]
  public async Task LogsInsteadOfThrowing()
  {
    _sender.Respond = _ => throw new InvalidOperationException("unexpected bug");

    await Should.NotThrowAsync(() => RunAsync());

    _log.Lines.ShouldContain(l => l.StartsWith("ERROR") && l.Contains("unexpected bug"));
    _spool.List().ShouldHaveSingleItem(); // written first, so still safe for a later run
  }

  [Fact]
  public async Task LogsEventsDroppedBecauseTheSpoolIsFull()
  {
    var tinySpool = new EventSpool(Path.Combine(_dir.Path, "tiny"), new SpoolLimits(MaxFiles: 1, MaxBytes: long.MaxValue));
    tinySpool.Write(Guid.CreateVersion7(DateTimeOffset.UtcNow.AddMinutes(-5)), "{}");
    _sender.Respond = _ => new SendOutcome(SendResult.Failed, "down");

    await RunAsync(NewRunner(tinySpool));

    _log.Lines.ShouldContain(l => l.StartsWith("ERROR") && l.Contains("dropped"));
  }

  private sealed class FakeSender : IEventSender
  {
    public List<string> Sent { get; } = [];
    public Func<string, SendOutcome> Respond { get; set; } = _ => new SendOutcome(SendResult.Accepted, "201 Created");
    public Action<string>? OnSend { get; set; }

    public Task<SendOutcome> SendAsync(string envelopeJson, CancellationToken cancellationToken)
    {
      Sent.Add(envelopeJson);
      OnSend?.Invoke(envelopeJson);
      return Task.FromResult(Respond(envelopeJson));
    }
  }

  private sealed class FakeLog : ICliLog
  {
    public List<string> Lines { get; } = [];
    public void Info(string message) => Lines.Add($"INFO {message}");
    public void Error(string message) => Lines.Add($"ERROR {message}");
  }
}
