using System.Text;

namespace AgentLedger.Cli.Spooling;

internal sealed record SpoolLimits(int MaxFiles, long MaxBytes)
{
  public static readonly SpoolLimits Default = new(MaxFiles: 10_000, MaxBytes: 100L * 1024 * 1024);
}

internal sealed record SpoolSize(int Count, long Bytes);

internal sealed record SpooledEvent(Guid EventId, string Path);

/// <summary>
/// Envelopes waiting to be sent, one file per event: <c>&lt;eventId&gt;.json</c> (ADR 0012). Each hook invocation
/// is a separate process, so the spool lives on disk. Files are written to a temporary name and renamed,
/// which is atomic: parallel hook processes never see a half-written file and need no locks.
/// </summary>
internal sealed class EventSpool(string directory, SpoolLimits limits)
{
  private const string Extension = ".json";

  public string Directory => directory;

  /// <summary>Writes the envelope, then enforces the size limits. Returns the IDs of any events dropped to do so.</summary>
  public IReadOnlyList<Guid> Write(Guid eventId, string envelopeJson)
  {
    EnsureDirectory();

    var finalPath = PathFor(eventId);
    var tempPath = $"{finalPath}.tmp-{Guid.NewGuid():N}";
    var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
    if (!OperatingSystem.IsWindows())
    {
      options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; // contains prompts and tool output
    }

    using (var stream = new FileStream(tempPath, options))
    using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
    {
      writer.Write(envelopeJson);
    }

    File.Move(tempPath, finalPath, overwrite: true);
    return EnforceLimits();
  }

  /// <summary>Spooled events, oldest first. UUIDv7 text starts with the timestamp, so name order is time order.</summary>
  public IReadOnlyList<SpooledEvent> List(int? max = null)
  {
    if (!System.IO.Directory.Exists(directory))
    {
      return [];
    }

    var events = System.IO.Directory.EnumerateFiles(directory)
      .Where(path => Path.GetExtension(path) == Extension)
      .Select(path => (Path: path, Name: Path.GetFileNameWithoutExtension(path)))
      .Where(file => Guid.TryParse(file.Name, out _))
      .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
      .Select(file => new SpooledEvent(Guid.Parse(file.Name), file.Path));

    return (max is { } limit ? events.Take(limit) : events).ToList();
  }

  /// <summary>The envelope JSON, or null if another process already sent and removed it.</summary>
  public string? Read(SpooledEvent spooled)
  {
    try
    {
      return File.ReadAllText(spooled.Path);
    }
    catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
    {
      return null;
    }
  }

  /// <summary>Removes the event with this ID, if it is still waiting.</summary>
  public void Remove(Guid eventId) => Remove(new SpooledEvent(eventId, PathFor(eventId)));

  /// <summary>Removes the event; already being gone is fine (another process may have removed it).</summary>
  public void Remove(SpooledEvent spooled)
  {
    try
    {
      File.Delete(spooled.Path);
    }
    catch (DirectoryNotFoundException)
    {
      // nothing to remove
    }
  }

  public SpoolSize Measure()
  {
    var files = List().Select(e => new FileInfo(e.Path)).Where(f => f.Exists).ToList();
    return new SpoolSize(files.Count, files.Sum(f => f.Length));
  }

  // Keeps the spool from filling the disk during a long outage: drops the oldest events first.
  private List<Guid> EnforceLimits()
  {
    var files = List().Select(e => (Event: e, Size: new FileInfo(e.Path).Length)).ToList();
    var count = files.Count;
    var bytes = files.Sum(f => f.Size);
    var dropped = new List<Guid>();

    foreach (var (oldest, size) in files)
    {
      if (count <= limits.MaxFiles && bytes <= limits.MaxBytes)
      {
        break;
      }

      Remove(oldest);
      dropped.Add(oldest.EventId);
      count--;
      bytes -= size;
    }

    return dropped;
  }

  private void EnsureDirectory()
  {
    if (System.IO.Directory.Exists(directory))
    {
      return;
    }

    if (OperatingSystem.IsWindows())
    {
      System.IO.Directory.CreateDirectory(directory); // the per-user profile folder is already private
    }
    else
    {
      System.IO.Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
  }

  private string PathFor(Guid eventId) => Path.Combine(directory, $"{eventId}{Extension}");
}
