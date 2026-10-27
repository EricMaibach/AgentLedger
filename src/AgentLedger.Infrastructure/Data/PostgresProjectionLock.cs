using AgentLedger.UseCases.Projections;
using Npgsql;

namespace AgentLedger.Infrastructure.Data;

// A session-level Postgres advisory lock on a connection of its own, held while the connection is open. Not pooled:
// a pooled connection stays open when disposed, and would keep the lock. If the connection drops, the lock is released
// and another instance may take over; two workers for a moment would only rebuild the same sessions twice, which is
// harmless (each rebuild is one transaction).
public sealed class PostgresProjectionLock(string connectionString) : IProjectionLock
{
  private readonly string _unpooled = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString;

  // Arbitrary, fixed: identifies the projection worker's lock among any advisory locks in the database.
  private const long LockKey = 0x41474C50524F4A31; // "AGLPROJ1"

  public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
  {
    var connection = new NpgsqlConnection(_unpooled);
    try
    {
      await connection.OpenAsync(cancellationToken);
      await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
      command.Parameters.AddWithValue("key", LockKey);
      if (await command.ExecuteScalarAsync(cancellationToken) is true)
        return connection; // closing it releases the lock
    }
    catch
    {
      await connection.DisposeAsync();
      throw;
    }

    await connection.DisposeAsync();
    return null;
  }
}
