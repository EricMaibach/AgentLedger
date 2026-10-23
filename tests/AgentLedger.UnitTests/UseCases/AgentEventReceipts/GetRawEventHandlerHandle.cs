using AgentLedger.Core.AgentEventReceiptAggregate;
using AgentLedger.Core.AgentEventReceiptAggregate.Specifications;
using AgentLedger.Core.ApiKeyAggregate;
using AgentLedger.UnitTests.Core.AgentEventReceiptAggregate;
using AgentLedger.UseCases.AgentEventReceipts.GetRawEvent;
using AgentLedger.UseCases.ApiKeys;
using Ardalis.Result;
using Ardalis.Specification;

namespace AgentLedger.UnitTests.UseCases.AgentEventReceipts;

public sealed class GetRawEventHandlerHandle
{
  private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

  private readonly IReadRepository<AgentEventReceipt> _repository = Substitute.For<IReadRepository<AgentEventReceipt>>();
  private readonly IApiKeyQueryService _apiKeys = Substitute.For<IApiKeyQueryService>();
  private readonly GetRawEventHandler _handler;
  private ISpecification<AgentEventReceipt>? _specUsed;

  public GetRawEventHandlerHandle()
  {
    _repository.ListAsync(Arg.Do<ISpecification<AgentEventReceipt>>(spec => _specUsed = spec), Arg.Any<CancellationToken>())
      .Returns(new List<AgentEventReceipt>());
    _apiKeys.GetSendersAsync(Arg.Any<IReadOnlyCollection<ApiKeyId>>(), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ApiKeyId, ApiKeySender>());
    _handler = new GetRawEventHandler(_repository, _apiKeys);
  }

  // The repository returns these receipts (as if the specification had already been applied).
  private void RepositoryReturns(params AgentEventReceipt[] receipts) =>
    _repository.ListAsync(Arg.Do<ISpecification<AgentEventReceipt>>(spec => _specUsed = spec), Arg.Any<CancellationToken>())
      .Returns(receipts.ToList());

  [Fact]
  public async Task ReturnsTheEventWithItsEnvelopeAndPayload()
  {
    var eventId = AgentEventId.From(Guid.CreateVersion7());
    var original = new AgentEventReceiptBuilder()
      .WithEventId(eventId)
      .WithAgent(AgentKind.ClaudeCode)
      .WithEventType("PreToolUse")
      .WithNativeSessionId("session-1")
      .WithCapturedAt(T0)
      .WithReceivedAt(T0.AddMilliseconds(40))
      .WithContext(new CaptureContext("devbox", "eric", "/workspace", "github.com/owner/repo", "main", null))
      .WithTags(new Dictionary<string, string> { ["story"] = "123" })
      .WithPayload("""{ "tool_name": "Bash" }""")
      .Build();
    RepositoryReturns(original);

    var result = await _handler.Handle(new GetRawEventQuery(eventId.Value), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Ok);
    var detail = result.Value;
    detail.EventId.ShouldBe(eventId.Value);
    detail.Agent.ShouldBe("ClaudeCode");                // the domain name; the endpoint maps it to the wire name
    detail.EventType.ShouldBe("PreToolUse");
    detail.NativeSessionId.ShouldBe("session-1");
    detail.CapturedAt.ShouldBe(T0);
    detail.Host.ShouldBe("devbox");
    detail.User.ShouldBe("eric");
    detail.ProjectDir.ShouldBe("/workspace");
    detail.GitRepo.ShouldBe("github.com/owner/repo");
    detail.GitBranch.ShouldBe("main");
    detail.GitWorktree.ShouldBeNull();
    detail.Tags.ShouldBe(new Dictionary<string, string> { ["story"] = "123" });
    detail.Payload.ShouldBe("""{ "tool_name": "Bash" }"""); // exact text, untouched
  }

