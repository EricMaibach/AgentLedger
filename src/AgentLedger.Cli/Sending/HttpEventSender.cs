using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace AgentLedger.Cli.Sending;

internal enum SendResult
{
  /// <summary>Stored (201). Remove from the spool.</summary>
  Accepted,

  /// <summary>The API refused it as malformed (400 and similar). Resending won't help: remove it and log why.</summary>
  Rejected,

  /// <summary>Server problem, timeout or no connection. Keep it in the spool and retry later.</summary>
  Failed,
}

internal sealed record SendOutcome(SendResult Result, string Detail);

internal interface IEventSender
{
  Task<SendOutcome> SendAsync(string envelopeJson, CancellationToken cancellationToken);
}

/// <summary>POSTs envelopes to the API's /events endpoint (ADR 0011). The HttpClient's timeout bounds each send.</summary>
internal sealed class HttpEventSender(HttpClient http, Uri apiUrl, string? apiKey) : IEventSender
{
  private readonly Uri _eventsUrl = new(apiUrl.ToString().TrimEnd('/') + "/events");

  public async Task<SendOutcome> SendAsync(string envelopeJson, CancellationToken cancellationToken)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, _eventsUrl)
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
      var status = (int)response.StatusCode;
      var detail = $"{status} {response.ReasonPhrase}";

      if (response.IsSuccessStatusCode)
      {
        return new SendOutcome(SendResult.Accepted, detail);
      }

      // 408 and 429 are 4xx but temporary; every other 4xx means this request itself is wrong.
      var temporary = status >= 500 || response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests;
      if (!temporary)
      {
        detail += $": {await response.Content.ReadAsStringAsync(cancellationToken)}";
      }

      return new SendOutcome(temporary ? SendResult.Failed : SendResult.Rejected, detail);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
      return new SendOutcome(SendResult.Failed, ex.Message); // unreachable or timed out
    }
  }
}
