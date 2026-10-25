using AgentLedger.Cli.Logging;
using AgentLedger.Cli.Sending;

namespace AgentLedger.Cli.Tests;

// Stands in for the API: records what was sent and answers as the test says.
internal sealed class FakeSender : IEventSender
{
  public List<string> Sent { get; } = [];
  public Func<string, SendOutcome> Respond { get; set; } = _ => new SendOutcome(SendResult.Accepted, "201 Created");
  public Action<string>? OnSend { get; set; }

  public Task<SendOutcome> SendAsync(string envelopeJson, CancellationToken cancellationToken)
  {
    Sent.Add(envelopeJson);
    OnSend?.Invoke(envelopeJson);
    return Task.FromResult(Respond(envelopeJson));
  }
}

internal sealed class FakeLog : ICliLog
{
  public List<string> Lines { get; } = [];
  public void Info(string message) => Lines.Add($"INFO {message}");
  public void Error(string message) => Lines.Add($"ERROR {message}");
}
