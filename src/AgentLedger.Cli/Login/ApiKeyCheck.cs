using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AgentLedger.Cli.Login;

internal enum ApiKeyCheckResult
{
  /// <summary>The ledger knows the key and it's active.</summary>
  Accepted,

  /// <summary>Unknown, revoked or expired: events sent with it are refused (and wait in the spool).</summary>
  Rejected,

  /// <summary>The ledger couldn't be asked (unreachable, or an error).</summary>
  Unknown,
}

/// <summary>What the ledger says about an API key (GET /api/keys/current, ADR 0019), for `login` and `status`.</summary>
internal sealed record ApiKeyCheck(ApiKeyCheckResult Result, string Detail, string? Name, string? Scope, string? Owner)
{
  public static async Task<ApiKeyCheck> CheckAsync(HttpClient http, Uri apiUrl, string apiKey)
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(apiUrl.ToString().TrimEnd('/') + "/api/keys/current"));
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

    try
    {
      using var response = await http.SendAsync(request);
      if (response.StatusCode == HttpStatusCode.Unauthorized)
      {
        return new ApiKeyCheck(ApiKeyCheckResult.Rejected, "rejected: unknown, revoked or expired", null, null, null);
      }

      if (!response.IsSuccessStatusCode)
      {
        return Unknown($"{(int)response.StatusCode} {response.ReasonPhrase}");
      }

      // JsonDocument needs no reflection, so this stays Native AOT-safe.
      using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
      var name = json.RootElement.GetProperty("name").GetString();
      var scope = json.RootElement.GetProperty("scope").GetString();
      var owner = json.RootElement.GetProperty("owner").GetString();
      return new ApiKeyCheck(ApiKeyCheckResult.Accepted, $"accepted: '{name}' ({scope}, owner {owner})", name, scope, owner);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
      return Unknown($"not reachable: {ex.Message}");
    }
    catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
    {
      return Unknown($"unexpected answer: {ex.Message}");
    }
  }

  private static ApiKeyCheck Unknown(string why) => new(ApiKeyCheckResult.Unknown, $"unknown ({why})", null, null, null);
}
