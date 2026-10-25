using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Transcripts;

/// <summary>Reading transcripts into their spool, and sending what's waiting there (ADR 0020).</summary>
internal sealed class TranscriptPipeline(TranscriptCatchUp catchUp, SpoolDrainer drainer)
{
  /// <summary>Batches sent per hook run: enough to keep up, few enough to stay quick. `flush` sends the rest.</summary>
  public const int SendLimitPerHook = 3;

  public void CatchUp(string payload) => catchUp.Run(payload);

  public Task<DrainReport> SendAsync(int? limit, CancellationToken cancellationToken) => drainer.DrainAsync(limit, except: null, cancellationToken);
}