  [Fact]
  public async Task ListsEveryReceiptInTheOrderTheRepositoryReturnsThem()
  {
    // The specification sorts oldest first; the handler must keep that order.
    var eventId = AgentEventId.From(Guid.CreateVersion7());
    var original = new AgentEventReceiptBuilder().WithEventId(eventId).WithReceivedAt(T0).Build();
    var resend = new AgentEventReceiptBuilder().WithEventId(eventId).WithReceivedAt(T0.AddSeconds(30)).Build();
    RepositoryReturns(original, resend);

    var result = await _handler.Handle(new GetRawEventQuery(eventId.Value), CancellationToken.None);

    result.Value.Receipts.ShouldBe([
      new RawEventReceipt(original.Id.Value, T0, SentWith: null),
      new RawEventReceipt(resend.Id.Value, T0.AddSeconds(30), SentWith: null)]);
  }

  [Fact]
  public async Task SaysWhoSentEachReceiptAndWithWhichKey()
  {
    // Receipts sent with a key are attributed to its owner (ADR 0019); older ones have no sender.
    var eventId = AgentEventId.From(Guid.CreateVersion7());
    var keyId = ApiKeyId.From(Guid.CreateVersion7());
    var withKey = new AgentEventReceiptBuilder().WithEventId(eventId).WithReceivedAt(T0).WithApiKeyId(keyId).Build();
    var withoutKey = new AgentEventReceiptBuilder().WithEventId(eventId).WithReceivedAt(T0.AddSeconds(1)).Build();
    RepositoryReturns(withKey, withoutKey);
    var sender = new ApiKeySender(keyId.Value, "dev container", "eric");
    _apiKeys.GetSendersAsync(Arg.Is<IReadOnlyCollection<ApiKeyId>>(ids => ids.SequenceEqual(new[] { keyId })), Arg.Any<CancellationToken>())
      .Returns(new Dictionary<ApiKeyId, ApiKeySender> { [keyId] = sender });

    var result = await _handler.Handle(new GetRawEventQuery(eventId.Value), CancellationToken.None);

    result.Value.Receipts.Select(r => r.SentWith).ShouldBe([sender, null]);
  }

  [Fact]
  public async Task TakesTheEnvelopeFromTheOriginalReceipt()
  {
    // Resends carry the same envelope, but "the event" is defined by its first receipt.
    var eventId = AgentEventId.From(Guid.CreateVersion7());
    var original = new AgentEventReceiptBuilder().WithEventId(eventId).WithEventType("Original").WithReceivedAt(T0).Build();
    var resend = new AgentEventReceiptBuilder().WithEventId(eventId).WithEventType("Resend").WithReceivedAt(T0.AddSeconds(30)).Build();
    RepositoryReturns(original, resend);

    var result = await _handler.Handle(new GetRawEventQuery(eventId.Value), CancellationToken.None);

    result.Value.EventType.ShouldBe("Original");
  }

  [Fact]
  public async Task AsksTheRepositoryForThatEventsReceipts()
  {
    // Checks the handler passes the right event ID into the specification: evaluating the
    // specification it used against a mixed list must keep only that event's receipt.
    var eventId = AgentEventId.From(Guid.CreateVersion7());
    var ofThisEvent = new AgentEventReceiptBuilder().WithEventId(eventId).Build();
    var ofAnotherEvent = new AgentEventReceiptBuilder().WithEventId(AgentEventId.From(Guid.CreateVersion7())).Build();

    await _handler.Handle(new GetRawEventQuery(eventId.Value), CancellationToken.None);

    _specUsed.ShouldBeOfType<ReceiptsByEventIdSpec>();
    _specUsed!.Evaluate([ofThisEvent, ofAnotherEvent]).ShouldBe([ofThisEvent]);
  }

  [Fact]
  public async Task ReturnsNotFoundForAnUnknownEvent()
  {
    var result = await _handler.Handle(new GetRawEventQuery(Guid.CreateVersion7()), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.NotFound);
  }

  [Fact]
  public async Task RejectsAnEmptyEventIdWithoutQuerying()
  {
    var result = await _handler.Handle(new GetRawEventQuery(Guid.Empty), CancellationToken.None);

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.ShouldHaveSingleItem().Identifier.ShouldBe("eventId");
    _repository.ReceivedCalls().ShouldBeEmpty();
  }
}
