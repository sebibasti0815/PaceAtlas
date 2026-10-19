using Microsoft.Data.Sqlite;

namespace PaceAtlas;

/// <summary>Metadata-only, bounded local change feed for a future sync transport.
/// No personal data or record payloads are exported by this API.</summary>
public sealed record SyncJournalEvent(long Sequence, string EventId, string EntityTable,
    string RecordId, long Revision, string Operation, string ChangedAtUtc);

public sealed record SyncJournalPage(long AfterSequence, long SnapshotSequence,
    IReadOnlyList<SyncJournalEvent> Events, bool HasMore);

public sealed record SyncLocalRecordState(string EntityTable, string RecordId,
    long Revision, bool IsDeleted);

public sealed partial class Store
{
    /// <summary>Returns a stable, bounded page of local changes. Pass the returned
    /// SnapshotSequence unchanged while paging and the last received Sequence as afterSequence.
    /// A later synchronization session may request a new snapshot.</summary>
    public SyncJournalPage ReadSyncJournal(long afterSequence = 0, int limit = 100,
        long? snapshotSequence = null)
    {
        if (afterSequence < 0 || limit is < 1 or > 500 || snapshotSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(afterSequence), "Invalid sync page arguments");
        using var db = Open();
        using var tx = db.BeginTransaction();
        long upper;
        using (var watermark = db.CreateCommand())
        {
            watermark.Transaction = tx;
            watermark.CommandText = "SELECT COALESCE(MAX(sequence), 0) FROM sync_change_journal";
            upper = (long)(watermark.ExecuteScalar() ?? 0L);
        }
        if (snapshotSequence.HasValue)
        {
            if (snapshotSequence.Value < afterSequence)
                throw new ArgumentException("Snapshot is older than cursor", nameof(snapshotSequence));
            upper = Math.Min(upper, snapshotSequence.Value);
        }
        var entries = new List<SyncJournalEvent>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = tx;
            command.CommandText = """
                SELECT sequence, event_uuid, entity_table, sync_uuid, sync_revision, operation, changed_at_utc
                FROM sync_change_journal
                WHERE sequence > $after AND sequence <= $upper
                ORDER BY sequence ASC LIMIT $limit
                """;
            command.Parameters.AddWithValue("$after", afterSequence);
            command.Parameters.AddWithValue("$upper", upper);
            command.Parameters.AddWithValue("$limit", limit + 1);
            using var reader = command.ExecuteReader();
            while (reader.Read())
                entries.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.GetInt64(4), reader.GetString(5), reader.GetString(6)));
        }
        tx.Commit();
        var more = entries.Count > limit;
        if (more) entries.RemoveAt(entries.Count - 1);
        return new(afterSequence, upper, entries, more);
    }

    /// <summary>Read-only authoritative local revision lookup. Deleted IDs remain discoverable.
    /// The table name is strictly validated against the internal allowlist.</summary>
    public SyncLocalRecordState? ReadSyncRecordState(string entityTable, string recordId)
    {
        if (!SyncIdentityTables.Contains(entityTable, StringComparer.Ordinal))
            throw new ArgumentException("Unknown sync entity type", nameof(entityTable));
        if (!Guid.TryParse(recordId, out var uuid) || uuid == Guid.Empty)
            throw new ArgumentException("Invalid record UUID", nameof(recordId));
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = $"SELECT sync_revision FROM {entityTable} WHERE sync_uuid = $uuid LIMIT 1";
        command.Parameters.AddWithValue("$uuid", recordId);
        var revision = command.ExecuteScalar();
        if (revision is not null && revision is not DBNull)
            return new(entityTable, recordId, Convert.ToInt64(revision), false);
        using var deleted = db.CreateCommand();
        deleted.CommandText = "SELECT sync_revision FROM sync_tombstones WHERE entity_table = $table AND sync_uuid = $uuid LIMIT 1";
        deleted.Parameters.AddWithValue("$table", entityTable);
        deleted.Parameters.AddWithValue("$uuid", recordId);
        revision = deleted.ExecuteScalar();
        return revision is null || revision is DBNull ? null
            : new(entityTable, recordId, Convert.ToInt64(revision), true);
    }
}
