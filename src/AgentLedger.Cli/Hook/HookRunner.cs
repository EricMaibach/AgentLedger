using AgentLedger.Cli.Envelope;
using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Hook;

/// <summary>
/// What `agentledger hook &lt;agent&gt;` does for one hook invocation (ADR 0012): write the envelope to the
/// spool first, send it, settle it, catch up the session's transcript (ADR 0020), then resend a few events and
/// transcript batches left over from earlier runs. It never
/// throws: whatever goes wrong is logged, and the agent is never disrupted.
/// </summary>
internal sealed class HookRunner(EnvelopeBuilder builder, EventSpool spool, IEventSender sender, ICliLog log, int resendLimit = HookRunner.DefaultResendLimit,
  Transcripts.TranscriptPipeline? transcripts = null)
{
  public const int DefaultResendLimit = 5;

  private readonly SpoolDrainer _drainer = new(spool, sender, log);

  public async Task RunAsync(string agent, string payload, CancellationToken cancellationToken)
  {
    try
    {
      var envelope = builder.Build(agent, payload);
      var json = EnvelopeJson.Write(envelope);

      // Write first: if the agent kills this process mid-request, the event is already safe on disk.
      foreach (var droppedId in spool.Write(envelope.EventId, json))
      {
        log.Error($"Spool full: dropped the oldest waiting event {droppedId}.");
      }

      var outcome = await _drainer.SendAndSettleAsync(envelope.EventId, json, cancellationToken);

      // Then the session's transcript (ADR 0020): never at the expense of the event, which is already recorded.
      try
      {
        transcripts?.CatchUp(payload);
      }
      catch (Exception ex)
      {
        log.Error($"hook {agent}: reading the transcript failed: {ex}");
      }

      // The API answered, so it's reachable: send a few older events and transcript batches too (bounded, to stay quick).
      if (outcome.Result != SendResult.Failed)
      {
        await _drainer.DrainAsync(resendLimit, except: envelope.EventId, cancellationToken);
        if (transcripts is not null)
        {
          await transcripts.SendAsync(Transcripts.TranscriptPipeline.SendLimitPerHook, cancellationToken);
        }
      }
    }
    catch (Exception ex)
    {
      log.Error($"hook {agent} failed: {ex}");
    }
  }
}
