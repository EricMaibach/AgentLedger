using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Transcripts;

/// <summary>
/// What each hook does for transcripts (ADR 0020): for every file the payload names, take its lock (or skip it),
/// read the next chunk, write it to the transcript spool as batches, and only then move the saved offset on. If
/// the process dies in between, the next hook reads those lines again and the ledger recognizes them.
/// </summary>
internal sealed class TranscriptCatchUp(string agent, ITranscriptSource? source, TranscriptStateStore states, TranscriptReader reader,
  EventSpool spool, ICliLog log)
{
  public void Run(string payload)
  {
    if (source is null)
    {
      return;
    }

    foreach (var file in source.FilesFor(payload))
    {
      try
      {
        CatchUp(file);
      }
      catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
      {
        log.Error($"Couldn't read transcript {file.Path}: {ex.Message}");
      }
    }
  }

  private void CatchUp(TranscriptFileRef file)
  {
    using var fileLock = states.TryLock(file.Path);
    if (fileLock is null)
    {
      return; // another hook is reading it; the next one catches up
    }

    var state = states.Load(file.Path);
    var chunk = reader.Read(file, state);
    if (chunk.Lines.Count > 0)
    {
      var meta = file.MetaPath is { } metaPath && File.Exists(metaPath) ? File.ReadAllText(metaPath) : null;
      foreach (var batch in TranscriptBatches.ToJson(agent, file, meta, chunk.Lines))
      {
        foreach (var dropped in spool.Write(Guid.CreateVersion7(), batch))
        {
          log.Error($"Transcript spool full: dropped the oldest waiting batch {dropped}.");
        }
      }
    }

    if (chunk.NewState != state)
    {
      states.Save(chunk.NewState);
    }
  }
}
