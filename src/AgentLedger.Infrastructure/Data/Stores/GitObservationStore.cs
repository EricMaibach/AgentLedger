using System.Text.Json;
using AgentLedger.Core.GitAggregate;
using AgentLedger.Infrastructure.Data.Config;
using AgentLedger.UseCases.Git;
using Npgsql;
using NpgsqlTypes;

namespace AgentLedger.Infrastructure.Data.Stores;

// Observed commits and tag observations (ADR 0021), each in one statement that skips what is already stored. Commits have lists of varying
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

  private const string InsertTags = """
    INSERT INTO git_tag_observations (id, repository, tag_name, change, commit, previous_commit, observed_at, api_key_id, received_at,
                                      created_at, updated_at)
    SELECT t.id, t.repository, t.tag_name, t.change, t.commit, t.previous_commit, t.observed_at, t.api_key_id, t.received_at, @now, @now
    FROM jsonb_to_recordset(@tags) AS t(id uuid, repository text, tag_name text, change text, commit text, previous_commit text,
                                        observed_at timestamptz, api_key_id uuid, received_at timestamptz)
    ON CONFLICT (id) DO NOTHING
    """;

  // One transaction: a batch's commits and tag observations are stored together or not at all. Both skip what is
  // already stored: commits by repository and hash, tag observations by the ID the CLI gave them (a batch sent twice).
  public async Task<GitStoreCounts> AddAsync(IReadOnlyList<GitCommit> commits, IReadOnlyList<GitTagObservation> tags, CancellationToken cancellationToken)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    var commitsStored = await AddCommitsAsync(commits, cancellationToken);
    var tagsStored = await AddTagsAsync(tags, cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return new GitStoreCounts(commitsStored, tagsStored);
  }

  private async Task<int> AddTagsAsync(IReadOnlyList<GitTagObservation> tags, CancellationToken cancellationToken)
  {
    if (tags.Count == 0)
    {
      return 0;
    }

    var rows = tags.Select(t => new Dictionary<string, object?>
    {
      ["id"] = t.Id.Value,
      ["repository"] = t.Repository,
      ["tag_name"] = t.TagName,
      ["change"] = t.Change.Name,
      ["commit"] = t.Commit?.Value,
      ["previous_commit"] = t.PreviousCommit?.Value,
      ["observed_at"] = t.ObservedAt,
      ["api_key_id"] = t.ApiKeyId.Value,
      ["received_at"] = t.ReceivedAt,
    });

    return await db.Database.ExecuteSqlRawAsync(InsertTags,
    [
      new NpgsqlParameter("tags", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(rows) },
      new NpgsqlParameter("now", clock.GetUtcNow()),
    ], cancellationToken);
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
