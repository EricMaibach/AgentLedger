using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using AgentLedger.Cli.Sending;

namespace AgentLedger.Cli.Transcripts;

/// <summary>POSTs a transcript batch to /transcripts/lines, gzipped (ADR 0020). Settled like events.</summary>
internal sealed class HttpTranscriptSender(HttpClient http, Uri apiUrl, string? apiKey) : IEventSender
{
  private readonly Uri _url = new(apiUrl.ToString().TrimEnd('/') + "/transcripts/lines");

  public async Task<SendOutcome> SendAsync(string envelopeJson, CancellationToken cancellationToken)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, _url) { Content = Gzip(envelopeJson) };
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

  // Transcript text compresses about 4× (ADR 0020); the fastest level is plenty.
  private static ByteArrayContent Gzip(string json)
  {
    using var buffer = new MemoryStream();
    using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
    {
      gzip.Write(Encoding.UTF8.GetBytes(json));
    }

    var content = new ByteArrayContent(buffer.ToArray());
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    content.Headers.ContentEncoding.Add("gzip");
    return content;
  }
}
