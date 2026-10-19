using Microsoft.Data.Sqlite;
using PaceAtlas.Sync;

namespace PaceAtlas;

/// <summary>
/// Durable, metadata-only intake of proposed remote mutations. This is deliberately
/// NOT a record importer: payloads are never written to health tables in this phase.
/// </summary>
public sealed record SyncInboxReceipt(Guid OperationId, SyncDecision Decision, string Reason);

public sealed partial class Store
{
    private static void InitializeSyncInbox(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS sync_inbox (
                operation_uuid TEXT PRIMARY KEY,
                entity_table TEXT NOT NULL,
                record_uuid TEXT NOT NULL,
                decision TEXT NOT NULL,
                reason TEXT NOT NULL,
                received_at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_sync_inbox_record ON sync_inbox(entity_table, record_uuid);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Atomically validates a remote proposal against the local revision and records
    /// a durable decision. No mutation payload is persisted or applied. A future
    /// authenticated server endpoint must implement actual record application.
    /// </summary>
    public SyncInboxReceipt StageIncomingMutation(string entityTable, SyncMutation mutation)
    {
        if (!SyncIdentityTables.Contains(entityTable, StringComparer.Ordinal))
            throw new ArgumentException("Unknown entity type", nameof(entityTable));
        if (mutation is null) throw new ArgumentNullException(nameof(mutation));
        using var db = Open();
        // IMMEDIATE reserves the write lock before reading revision, preventing a
        // concurrent local writer from changing the state during this decision.
        using var tx = db.BeginTransaction(deferred: false);
        using (var previous = db.CreateCommand())
        {
            previous.Transaction = tx;
            previous.CommandText = "SELECT decision, reason FROM sync_inbox WHERE operation_uuid=$op";
            previous.Parameters.AddWithValue("$op", mutation.OperationId.ToString("D"));
            using var reader = previous.ExecuteReader();
            if (reader.Read())
            {
                var reason = reader.GetString(1);
                var priorDecision = reader.GetString(0);
                reader.Close();
                tx.Commit();
                return new(mutation.OperationId, SyncDecision.AlreadyApplied,
                    $"Proposal already evaluated ({priorDecision}): {reason}");
            }
        }
        long? revision = null;
        var deleted = false;
        using (var active = db.CreateCommand())
        {
            active.Transaction = tx;
            active.CommandText = $"SELECT sync_revision FROM {entityTable} WHERE sync_uuid=$id LIMIT 1";
            active.Parameters.AddWithValue("$id", mutation.RecordId.ToString("D"));
            var value = active.ExecuteScalar();
            if (value is not null && value is not DBNull) revision = Convert.ToInt64(value);
        }
        if (revision is null)
        {
            using var tombstone = db.CreateCommand();
            tombstone.Transaction = tx;
            tombstone.CommandText = "SELECT sync_revision FROM sync_tombstones WHERE entity_table=$table AND sync_uuid=$id LIMIT 1";
            tombstone.Parameters.AddWithValue("$table", entityTable);
            tombstone.Parameters.AddWithValue("$id", mutation.RecordId.ToString("D"));
            var value = tombstone.ExecuteScalar();
            if (value is not null && value is not DBNull)
            {
                revision = Convert.ToInt64(value);
                deleted = true;
            }
        }
        var state = revision.HasValue ? new SyncRecordState(mutation.RecordId, revision.Value, deleted) : null;
        var result = SyncPreconditions.Evaluate(mutation, state, false);
        using (var save = db.CreateCommand())
        {
            save.Transaction = tx;
            save.CommandText = """
                INSERT INTO sync_inbox(operation_uuid,entity_table,record_uuid,decision,reason,received_at_utc)
                VALUES($op,$table,$id,$decision,$reason,strftime('%Y-%m-%dT%H:%M:%fZ','now'))
                """;
            save.Parameters.AddWithValue("$op", mutation.OperationId.ToString("D"));
            save.Parameters.AddWithValue("$table", entityTable);
            save.Parameters.AddWithValue("$id", mutation.RecordId.ToString("D"));
            save.Parameters.AddWithValue("$decision", result.Decision.ToString());
            save.Parameters.AddWithValue("$reason", result.Reason);
            save.ExecuteNonQuery();
        }
        tx.Commit();
        return new(mutation.OperationId, result.Decision, result.Reason);
    }
}
