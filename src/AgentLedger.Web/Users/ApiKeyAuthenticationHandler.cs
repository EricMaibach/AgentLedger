using System.Security.Claims;
using System.Text.Encodings.Web;
using AgentLedger.UseCases.ApiKeys.AuthenticateApiKey;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AgentLedger.Web.Users;

// Authenticates `Authorization: Bearer agl_…` (ADR 0019). Only endpoints whose policy names this scheme use it
// (ingest, the JSON API, the current-key check); pages never do. Failure is 401, the wrong scope 403.
internal sealed class ApiKeyAuthenticationHandler(
  IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IMediator mediator)
  : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
  public const string SchemeName = "ApiKey";
  public const string KeyIdClaim = "agentledger:api_key_id";
  public const string KeyNameClaim = "agentledger:api_key_name";
  public const string ScopeClaim = "agentledger:api_key_scope";

  private const string BearerPrefix = "Bearer ";

  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    string? header = Request.Headers.Authorization;
    if (header is null || !header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
    {
      return AuthenticateResult.NoResult();
    }

    var result = await mediator.Send(new AuthenticateApiKeyCommand(header[BearerPrefix.Length..].Trim(),
      CliUserAgent.VersionOf(Request.Headers.UserAgent.ToString())), Context.RequestAborted);
    if (!result.IsSuccess)
    {
      return AuthenticateResult.Fail("Invalid API key.");
    }

    var holder = result.Value;
    var identity = new ClaimsIdentity(
    [
      new Claim(ClaimTypes.NameIdentifier, holder.OwnerId.ToString()),
      new Claim(ClaimTypes.Name, holder.Owner),
      new Claim(KeyIdClaim, holder.KeyId.ToString()),
      new Claim(KeyNameClaim, holder.KeyName),
      new Claim(ScopeClaim, holder.Scope),
    ], SchemeName);

    return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
  }
}
