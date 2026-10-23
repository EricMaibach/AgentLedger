namespace AgentLedger.Core.ApiKeyAggregate;

// Exactly one per key (ADR 0019): a leaked ingest key can't read, a leaked read key can't send events.
public sealed class ApiKeyScope : SmartEnum<ApiKeyScope>
{
  public static readonly ApiKeyScope Ingest = new(nameof(Ingest), 1);
  public static readonly ApiKeyScope Read = new(nameof(Read), 2);

  private ApiKeyScope(string name, int value) : base(name, value) { }

  // How the scope appears in a token: agl_ingest_…, agl_read_…
  public string TokenName => Name.ToLowerInvariant();
}
