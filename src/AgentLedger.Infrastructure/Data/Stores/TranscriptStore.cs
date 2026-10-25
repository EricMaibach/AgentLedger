using AgentLedger.Core.TranscriptAggregate;
using AgentLedger.UseCases.Transcripts;
using Npgsql;
using NpgsqlTypes;

namespace AgentLedger.Infrastructure.Data.Stores;

// A transcript batch in one transaction and two statements (ADR 0020). SQL rather than EF change tracking:
// `ON CONFLICT DO NOTHING` skips lines already stored without an error per duplicate, and unnest() inserts
// up to a thousand lines as one statement. Raw SQL bypasses the audit interceptor, so it sets the audit
// timestamps itself.
public sealed class TranscriptStore(AppDbContext db, TimeProvider clock) : ITranscriptStore
{
  private const string UpsertFile = """
    INSERT INTO transcript_files (id, agent, native_session_id, file, subagent_id, meta, api_key_id, first_seen_at, created_at, updated_at)
    VALUES (@id, @agent, @session, @file, @subagent, @meta, @key, @firstSeen, @now, @now)
    ON CONFLICT (id) DO UPDATE SET meta = EXCLUDED.meta, updated_at = EXCLUDED.updated_at
      WHERE transcript_files.meta IS NULL AND EXCLUDED.meta IS NOT NULL
    """;

  private const string InsertLines = """
    INSERT INTO transcript_lines (id, agent, native_session_id, file, subagent_id, byte_offset, text, line_type, api_key_id, received_at, created_at, updated_at)
    SELECT line.id, @agent, @session, @file, @subagent, line.byte_offset, line.text, line.line_type, line.api_key_id, line.received_at, @now, @now
    FROM unnest(@ids, @offsets, @texts, @types, @keys, @received) AS line(id, byte_offset, text, line_type, api_key_id, received_at)
    ON CONFLICT (id) DO NOTHING
    """;

  public async Task<int> AddAsync(TranscriptFile file, IReadOnlyList<TranscriptLine> lines, CancellationToken cancellationToken)
  {
    if (lines.Any(l => l.Agent != file.Agent || l.NativeSessionId != file.NativeSessionId || l.File != file.File))
    {
      throw new ArgumentException("Every line of a batch must belong to its file.", nameof(lines));
    }

    var now = clock.GetUtcNow();
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

    await db.Database.ExecuteSqlRawAsync(UpsertFile,
    [
      new NpgsqlParameter("id", file.Id.Value),
      new NpgsqlParameter("agent", file.Agent.Name),
      new NpgsqlParameter("session", file.NativeSessionId),
      new NpgsqlParameter("file", file.File.Value),
      new NpgsqlParameter("subagent", NpgsqlDbType.Text) { Value = (object?)file.SubagentId ?? DBNull.Value },
      new NpgsqlParameter("meta", NpgsqlDbType.Text) { Value = (object?)file.Meta ?? DBNull.Value },
      new NpgsqlParameter("key", file.ApiKeyId.Value),
      new NpgsqlParameter("firstSeen", file.FirstSeenAt),
      new NpgsqlParameter("now", now),
    ], cancellationToken);

    var stored = lines.Count == 0 ? 0 : await db.Database.ExecuteSqlRawAsync(InsertLines,
    [
      new NpgsqlParameter("agent", file.Agent.Name),
      new NpgsqlParameter("session", file.NativeSessionId),
      new NpgsqlParameter("file", file.File.Value),
      new NpgsqlParameter("subagent", NpgsqlDbType.Text) { Value = (object?)file.SubagentId ?? DBNull.Value },
      new NpgsqlParameter("now", now),
      new NpgsqlParameter("ids", lines.Select(l => l.Id.Value).ToArray()),
      new NpgsqlParameter("offsets", lines.Select(l => l.ByteOffset).ToArray()),
      new NpgsqlParameter("texts", lines.Select(l => l.Text).ToArray()),
      new NpgsqlParameter("types", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = lines.Select(l => l.LineType).ToArray() },
      new NpgsqlParameter("keys", lines.Select(l => l.ApiKeyId.Value).ToArray()),
      new NpgsqlParameter("received", lines.Select(l => l.ReceivedAt).ToArray()),
    ], cancellationToken);

    await transaction.CommitAsync(cancellationToken);
    return stored;
  }
}
