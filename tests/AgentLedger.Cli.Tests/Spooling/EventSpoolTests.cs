using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Tests.Spooling;

public sealed class EventSpoolTests : IDisposable
{
  private static readonly DateTimeOffset T0 = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

  private readonly TempDirectory _dir = new();
  private readonly string _spoolPath;

  public EventSpoolTests() => _spoolPath = Path.Combine(_dir.Path, "spool"); // not created yet: the spool creates it

  public void Dispose() => _dir.Dispose();

  private EventSpool NewSpool(int maxFiles = 10_000, long maxBytes = 100_000_000) => new(_spoolPath, new SpoolLimits(maxFiles, maxBytes));

  // UUIDv7 IDs 0, 1, 2… seconds after T0, so tests control their order. Created once: each
  // CreateVersion7 call adds fresh random bits, so calling it again would give a different ID.
  private static readonly Guid[] Ids = Enumerable.Range(0, 30).Select(s => Guid.CreateVersion7(T0.AddSeconds(s))).ToArray();

  private static Guid IdAt(int seconds) => Ids[seconds];

  [Fact]
  public void WritesTheEnvelopeUnderItsEventId()
  {
    var id = IdAt(0);

    NewSpool().Write(id, """{"eventId":"x"}""");

    File.ReadAllText(Path.Combine(_spoolPath, $"{id}.json")).ShouldBe("""{"eventId":"x"}""");
  }

  [Fact]
  public void LeavesNoTemporaryFilesBehind()
  {
    NewSpool().Write(IdAt(0), "{}");

    Directory.GetFiles(_spoolPath).ShouldHaveSingleItem().ShouldEndWith(".json");
  }

  [Fact]
  public void ListsOldestFirst()
  {
    var spool = NewSpool();
    spool.Write(IdAt(20), "{}");
    spool.Write(IdAt(0), "{}");
    spool.Write(IdAt(10), "{}");

    spool.List().Select(e => e.EventId).ShouldBe([IdAt(0), IdAt(10), IdAt(20)]);
  }

  [Fact]
  public void ListsAtMostTheRequestedNumber()
  {
    var spool = NewSpool();
    spool.Write(IdAt(0), "{}");
    spool.Write(IdAt(1), "{}");
    spool.Write(IdAt(2), "{}");

    spool.List(max: 2).Select(e => e.EventId).ShouldBe([IdAt(0), IdAt(1)]);
  }

  [Fact]
  public void IgnoresFilesThatAreNotSpooledEvents()
  {
    // E.g. a temporary file left by a process that was killed mid-write.
    var spool = NewSpool();
    spool.Write(IdAt(0), "{}");
    File.WriteAllText(Path.Combine(_spoolPath, $"{IdAt(1)}.json.tmp-abc"), "partial");
    File.WriteAllText(Path.Combine(_spoolPath, "notes.txt"), "?");

    spool.List().ShouldHaveSingleItem().EventId.ShouldBe(IdAt(0));
  }

  [Fact]
  public void ReadsASpooledEvent()
  {
    var spool = NewSpool();
    spool.Write(IdAt(0), """{"a":1}""");

    spool.Read(spool.List().Single()).ShouldBe("""{"a":1}""");
  }

  [Fact]
  public void RemovesAnEventAndToleratesItAlreadyBeingGone()
  {
    // Parallel hook processes may send and remove the same file; that must not be an error.
    var spool = NewSpool();
    spool.Write(IdAt(0), "{}");
    var spooled = spool.List().Single();

    spool.Remove(spooled);
    spool.Remove(spooled);

    spool.List().ShouldBeEmpty();
    spool.Read(spooled).ShouldBeNull();
  }

  [Fact]
  public void ListsNothingWhenTheFolderDoesNotExistYet()
  {
    NewSpool().List().ShouldBeEmpty();
  }

  [Fact]
  public void MakesTheFolderAndFilesReadableOnlyByTheUser()
  {
    if (OperatingSystem.IsWindows())
    {
      return; // Unix permissions; on Windows the per-user profile folder is already private
    }

    var id = IdAt(0);
    NewSpool().Write(id, "{}");

    File.GetUnixFileMode(_spoolPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    File.GetUnixFileMode(Path.Combine(_spoolPath, $"{id}.json")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
  }

  [Fact]
  public void DropsTheOldestEventsBeyondTheFileLimit()
  {
    var spool = NewSpool(maxFiles: 2);
    spool.Write(IdAt(0), "{}");
    spool.Write(IdAt(1), "{}");

    var dropped = spool.Write(IdAt(2), "{}");

    dropped.ShouldBe([IdAt(0)]);
    spool.List().Select(e => e.EventId).ShouldBe([IdAt(1), IdAt(2)]);
  }

  [Fact]
  public void DropsTheOldestEventsBeyondTheSizeLimit()
  {
    var spool = NewSpool(maxBytes: 25);
    spool.Write(IdAt(0), new string('a', 10));
    spool.Write(IdAt(1), new string('b', 10));

    var dropped = spool.Write(IdAt(2), new string('c', 10));

    dropped.ShouldBe([IdAt(0)]);
    spool.Measure().ShouldBe(new SpoolSize(Count: 2, Bytes: 20));
  }
}
