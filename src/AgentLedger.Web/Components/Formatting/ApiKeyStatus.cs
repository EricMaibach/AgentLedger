using AgentLedger.UseCases.ApiKeys;

namespace AgentLedger.Web.Components.Formatting;

/// <summary>A key's state for the key list (ADR 0019).</summary>
public sealed record ApiKeyStatus(string Text, bool IsActive)
{
  /// <summary>Keys expiring within this many days are flagged in the list.</summary>
  public const int ExpiryWarningDays = 14;

  /// <summary>Describes a key at a point in time. Revoked wins over expired: it's the more deliberate fact.</summary>
  public static ApiKeyStatus Describe(ApiKeyListItem key, DateTimeOffset now)
  {
    if (key.RevokedAt is not null)
    {
      return new ApiKeyStatus($"Revoked by {key.RevokedBy}", IsActive: false);
    }

    if (key.ExpiresAt is not { } expiresAt)
    {
      return new ApiKeyStatus("Active", IsActive: true);
    }

    if (expiresAt <= now)
    {
      return new ApiKeyStatus("Expired", IsActive: false);
    }

    var days = (int)Math.Ceiling((expiresAt - now).TotalDays);
    return days <= ExpiryWarningDays
      ? new ApiKeyStatus(days == 1 ? "Expires in 1 day" : $"Expires in {days} days", IsActive: true)
      : new ApiKeyStatus("Active", IsActive: true);
  }
}
