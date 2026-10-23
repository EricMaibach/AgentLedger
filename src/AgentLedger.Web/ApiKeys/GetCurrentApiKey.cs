using System.Security.Claims;
using AgentLedger.Web.Users;

namespace AgentLedger.Web.ApiKeys;

/// <summary>What the presented API key is: for its holder to check it (the CLI's login and status, ADR 0019).</summary>
public sealed record CurrentApiKeyResponse(Guid KeyId, string Name, string Scope, string Owner);

/// <summary>GET /api/keys/current: any valid key, whatever its scope.</summary>
public sealed class GetCurrentApiKey : EndpointWithoutRequest<CurrentApiKeyResponse>
{
  /// <inheritdoc />
  public override void Configure()
  {
    Get("/api/keys/current");
    Policies(AuthenticationConfig.AnyApiKeyPolicy);
    Summary(s =>
    {
      s.Summary = "Describe the API key used for this request";
      s.Description = "Its ID, name, scope (ingest or read) and owner. 401 if the key is missing, unknown, revoked or expired.";
    });
  }

  /// <inheritdoc />
  public override Task HandleAsync(CancellationToken ct) =>
    Send.OkAsync(new CurrentApiKeyResponse(
      Guid.Parse(User.FindFirstValue(ApiKeyAuthenticationHandler.KeyIdClaim)!),
      User.FindFirstValue(ApiKeyAuthenticationHandler.KeyNameClaim)!,
      User.FindFirstValue(ApiKeyAuthenticationHandler.ScopeClaim)!.ToLowerInvariant(),
      User.FindFirstValue(ClaimTypes.Name)!), ct);
}
