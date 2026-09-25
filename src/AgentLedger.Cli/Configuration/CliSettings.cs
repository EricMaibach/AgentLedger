namespace AgentLedger.Cli.Configuration;

/// <summary>A resolved setting and where it came from, e.g. "environment (AGENTLEDGER_URL)" or "default".</summary>
internal sealed record Setting<T>(T Value, string Source);

/// <summary>The CLI's settings (ADR 0012), each resolved separately. Warnings describe values that were skipped.</summary>
internal sealed record CliSettings(
  Setting<Uri> Url,
  Setting<string?> ApiKey,
  Setting<TimeSpan> Timeout,
  Setting<string> SpoolDirectory,
  Setting<bool> Enabled,
  IReadOnlyList<string> Warnings);
