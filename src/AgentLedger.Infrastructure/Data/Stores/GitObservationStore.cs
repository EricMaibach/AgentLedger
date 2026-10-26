using System.Text.Json;
using AgentLedger.Core.GitAggregate;
using AgentLedger.Infrastructure.Data.Config;
using AgentLedger.UseCases.Git;
using Npgsql;
using NpgsqlTypes;

namespace AgentLedger.Infrastructure.Data.Stores;

// Observed commits in one statement (ADR 0021), skipping those already stored, and tag observations appended. Commits have lists of varying
// length (parents, files), so the batch goes to Postgres as JSON and jsonb_to_recordset unpacks it. Raw SQL
// bypasses the audit interceptor, so it sets the audit timestamps itself.
public sealed class GitObservationStore(AppDbContext db, TimeProvider clock) : IGitObservationStore
{
  private const string InsertCommits = """
    INSERT INTO git_commits (id, repository, hash, parent_hashes, authored_at, committed_at, message, files, files_truncated,
                             api_key_id, received_at, created_at, updated_at)
    SELECT c.id, c.repository, c.hash, c.parent_hashes, c.authored_at, c.committed_at, c.message, c.files, c.files_truncated,
           c.api_key_id, c.received_at, @now, @now
    FROM jsonb_to_recordset(@commits) AS c(id uuid, repository text, hash text, parent_hashes text[], authored_at timestamptz,
                                           committed_at timestamptz, message text, files jsonb, files_truncated boolean,
                                           api_key_id uuid, received_at timestamptz)
    ON CONFLICT (id) DO NOTHING
    """;

  // One transaction: a batch's commits and tag observations are stored together or not at all.
  public async Task<int> AddAsync(IReadOnlyList<GitCommit> commits, IReadOnlyList<GitTagObservation> tags, CancellationToken cancellationToken)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    var stored = await AddCommitsAsync(commits, cancellationToken);

    // Observations are new rows by definition (appended, never conflicting), so EF inserts them; its interceptor
    // sets the audit timestamps.
    db.GitTagObservations.AddRange(tags);
    await db.SaveChangesAsync(cancellationToken);

    await transaction.CommitAsync(cancellationToken);
    return stored;
  }

  private async Task<int> AddCommitsAsync(IReadOnlyList<GitCommit> commits, CancellationToken cancellationToken)
  {
    if (commits.Count == 0)
    {
      return 0;
    }

    var rows = commits.Select(c => new Dictionary<string, object?>
    {
      ["id"] = c.Id.Value,
      ["repository"] = c.Repository,
      ["hash"] = c.Hash.Value,
      ["parent_hashes"] = c.ParentHashes.Select(p => p.Value).ToArray(),
      ["authored_at"] = c.AuthoredAt,
      ["committed_at"] = c.CommittedAt,
      ["message"] = c.Message,
      ["files"] = JsonDocument.Parse(GitCommitFilesJson.Write(c.Files)).RootElement,
      ["files_truncated"] = c.FilesTruncated,
      ["api_key_id"] = c.ApiKeyId.Value,
      ["received_at"] = c.ReceivedAt,
    });

    return await db.Database.ExecuteSqlRawAsync(InsertCommits,
    [
      new NpgsqlParameter("commits", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(rows) },
      new NpgsqlParameter("now", clock.GetUtcNow()),
    ], cancellationToken);
  }
}
