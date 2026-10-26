using System.Net.Http.Headers;
using System.Text;
using AgentLedger.Cli.Sending;

namespace AgentLedger.Cli.Git;

/// <summary>POSTs a batch of observed commits to /git/observations (ADR 0021). Settled like events.</summary>
internal sealed class HttpGitSender(HttpClient http, Uri apiUrl, string? apiKey) : IEventSender
{
  private readonly Uri _url = new(apiUrl.ToString().TrimEnd('/') + "/git/observations");

  public async Task<SendOutcome> SendAsync(string envelopeJson, CancellationToken cancellationToken)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, _url)
    {
      Content = new StringContent(envelopeJson, Encoding.UTF8, "application/json"),
    };
    if (!string.IsNullOrEmpty(apiKey))
    {
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    try
    {
      using var response = await http.SendAsync(request, cancellationToken);
      return await SendOutcomes.FromResponseAsync(response, cancellationToken);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
      return new SendOutcome(SendResult.Failed, ex.Message); // unreachable or timed out
    }
  }
}
