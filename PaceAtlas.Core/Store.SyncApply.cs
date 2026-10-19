using Microsoft.Data.Sqlite;
using System.Text.Json;
using PaceAtlas.Sync;

namespace PaceAtlas;

/// <summary>Explicit, local-only atomic application for the entries entity.
/// No network endpoint invokes this method. Other entity types remain unsupported.</summary>
public sealed record SyncEntryPayload(string Kind, string Start, string? End, string Data, string Note);
public sealed record SyncApplyReceipt(Guid OperationId, SyncDecision Decision, string Reason, long? Revision);

public sealed partial class Store
{
    private static void InitializeSyncApply(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS sync_applied_operations (
                operation_uuid TEXT PRIMARY KEY,
                entity_table TEXT NOT NULL,
                record_uuid TEXT NOT NULL,
                mutation_kind TEXT NOT NULL,
                resulting_revision INTEGER NOT NULL,
                applied_at_utc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>Applies a single entries mutation in one IMMEDIATE transaction.
    /// Repeated operation IDs never apply twice. Staged proposals are not treated as commits.
    /// Caller must authenticate and authorize the owner before invoking this in a future server.</summary>
    public SyncApplyReceipt ApplyIncomingEntryMutation(SyncMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (mutation.OperationId == Guid.Empty || mutation.RecordId == Guid.Empty)
            return new(mutation.OperationId, SyncDecision.Invalid, "Missing identifier", null);

        SyncEntryPayload? payload = null;
        if (mutation.Kind != SyncMutationKind.Delete)
        {
            try { payload = JsonSerializer.Deserialize<SyncEntryPayload>(mutation.Payload ?? ""); }
            catch (JsonException) { return new(mutation.OperationId, SyncDecision.Invalid, "Malformed entry payload", null); }
            if (payload is null || string.IsNullOrWhiteSpace(payload.Kind) ||
                string.IsNullOrWhiteSpace(payload.Start) || string.IsNullOrWhiteSpace(payload.Data) ||
                !DateTimeOffset.TryParse(payload.Start, out _) ||
                (payload.End is not null && !DateTimeOffset.TryParse(payload.End, out _)))
                return new(mutation.OperationId, SyncDecision.Invalid, "Incomplete entry payload", null);
            try { using var parsed = JsonDocument.Parse(payload.Data); }
            catch (JsonException) { return new(mutation.OperationId, SyncDecision.Invalid, "Malformed entry data", null); }
        }

        using var db = Open();
        using var tx = db.BeginTransaction(deferred: false);
        using (var duplicate = db.CreateCommand())
        {
            duplicate.Transaction = tx;
            duplicate.CommandText = "SELECT resulting_revision FROM sync_applied_operations WHERE operation_uuid=$op";
            duplicate.Parameters.AddWithValue("$op", mutation.OperationId.ToString("D"));
            var prior = duplicate.ExecuteScalar();
            if (prior is not null && prior is not DBNull)
            {
                tx.Commit();
                return new(mutation.OperationId, SyncDecision.AlreadyApplied, "Already committed", Convert.ToInt64(prior));
            }
        }
        long? revision = null;
        var deleted = false;
        using (var current = db.CreateCommand())
        {
            current.Transaction = tx;
            current.CommandText = "SELECT sync_revision FROM entries WHERE sync_uuid=$id";
            current.Parameters.AddWithValue("$id", mutation.RecordId.ToString("D"));
            var value = current.ExecuteScalar();
            if (value is not null && value is not DBNull) revision = Convert.ToInt64(value);
        }
        if (revision is null)
        {
            using var tombstone = db.CreateCommand();
            tombstone.Transaction = tx;
            tombstone.CommandText = "SELECT sync_revision FROM sync_tombstones WHERE entity_table='entries' AND sync_uuid=$id";
            tombstone.Parameters.AddWithValue("$id", mutation.RecordId.ToString("D"));
            var value = tombstone.ExecuteScalar();
            if (value is not null && value is not DBNull) { revision = Convert.ToInt64(value); deleted = true; }
        }
        var decision = SyncPreconditions.Evaluate(mutation,
            revision is null ? null : new SyncRecordState(mutation.RecordId, revision.Value, deleted), false);
        if (decision.Decision != SyncDecision.Accept)
        {
            tx.Commit();
            return new(mutation.OperationId, decision.Decision, decision.Reason, revision);
        }

        using (var apply = db.CreateCommand())
        {
            apply.Transaction = tx;
            apply.Parameters.AddWithValue("$id", mutation.RecordId.ToString("D"));
            if (mutation.Kind == SyncMutationKind.Create)
            {
                apply.CommandText = "INSERT INTO entries(kind,start,end,data,note,sync_uuid) VALUES($kind,$start,$end,$data,$note,$id)";
            }
            else if (mutation.Kind == SyncMutationKind.Update)
            {
                apply.CommandText = "UPDATE entries SET kind=$kind,start=$start,end=$end,data=$data,note=$note WHERE sync_uuid=$id";
            }
            else
            {
                apply.CommandText = "DELETE FROM entries WHERE sync_uuid=$id";
            }
            if (payload is not null)
            {
                apply.Parameters.AddWithValue("$kind", payload.Kind);
                apply.Parameters.AddWithValue("$start", payload.Start);
                apply.Parameters.AddWithValue("$end", (object?)payload.End ?? DBNull.Value);
                apply.Parameters.AddWithValue("$data", payload.Data);
                apply.Parameters.AddWithValue("$note", payload.Note ?? "");
            }
            if (apply.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("Unexpected affected row count; transaction rolled back");
        }
        var newRevision = mutation.Kind == SyncMutationKind.Create ? 1 : revision!.Value + 1;
        using (var receipt = db.CreateCommand())
        {
            receipt.Transaction = tx;
            receipt.CommandText = """
                INSERT INTO sync_applied_operations(operation_uuid,entity_table,record_uuid,mutation_kind,resulting_revision,applied_at_utc)
                VALUES($op,'entries',$id,$kind,$rev,strftime('%Y-%m-%dT%H:%M:%fZ','now'))
                """;
            receipt.Parameters.AddWithValue("$op", mutation.OperationId.ToString("D"));
            receipt.Parameters.AddWithValue("$id", mutation.RecordId.ToString("D"));
            receipt.Parameters.AddWithValue("$kind", mutation.Kind.ToString());
            receipt.Parameters.AddWithValue("$rev", newRevision);
            receipt.ExecuteNonQuery();
        }
        tx.Commit();
        return new(mutation.OperationId, SyncDecision.Accept, "Committed", newRevision);
    }
}
