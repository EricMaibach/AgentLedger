using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.UseCases.AgentEventReceipts.Ingest;
using Ardalis.Result;
using Microsoft.Extensions.Time.Testing;
using NSubstitute.ExceptionExtensions;

namespace AgentLedger.UnitTests.UseCases.AgentEventReceipts;

public sealed class IngestEventHandlerHandle
{
  private static readonly DateTimeOffset Now = new(2026, 9, 24, 9, 30, 0, TimeSpan.Zero);

  private readonly IRepository<AgentEventReceipt> _repository = Substitute.For<IRepository<AgentEventReceipt>>();
  private readonly FakeTimeProvider _clock = new(Now);
  private readonly IngestEventHandler _handler;
  private AgentEventReceipt? _saved;

  public IngestEventHandlerHandle()
  {
    // Capture whatever the handler saves, and return it as the real repository would.
    _repository.AddAsync(Arg.Do<AgentEventReceipt>(r => _saved = r), Arg.Any<CancellationToken>())
      .Returns(call => call.Arg<AgentEventReceipt>());
    _handler = new IngestEventHandler(_repository, _clock);
  }

  // A valid command; tests derive variants with `with { ... }`.
  private static IngestEventCommand ValidCommand() => new(
    EventId: Guid.CreateVersion7(),
    Agent: "ClaudeCode",
    EventType: "PreToolUse",
    NativeSessionId: "3829bce8-0000-0000-0000-000000000000",
    CapturedAt: Now.AddMilliseconds(-40),
    Host: "devbox",
    User: "eric",
    ProjectDir: "/workspace",
    GitRepo: "github.com/EricMaibach/AgentLedger",
    GitBranch: "main",
    GitWorktree: null,
    Tags: new Dictionary<string, string> { ["story"] = "123" },
    Payload: """{"session_id":"3829bce8","hook_event_name":"PreToolUse"}""");

  [Fact]
  public async Task ReturnsCreatedWithTheNewReceiptId()
  {
    var result = await _handler.Handle(ValidCommand(), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Created);
    _saved.ShouldNotBeNull();
    result.Value.ShouldBe(_saved.Id);
  }

  [Fact]
  public async Task SavesAReceiptCarryingTheCommandValues()
  {
    var command = ValidCommand();

    await _handler.Handle(command, CancellationToken.None);

    _saved.ShouldNotBeNull();
    _saved.EventId.ShouldBe(AgentEventId.From(command.EventId));
    _saved.Agent.ShouldBe(AgentKind.ClaudeCode);
    _saved.EventType.ShouldBe(command.EventType);
    _saved.NativeSessionId.ShouldBe(command.NativeSessionId);
    _saved.CapturedAt.ShouldBe(command.CapturedAt);
    _saved.Context.ShouldBe(new CaptureContext(command.Host, command.User, command.ProjectDir,
      command.GitRepo, command.GitBranch, command.GitWorktree));
    _saved.Tags.ShouldBe(command.Tags);
    _saved.Payload.ShouldBe(command.Payload);
  }

  [Fact]
  public async Task TakesReceivedAtFromTheClock()
  {
    await _handler.Handle(ValidCommand(), CancellationToken.None);

    _saved.ShouldNotBeNull();
    _saved.ReceivedAt.ShouldBe(Now);
  }

  [Fact]
  public async Task GivesEachReceiptItsOwnIdEvenForTheSameEvent()
  {
    // ELT: a resend is a new receipt with a new ReceiptId and the same EventId (ADR 0010).
    var command = ValidCommand();

    var first = await _handler.Handle(command, CancellationToken.None);
    var resend = await _handler.Handle(command, CancellationToken.None);

    resend.Status.ShouldBe(ResultStatus.Created);
    resend.Value.ShouldNotBe(first.Value);
    resend.Value.Value.ShouldNotBe(command.EventId);
  }

  [Fact]
  public async Task RejectsUnknownAgent()
  {
    var result = await _handler.Handle(ValidCommand() with { Agent = "Cursor" }, CancellationToken.None);

    ShouldBeInvalidAndNotSaved(result, "agent");
    result.ValidationErrors.Single().ErrorMessage.ShouldContain("Cursor");
  }

  [Fact]
  public async Task RejectsEmptyEventId()
  {
    var result = await _handler.Handle(ValidCommand() with { EventId = Guid.Empty }, CancellationToken.None);

    ShouldBeInvalidAndNotSaved(result, "eventId");
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task RejectsMissingEventType(string eventType)
  {
    var result = await _handler.Handle(ValidCommand() with { EventType = eventType }, CancellationToken.None);

    ShouldBeInvalidAndNotSaved(result, "eventType");
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task RejectsMissingPayload(string payload)
  {
    var result = await _handler.Handle(ValidCommand() with { Payload = payload }, CancellationToken.None);

    ShouldBeInvalidAndNotSaved(result, "payload");
  }

  [Fact]
  public async Task RejectsMissingCapturedAt()
  {
    var result = await _handler.Handle(ValidCommand() with { CapturedAt = default }, CancellationToken.None);

    ShouldBeInvalidAndNotSaved(result, "capturedAt");
  }

  [Fact]
  public async Task LetsSaveFailuresPropagate()
  {
    // E.g. the database is down. This is not bad input: it must surface as an exception
    // (HTTP 500, so the CLI retries), never as Invalid (HTTP 400, so the CLI gives up).
    _repository.AddAsync(Arg.Any<AgentEventReceipt>(), Arg.Any<CancellationToken>())
      .ThrowsAsync(new InvalidOperationException("database unavailable"));

    await Should.ThrowAsync<InvalidOperationException>(
      () => _handler.Handle(ValidCommand(), CancellationToken.None).AsTask());
  }

  [Fact]
  public async Task LetsArgumentExceptionsFromSavingPropagate()
  {
    // Only the domain's guard clauses mean "invalid input". An ArgumentException thrown while
    // saving is an infrastructure bug and must not be mistaken for one.
    _repository.AddAsync(Arg.Any<AgentEventReceipt>(), Arg.Any<CancellationToken>())
      .ThrowsAsync(new ArgumentException("bug in the data layer"));

    await Should.ThrowAsync<ArgumentException>(
      () => _handler.Handle(ValidCommand(), CancellationToken.None).AsTask());
  }

  // Identifiers are the camelCase field names the API caller sends in the request JSON.
  private void ShouldBeInvalidAndNotSaved(Result<ReceiptId> result, string expectedField)
  {
    result.Status.ShouldBe(ResultStatus.Invalid);
    var error = result.ValidationErrors.ShouldHaveSingleItem();
    error.Identifier.ShouldBe(expectedField);
    error.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    _repository.ReceivedCalls().ShouldBeEmpty();
  }
}
