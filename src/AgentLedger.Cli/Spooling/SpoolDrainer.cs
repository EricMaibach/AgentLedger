using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Sending;

namespace AgentLedger.Cli.Spooling;

/// <param name="StoppedBecause">Why draining stopped early (the API failed), or null if it finished.</param>
internal sealed record DrainReport(int Sent, int Rejected, string? StoppedBecause);

/// <summary>
/// Sends spooled events oldest first and settles each (ADR 0012): accepted → removed; rejected as
/// malformed → removed and logged; failed → kept, and draining stops, since the API is unavailable.
/// Used by `hook` (a few per run) and `flush` (everything).
/// </summary>
internal sealed class SpoolDrainer(EventSpool spool, IEventSender sender, ICliLog log)
{
  public async Task<DrainReport> DrainAsync(int? limit, Guid? except, CancellationToken cancellationToken)
  {
    int sent = 0, rejected = 0;
    var waiting = spool.List(limit is { } max ? max + 1 : null).Where(e => e.EventId != except);

    foreach (var spooled in limit is { } take ? waiting.Take(take) : waiting)
    {
      if (spool.Read(spooled) is not { } json)
      {
        continue; // another process already sent it
      }

      var outcome = await SendAndSettleAsync(spooled.EventId, json, cancellationToken);
      switch (outcome.Result)
      {
        case SendResult.Accepted:
          sent++;
          break;
        case SendResult.Rejected:
          rejected++;
          break;
        case SendResult.Failed:
          return new DrainReport(sent, rejected, outcome.Detail);
      }
    }

    return new DrainReport(sent, rejected, StoppedBecause: null);
  }

  public async Task<SendOutcome> SendAndSettleAsync(Guid eventId, string json, CancellationToken cancellationToken)
  {
    var outcome = await sender.SendAsync(json, cancellationToken);
    switch (outcome.Result)
    {
      case SendResult.Accepted:
        spool.Remove(eventId);
        break;
      case SendResult.Rejected:
        spool.Remove(eventId); // malformed: resending would be rejected again
        log.Error($"API rejected event {eventId}; dropped it: {outcome.Detail}");
        break;
      case SendResult.Failed:
        log.Info($"API unavailable; event {eventId} stays in the spool: {outcome.Detail}");
        break;
    }

    return outcome;
  }
}
