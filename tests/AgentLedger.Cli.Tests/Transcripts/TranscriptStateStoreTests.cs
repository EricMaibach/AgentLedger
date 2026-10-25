using AgentLedger.Cli.Transcripts;

namespace AgentLedger.Cli.Tests.Transcripts;

// Where each transcript was read up to, and a lock so parallel hooks skip rather than collide (ADR 0020).
public sealed class TranscriptStateStoreTests : IDisposable
{
  private readonly TempDirectory _dir = new();
  private readonly TranscriptStateStore _store;

  public TranscriptStateStoreTests() => _store = new TranscriptStateStore(Path.Combine(_dir.Path, "transcripts"));

  public void Dispose() => _dir.Dispose();

  [Fact]
  public void RemembersWhereAFileWasReadUpTo()
  {
    var state = new TranscriptFileState("/x/s1.jsonl", Offset: 412, FingerprintLength: 100, Fingerprint: "abc");

    _store.Save(state);

    _store.Load("/x/s1.jsonl").ShouldBe(state);
    _store.Load("/x/other.jsonl").ShouldBeNull();
    _store.Count().ShouldBe(1);
  }

  [Fact]
  public void LetsOnlyOneProcessReadAFileAtATime()
  {
    using (var first = _store.TryLock("/x/s1.jsonl"))
    {
      first.ShouldNotBeNull();
      _store.TryLock("/x/s1.jsonl").ShouldBeNull();      // another hook process would skip
      _store.TryLock("/x/other.jsonl").ShouldNotBeNull(); // other files aren't affected
    }

    _store.TryLock("/x/s1.jsonl").ShouldNotBeNull();      // released
  }
}
