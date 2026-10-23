namespace AgentLedger.Core.ApiKeyAggregate;

// An API key as people and clients see it (ADR 0019): agl_<scope>_<key ID>_<secret>. The prefix makes keys
// recognizable to people and secret scanners; the scope says what it's for; the key ID finds the stored key;
// only the secret is secret, and only its hash is stored.
public sealed record ApiKeyToken(ApiKeyScope Scope, ApiKeyId KeyId, string Secret)
{
  public const string Prefix = "agl";
  public const int KeyIdLength = 22; // a GUID's 16 bytes in base62
  public const int SecretBytes = 32;
  public const int SecretLength = 43; // 32 bytes in base62

  public string Value => $"{Prefix}_{Scope.TokenName}_{Base62.Encode(KeyId.Value.ToByteArray(), KeyIdLength)}_{Secret}";

  public static ApiKeyToken Create(ApiKeyScope scope, ApiKeyId keyId, byte[] secretBytes) =>
    new(scope, keyId, Base62.Encode(secretBytes, SecretLength));

  public static bool TryParse(string? text, out ApiKeyToken token)
  {
    token = null!; // only read when this returns true
    if (text?.Split('_') is not [Prefix, var scopeName, var keyIdText, var secret]
        || ApiKeyScope.List.FirstOrDefault(s => s.TokenName == scopeName) is not { } scope
        || keyIdText.Length != KeyIdLength
        || !Base62.TryDecode(keyIdText, 16, out var keyIdBytes)
        || !ApiKeyId.TryFrom(new Guid(keyIdBytes), out var keyId)
        || secret.Length != SecretLength
        || !Base62.TryDecode(secret, SecretBytes, out _))
    {
      return false;
    }

    token = new ApiKeyToken(scope, keyId, secret);
    return true;
  }

  // Tokens must not end up in logs.
  public override string ToString() => $"{Prefix}_{Scope.TokenName}_{KeyId}_(secret)";
}
