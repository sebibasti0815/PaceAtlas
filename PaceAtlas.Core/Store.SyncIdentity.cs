using Microsoft.Data.Sqlite;

namespace PaceAtlas;

public sealed partial class Store
{
    // Local schema preparation only. Deletion tombstones and the sync journal follow in a later release.
    private static readonly string[] SyncIdentityTables =
    [
        "entries", "medication_plan", "medication_products", "stock_movements",
        "foods", "food_rules", "meals", "ai_analyses"
    ];

    private static void InitializeSyncIdentities(SqliteConnection db)
    {
        // Preserve a consistent copy of the pre-migration database once. The SQLite backup API
        // also handles databases using WAL; copying the .db file alone would not be safe.
        using (var check = db.CreateCommand())
        {
            check.CommandText = "PRAGMA table_info(entries)";
            var hasUuid = false;
            using (var reader = check.ExecuteReader())
                while (reader.Read())
                    if (reader.GetString(1) == "sync_uuid") hasUuid = true;
            if (!hasUuid && File.Exists(Database))
            {
                var backupPath = Database + ".pre-sync-identity.bak";
                if (!File.Exists(backupPath))
                {
                    using var backup = new SqliteConnection(new SqliteConnectionStringBuilder
                    {
                        DataSource = backupPath, Mode = SqliteOpenMode.ReadWriteCreate
                    }.ToString());
                    backup.Open();
                    db.BackupDatabase(backup);
                }
            }
        }

        using var transaction = db.BeginTransaction();
        foreach (var table in SyncIdentityTables)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var info = db.CreateCommand())
            {
                info.Transaction = transaction;
                info.CommandText = $"PRAGMA table_info({table})";
                using var reader = info.ExecuteReader();
                while (reader.Read()) names.Add(reader.GetString(1));
            }
            if (names.Count == 0) throw new InvalidOperationException($"Missing table for identity migration: {table}");
            foreach (var (column, sqlType) in new[]
            {
                ("sync_uuid", "TEXT"), ("sync_revision", "INTEGER NOT NULL DEFAULT 1")
            })
            {
                if (names.Contains(column)) continue;
                using var alter = db.CreateCommand();
                alter.Transaction = transaction;
                alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {sqlType}";
                alter.ExecuteNonQuery();
            }
            using (var read = db.CreateCommand())
            {
                read.Transaction = transaction;
                read.CommandText = $"SELECT id FROM {table} WHERE sync_uuid IS NULL OR sync_uuid = ''";
                var ids = new List<long>();
                using (var reader = read.ExecuteReader())
                    while (reader.Read()) ids.Add(reader.GetInt64(0));
                foreach (var id in ids)
                {
                    using var update = db.CreateCommand();
                    update.Transaction = transaction;
                    update.CommandText = $"UPDATE {table} SET sync_uuid = $uuid WHERE id = $id AND (sync_uuid IS NULL OR sync_uuid = '')";
                    update.Parameters.AddWithValue("$uuid", Guid.NewGuid().ToString("D"));
                    update.Parameters.AddWithValue("$id", id);
                    update.ExecuteNonQuery();
                }
            }
            using var index = db.CreateCommand();
            index.Transaction = transaction;
            index.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS ix_{table}_sync_uuid ON {table}(sync_uuid)";
            index.ExecuteNonQuery();
            // SQLite-side UUID v4 generation covers existing insertion paths without requiring
            // changes to every WinUI, IPC and Raycast caller. No client-side numeric ID reuse.
            using var trigger = db.CreateCommand();
            trigger.Transaction = transaction;
            trigger.CommandText = $"""
                CREATE TRIGGER IF NOT EXISTS tr_{table}_sync_uuid_insert
                AFTER INSERT ON {table}
                WHEN NEW.sync_uuid IS NULL OR NEW.sync_uuid = ''
                BEGIN
                    UPDATE {table} SET sync_uuid = (
                        lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' ||
                        substr(lower(hex(randomblob(2))),2,3) || '-' ||
                        substr('89ab', 1 + (abs(random()) % 4), 1) ||
                        substr(lower(hex(randomblob(2))),2,3) || '-' ||
                        lower(hex(randomblob(6)))
                    ) WHERE id = NEW.id;
                END;
                """;
            trigger.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
