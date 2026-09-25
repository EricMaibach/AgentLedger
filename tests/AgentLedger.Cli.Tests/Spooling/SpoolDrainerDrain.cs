using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Tests.Spooling;

public sealed class SpoolDrainerDrain : IDisposable
{
  private readonly TempDirectory _dir = new();
  private readonly EventSpool _spool;
  private readonly List<SendResult> _responses = [];
  private readonly IEventSender _sender = Substitute.For<IEventSender>();
  private readonly SpoolDrainer _drainer;

  public SpoolDrainerDrain()
  {
    _spool = new EventSpool(Path.Combine(_dir.Path, "spool"), SpoolLimits.Default);
    var call = 0;
    // Replies from _responses in order; Accepted once the list runs out.
    _sender.SendAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
      .Returns(_ => new SendOutcome(call < _responses.Count ? _responses[call++] : SendResult.Accepted, "detail"));
    _drainer = new SpoolDrainer(_spool, _sender, Substitute.For<ICliLog>());
  }

  public void Dispose() => _dir.Dispose();

  private List<Guid> Seed(int count) =>
    Enumerable.Range(0, count).Select(i =>
    {
      var id = Guid.CreateVersion7(DateTimeOffset.UtcNow.AddMinutes(-count + i));
      _spool.Write(id, "{}");
      return id;
    }).ToList();

  [Fact]
  public async Task SendsEverythingWhenThereIsNoLimit()
  {
    Seed(12);

    var report = await _drainer.DrainAsync(limit: null, except: null, CancellationToken.None);

    report.ShouldBe(new DrainReport(Sent: 12, Rejected: 0, StoppedBecause: null));
    _spool.List().ShouldBeEmpty();
  }

  [Fact]
  public async Task StopsAtTheFirstFailureAndSaysWhy()
  {
    Seed(4);
    _responses.AddRange([SendResult.Accepted, SendResult.Failed]);

    var report = await _drainer.DrainAsync(limit: null, except: null, CancellationToken.None);

    report.ShouldBe(new DrainReport(Sent: 1, Rejected: 0, StoppedBecause: "detail"));
    _spool.List().Count.ShouldBe(3);
  }

  [Fact]
  public async Task RemovesAndCountsRejectedEvents()
  {
    Seed(2);
    _responses.AddRange([SendResult.Rejected, SendResult.Accepted]);

    var report = await _drainer.DrainAsync(limit: null, except: null, CancellationToken.None);

    report.ShouldBe(new DrainReport(Sent: 1, Rejected: 1, StoppedBecause: null));
    _spool.List().ShouldBeEmpty();
  }

  [Fact]
  public async Task SkipsTheExcludedEventAndRespectsTheLimit()
  {
    var ids = Seed(4);

    var report = await _drainer.DrainAsync(limit: 2, except: ids[0], CancellationToken.None);

    report.Sent.ShouldBe(2);
    _spool.List().Select(e => e.EventId).ShouldBe([ids[0], ids[3]]);
  }
}
