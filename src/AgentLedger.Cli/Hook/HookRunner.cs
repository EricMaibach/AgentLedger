using AgentLedger.Cli.Envelope;
using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Spooling;

namespace AgentLedger.Cli.Hook;

/// <summary>
/// What `agentledger hook &lt;agent&gt;` does for one hook invocation (ADR 0012): write the envelope to the
/// spool first, send it, settle it, then resend a few events left over from earlier runs. It never
/// throws: whatever goes wrong is logged, and the agent is never disrupted.
/// </summary>
internal sealed class HookRunner(EnvelopeBuilder builder, EventSpool spool, IEventSender sender, ICliLog log, int resendLimit = HookRunner.DefaultResendLimit)
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

      // The API answered, so it's reachable: send a few older events too (a bounded number, to stay quick).
      if (outcome.Result != SendResult.Failed)
      {
        await _drainer.DrainAsync(resendLimit, except: envelope.EventId, cancellationToken);
      }
    }
    catch (Exception ex)
    {
      log.Error($"hook {agent} failed: {ex}");
    }
  }
}
