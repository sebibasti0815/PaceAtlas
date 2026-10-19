using Microsoft.Data.Sqlite;

namespace PaceAtlas;

public sealed partial class Store
{
    // Change tracking is deliberately metadata-only: no medical values enter the journal.
    // The journal is local preparation, NOT an active sync implementation.
    private static void InitializeSyncTracking(SqliteConnection db)
    {
        using var tx = db.BeginTransaction();
        void Execute(string sql)
        {
            using var command = db.CreateCommand();
            command.Transaction = tx;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
        Execute("""
            CREATE TABLE IF NOT EXISTS sync_tombstones (
                entity_table TEXT NOT NULL,
                sync_uuid TEXT NOT NULL,
                sync_revision INTEGER NOT NULL,
                deleted_at_utc TEXT NOT NULL,
                PRIMARY KEY (entity_table, sync_uuid)
            );
            CREATE TABLE IF NOT EXISTS sync_change_journal (
                sequence INTEGER PRIMARY KEY AUTOINCREMENT,
                event_uuid TEXT NOT NULL UNIQUE,
                entity_table TEXT NOT NULL,
                sync_uuid TEXT NOT NULL,
                sync_revision INTEGER NOT NULL,
                operation TEXT NOT NULL CHECK(operation IN ('create','update','delete')),
                changed_at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_sync_journal_entity
                ON sync_change_journal(entity_table, sync_uuid, sequence);
            """);
        foreach (var table in SyncIdentityTables)
        {
            // Only the fixed internal table-name allowlist is interpolated into SQL.
            var columns = new List<string>();
            using (var info = db.CreateCommand())
            {
                info.Transaction = tx;
                info.CommandText = $"PRAGMA table_info({table})";
                using var reader = info.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(1);
                    if (!name.Equals("sync_uuid", StringComparison.OrdinalIgnoreCase) &&
                        !name.Equals("sync_revision", StringComparison.OrdinalIgnoreCase))
                        columns.Add("\"" + name.Replace("\"", "\"\"") + "\"");
                }
            }
            if (columns.Count == 0)
                throw new InvalidOperationException($"Cannot track table without columns: {table}");
            Execute($"""
                CREATE TRIGGER IF NOT EXISTS tr_{table}_sync_create_explicit
                AFTER INSERT ON {table}
                WHEN NEW.sync_uuid IS NOT NULL AND NEW.sync_uuid <> ''
                BEGIN
                    INSERT INTO sync_change_journal(event_uuid, entity_table, sync_uuid, sync_revision, operation, changed_at_utc)
                    VALUES (lower(hex(randomblob(16))), '{table}', NEW.sync_uuid, NEW.sync_revision, 'create', strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                END;
                """);
            // The identity migration's AFTER INSERT trigger fills UUIDs via UPDATE. This
            // specialized trigger records that insertion without counting it as a user edit.
            Execute($"""
                CREATE TRIGGER IF NOT EXISTS tr_{table}_sync_create_generated
                AFTER UPDATE OF sync_uuid ON {table}
                WHEN (OLD.sync_uuid IS NULL OR OLD.sync_uuid = '') AND NEW.sync_uuid IS NOT NULL AND NEW.sync_uuid <> ''
                BEGIN
                    INSERT INTO sync_change_journal(event_uuid, entity_table, sync_uuid, sync_revision, operation, changed_at_utc)
                    VALUES (lower(hex(randomblob(16))), '{table}', NEW.sync_uuid, NEW.sync_revision, 'create', strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                END;
                """);
            Execute($"""
                CREATE TRIGGER IF NOT EXISTS tr_{table}_sync_update
                AFTER UPDATE OF {string.Join(", ", columns)} ON {table}
                WHEN NEW.sync_uuid IS NOT NULL AND NEW.sync_uuid <> ''
                BEGIN
                    UPDATE {table} SET sync_revision = OLD.sync_revision + 1 WHERE id = NEW.id;
                    INSERT INTO sync_change_journal(event_uuid, entity_table, sync_uuid, sync_revision, operation, changed_at_utc)
                    VALUES (lower(hex(randomblob(16))), '{table}', NEW.sync_uuid, OLD.sync_revision + 1, 'update', strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                END;
                """);
            Execute($"""
                CREATE TRIGGER IF NOT EXISTS tr_{table}_sync_delete
                BEFORE DELETE ON {table}
                WHEN OLD.sync_uuid IS NOT NULL AND OLD.sync_uuid <> ''
                BEGIN
                    INSERT INTO sync_tombstones(entity_table, sync_uuid, sync_revision, deleted_at_utc)
                    VALUES ('{table}', OLD.sync_uuid, OLD.sync_revision + 1, strftime('%Y-%m-%dT%H:%M:%fZ','now'))
                    ON CONFLICT(entity_table, sync_uuid) DO UPDATE SET
                        sync_revision = max(sync_revision, excluded.sync_revision),
                        deleted_at_utc = excluded.deleted_at_utc;
                    INSERT INTO sync_change_journal(event_uuid, entity_table, sync_uuid, sync_revision, operation, changed_at_utc)
                    VALUES (lower(hex(randomblob(16))), '{table}', OLD.sync_uuid, OLD.sync_revision + 1, 'delete', strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                END;
                """);
        }
        tx.Commit();
    }
}
