using System.Security.Claims;
using AgentLedger.UseCases.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AgentLedger.Web.Users;

// The sign-in cookie's contents (ADR 0018): who is signed in, and the security stamp it was issued with.
internal static class SessionCookie
{
  public const string SecurityStampClaim = "agentledger:security_stamp";
  public const string AdminRole = "admin";

  public static Task SignInAsync(HttpContext http, SignedInUser user)
  {
    List<Claim> claims =
    [
      new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
      new(ClaimTypes.Name, user.Username),
      new(SecurityStampClaim, user.SecurityStamp.ToString()),
    ];
    if (user.IsAdmin)
    {
      claims.Add(new Claim(ClaimTypes.Role, AdminRole));
    }

    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
  }

  public static Task SignOutAsync(HttpContext http) => http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

  public static bool TryRead(ClaimsPrincipal principal, out Guid userId, out Guid securityStamp)
  {
    securityStamp = Guid.Empty;
    return Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
           && Guid.TryParse(principal.FindFirstValue(SecurityStampClaim), out securityStamp);
  }
}
