using System.Net.Http.Headers;

namespace AgentLedger.Cli.Sending;

/// <summary>
/// The HTTP client every command uses. Each request names the CLI and its version in User-Agent (ADR 0024), so the
/// ledger knows what each machine runs.
/// </summary>
internal static class CliHttp
{
  public static string UserAgent { get; } = $"agentledger-cli/{CliVersion.Current}";

  public static HttpClient Create(TimeSpan timeout, HttpMessageHandler? handler = null)
  {
    var http = handler is null ? new HttpClient() : new HttpClient(handler);
    http.Timeout = timeout;
    http.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse(UserAgent));
    return http;
  }
}

/// <summary>Notes the ledger's version from its answers (ADR 0024): what the CLI follows when it updates itself.</summary>
internal sealed class LedgerVersionTracker : DelegatingHandler
{
  public const string Header = "X-AgentLedger-Version";

  public LedgerVersionTracker() : base(new HttpClientHandler()) { }

  public LedgerVersionTracker(HttpMessageHandler inner) : base(inner) { }

  /// <summary>The version in the latest answer that named one.</summary>
  public string? LedgerVersion { get; private set; }

  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  {
    var response = await base.SendAsync(request, cancellationToken);
    if (response.Headers.TryGetValues(Header, out var values) && values.FirstOrDefault() is { Length: > 0 } version)
    {
      LedgerVersion = version;
    }

    return response;
  }
}
