using System.Net;
using AgentLedger.Cli.Sending;
using AgentLedger.Cli.Tests.Login;

namespace AgentLedger.Cli.Tests.Sending;

// Every request names the CLI and its version (ADR 0024), so the ledger's machines page shows what each machine runs.
public sealed class CliHttpCreate
{
  [Fact]
  public void NamesTheCliInUserAgent()
  {
    CliHttp.UserAgent.ShouldBe($"agentledger-cli/{CliVersion.Current}");
  }

  [Fact]
  public async Task SendsUserAgentWithEveryRequest()
  {
    var stub = StubHttp.Returning(HttpStatusCode.OK);
    using var http = CliHttp.Create(TimeSpan.FromSeconds(1), stub);

    await http.GetAsync("http://ledger.test/status");

    stub.Request!.Headers.UserAgent.ToString().ShouldBe(CliHttp.UserAgent);
  }

  [Fact]
  public void HasAVersion()
  {
    CliVersion.Current.ShouldNotBeNullOrWhiteSpace();
  }
}

// The ledger names its version in every answer (ADR 0024): the CLI follows it when it updates itself.
public sealed class LedgerVersionTrackerTrack
{
  [Fact]
  public async Task NotesTheLedgersVersion()
  {
    var tracker = new LedgerVersionTracker(new StubHttp(_ =>
    {
      var response = new HttpResponseMessage(HttpStatusCode.Created);
      response.Headers.Add(LedgerVersionTracker.Header, "0.7.0+abc");
      return response;
    }));
    using var http = new HttpClient(tracker);

    await http.PostAsync("http://ledger.test/events", new StringContent("{}"));

    tracker.LedgerVersion.ShouldBe("0.7.0+abc");
  }

  [Fact]
  public async Task KnowsNothingWithoutTheHeader()
  {
    var tracker = new LedgerVersionTracker(StubHttp.Returning(HttpStatusCode.Created));
    using var http = new HttpClient(tracker);

    await http.PostAsync("http://ledger.test/events", new StringContent("{}"));

    tracker.LedgerVersion.ShouldBeNull();
  }
}
