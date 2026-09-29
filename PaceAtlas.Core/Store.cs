using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Json;

namespace PaceAtlas;
public sealed class MedicationPlan
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string Time { get; set; } = "";
    public string StartDate { get; set; } = "0001-01-01";
    public string? EndDate { get; set; }
    public bool IsActiveOn(DateOnly day) =>
        string.CompareOrdinal(StartDate, day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)) <= 0 &&
        (EndDate is null || string.CompareOrdinal(EndDate, day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)) >= 0);
    public string Name { get; set; } = "";
    public string Dose { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Form { get; set; } = "";
    public List<string> Goals { get; set; } = new();
}
public sealed class IntakeData
{
    public long PlanId { get; set; }
    public long ProductId { get; set; }
    public string Name { get; set; } = "";
    public string PlannedDose { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string Form { get; set; } = "";
    public List<string> Goals { get; set; } = new();
    public string ActualDose { get; set; } = "";
    public string ActualQuantity { get; set; } = "";
    public string Status { get; set; } = "";
}
public sealed class MedicationProduct
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Dose { get; set; } = "";
    public string Form { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Supplier { get; set; } = "";
    public decimal PackUnits { get; set; }
    public decimal PackPrice { get; set; }
}
public sealed class ProductStock
{
    public MedicationProduct Product { get; set; } = new();
    public decimal Current { get; set; }
    public decimal WeeklyNeed { get; set; }
    public bool StockKnown { get; set; }
}
public sealed class StockMovement
{
    public long Id { get; set; }
    public string Day { get; set; } = "";
    public string Kind { get; set; } = "";
    public decimal Units { get; set; }
    public decimal Packages { get; set; }
    public decimal Cost { get; set; }
}
public sealed class MeasureData
{
    public string Name { get; set; } = "";
    public string Dose { get; set; } = "";
    public bool TrackProgress { get; set; }
    public List<string> ReasonSymptoms { get; set; } = new();
    public string ReasonOther { get; set; } = "";
    public string Goal { get; set; } = "";
    public string Baseline { get; set; } = "";
    public DateTime? ReviewDate { get; set; }
    public string Status { get; set; } = "active";
    public string Outcome { get; set; } = "";
    public string ReviewNote { get; set; } = "";
    public DateTime? ReviewedAt { get; set; }
}
public sealed class SavedAiAnalysis
{
    public string Model { get; set; } = "";
    public string SnapshotHash { get; set; } = "";
    public DateTime Created { get; set; }
    public string Response { get; set; } = "";
}
public sealed class Store
{
    private static readonly string[] DefaultGoalNames = ["Antidepressiv", "Angststörung", "Antioxidant", "Herzfrequenz", "Bluthochdruck", "ME/CFS (Mitochondrien)", "Schmerzen", "Leaky Gut", "Antihistamin", "ME/CFS (allg. Schmerzen)", "ME/CFS (Muskelschwäche)", "ME/CFS (Erschöpfung)", "ME/CFS (Verdauung)", "entzündungshemmend", "Borreliose", "Darmaufbau", "Wassereinlagerung", "ME/CFS (allg.)", "Gewicht", "Brain Fog", "Schwindel", "Testosteron", "Schlafstörung"];
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaceAtlas");
    public static string Database => Path.Combine(Folder, "paceatlas.db");
    public Store() { Directory.CreateDirectory(Folder); Initialize(); }
    private SqliteConnection Open()
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Database, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        db.Open();
        return db;
    }
    private void Initialize()
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS entries (id INTEGER PRIMARY KEY, kind TEXT NOT NULL, start TEXT NOT NULL, end TEXT, data TEXT NOT NULL, note TEXT NOT NULL DEFAULT ''); CREATE INDEX IF NOT EXISTS ix_entries_start ON entries(start); CREATE TABLE IF NOT EXISTS medication_plan (id INTEGER PRIMARY KEY, time TEXT NOT NULL, name TEXT NOT NULL, dose TEXT NOT NULL, form TEXT NOT NULL DEFAULT '', quantity TEXT NOT NULL DEFAULT '', goals TEXT NOT NULL DEFAULT '[]', product_id INTEGER NOT NULL DEFAULT 0); CREATE TABLE IF NOT EXISTS goal_options(name TEXT PRIMARY KEY); CREATE TABLE IF NOT EXISTS app_settings(key TEXT PRIMARY KEY, value TEXT NOT NULL); CREATE TABLE IF NOT EXISTS medication_products(id INTEGER PRIMARY KEY, name TEXT NOT NULL COLLATE NOCASE, dose TEXT NOT NULL COLLATE NOCASE, form TEXT NOT NULL COLLATE NOCASE, manufacturer TEXT NOT NULL DEFAULT '', supplier TEXT NOT NULL DEFAULT '', pack_units REAL NOT NULL DEFAULT 0, pack_price REAL NOT NULL DEFAULT 0, UNIQUE(name,dose,form)); CREATE TABLE IF NOT EXISTS stock_movements(id INTEGER PRIMARY KEY, product_id INTEGER NOT NULL, day TEXT NOT NULL, kind TEXT NOT NULL, units REAL NOT NULL, packages REAL NOT NULL DEFAULT 0, total_cost REAL NOT NULL DEFAULT 0); CREATE INDEX IF NOT EXISTS ix_stock_product ON stock_movements(product_id);";
        command.ExecuteNonQuery();
        using var analyses = db.CreateCommand();
        analyses.CommandText = "CREATE TABLE IF NOT EXISTS ai_analyses (id INTEGER PRIMARY KEY, period INTEGER NOT NULL, language TEXT NOT NULL, model TEXT NOT NULL, snapshot_hash TEXT NOT NULL, created TEXT NOT NULL, response TEXT NOT NULL); CREATE INDEX IF NOT EXISTS ix_ai_analyses_lookup ON ai_analyses(period,language,id DESC)";
        analyses.ExecuteNonQuery();
        using var columns = db.CreateCommand();
        columns.CommandText = "PRAGMA table_info(medication_plan)";
        using var reader = columns.ExecuteReader();
        bool hasForm = false, hasQuantity = false, hasGoals = false, hasProductId = false, hasStartDate = false, hasEndDate = false;
        while (reader.Read())
        {
            if (reader.GetString(1) == "form") hasForm = true;
            if (reader.GetString(1) == "quantity") hasQuantity = true;
            if (reader.GetString(1) == "goals") hasGoals = true;
            if (reader.GetString(1) == "product_id") hasProductId = true;
            if (reader.GetString(1) == "start_date") hasStartDate = true;
            if (reader.GetString(1) == "end_date") hasEndDate = true;
        }
        reader.Close();
        if (!hasForm)
        {
            using var migrate = db.CreateCommand();
            migrate.CommandText = "ALTER TABLE medication_plan ADD COLUMN form TEXT NOT NULL DEFAULT ''";
            migrate.ExecuteNonQuery();
        }
        if (!hasQuantity)
        {
            using var migrate = db.CreateCommand();
            migrate.CommandText = "ALTER TABLE medication_plan ADD COLUMN quantity TEXT NOT NULL DEFAULT ''";
            migrate.ExecuteNonQuery();
        }
        if (!hasGoals)
        {
            using var migrate = db.CreateCommand();
            migrate.CommandText = "ALTER TABLE medication_plan ADD COLUMN goals TEXT NOT NULL DEFAULT '[]'";
            migrate.ExecuteNonQuery();
        }
        if (!hasProductId)
        {
            using var migrate = db.CreateCommand();
            migrate.CommandText = "ALTER TABLE medication_plan ADD COLUMN product_id INTEGER NOT NULL DEFAULT 0";
            migrate.ExecuteNonQuery();
        }
        if (!hasStartDate)
        {
            using var migrate = db.CreateCommand();
            migrate.CommandText = "ALTER TABLE medication_plan ADD COLUMN start_date TEXT NOT NULL DEFAULT '0001-01-01'";
            migrate.ExecuteNonQuery();
        }
        if (!hasEndDate)
        {
            using var migrate = db.CreateCommand();
            migrate.CommandText = "ALTER TABLE medication_plan ADD COLUMN end_date TEXT";
            migrate.ExecuteNonQuery();
        }
        using var seeded = db.CreateCommand();
        seeded.CommandText = "SELECT COUNT(*) FROM app_settings WHERE key='goals_seeded'";
        if (Convert.ToInt32(seeded.ExecuteScalar()) == 0)
        {
            using var transaction = db.BeginTransaction();
            foreach (var name in DefaultGoalNames)
            {
                using var insert = db.CreateCommand(); insert.Transaction = transaction;
                insert.CommandText = "INSERT OR IGNORE INTO goal_options(name) VALUES($name)";
                insert.Parameters.AddWithValue("$name", name); insert.ExecuteNonQuery();
            }
            using var mark = db.CreateCommand(); mark.Transaction = transaction;
            mark.CommandText = "INSERT INTO app_settings(key,value) VALUES('goals_seeded','1')";
            mark.ExecuteNonQuery(); transaction.Commit();
        }
        var unlinked = new List<(long Id, string Name, string Dose, string Form)>();
        using (var read = db.CreateCommand())
        {
            read.CommandText = "SELECT id,name,dose,form FROM medication_plan WHERE product_id=0";
            using var items = read.ExecuteReader();
            while (items.Read()) unlinked.Add((items.GetInt64(0), items.GetString(1), items.GetString(2), items.GetString(3)));
        }
        foreach (var (id, name, dose, form) in unlinked)
        {
            long productId = EnsureProduct(db, null, name, dose, form);
            using var update = db.CreateCommand();
            update.CommandText = "UPDATE medication_plan SET product_id=$product WHERE id=$id";
            update.Parameters.AddWithValue("$product", productId); update.Parameters.AddWithValue("$id", id);
            update.ExecuteNonQuery();
        }
        ConsolidateProducts(db);
        PruneUnusedProducts(db);
    }
    private static void PruneUnusedProducts(SqliteConnection db)
    {
        // Intake records keep their product identity, even if their plan was later edited.
        var historicIds = new HashSet<long>();
        using (var read = db.CreateCommand())
        {
            read.CommandText = "SELECT data FROM entries WHERE kind='Einnahme'";
            using var reader = read.ExecuteReader();
            while (reader.Read())
            {
                var intake = JsonSerializer.Deserialize<IntakeData>(reader.GetString(0));
                if (intake is not null && intake.ProductId != 0) historicIds.Add(intake.ProductId);
            }
        }
        using var transaction = db.BeginTransaction();
        using var remove = db.CreateCommand(); remove.Transaction = transaction;
        remove.CommandText = "DELETE FROM medication_products WHERE id=$id AND NOT EXISTS (SELECT 1 FROM medication_plan WHERE product_id=$id) AND NOT EXISTS (SELECT 1 FROM stock_movements WHERE product_id=$id) AND manufacturer='' AND supplier='' AND pack_units=0 AND pack_price=0";
        using var candidates = db.CreateCommand(); candidates.Transaction = transaction;
        candidates.CommandText = "SELECT id FROM medication_products";
        var ids = new List<long>();
        using (var reader = candidates.ExecuteReader())
            while (reader.Read()) ids.Add(reader.GetInt64(0));
        foreach (var id in ids.Where(id => !historicIds.Contains(id)))
        {
            remove.Parameters.Clear(); remove.Parameters.AddWithValue("$id", id);
            remove.ExecuteNonQuery();
        }
        transaction.Commit();
    }
    private static void ConsolidateProducts(SqliteConnection db)
    {
        var products = new List<MedicationProduct>();
        using (var read = db.CreateCommand())
        {
            read.CommandText = "SELECT id,name,dose,form,manufacturer,supplier,pack_units,pack_price FROM medication_products";
            using var reader = read.ExecuteReader();
            while (reader.Read()) products.Add(new MedicationProduct { Id = reader.GetInt64(0), Name = reader.GetString(1),
                Dose = reader.GetString(2), Form = reader.GetString(3), Manufacturer = reader.GetString(4),
                Supplier = reader.GetString(5), PackUnits = Convert.ToDecimal(reader.GetValue(6)), PackPrice = Convert.ToDecimal(reader.GetValue(7)) });
        }
        var replacements = new Dictionary<long,long>();
        using var transaction = db.BeginTransaction();
        foreach (var group in products.GroupBy(p => (p.Name.Trim().ToUpperInvariant(), p.Form.Trim().ToUpperInvariant())).Where(g => g.Count() > 1))
        {
            var primary = group.OrderByDescending(p => p.PackUnits > 0).ThenBy(p => p.Id).First();
            foreach (var other in group.Where(p => p.Id != primary.Id))
            {
                replacements[other.Id] = primary.Id;
                if (primary.Manufacturer.Length == 0) primary.Manufacturer = other.Manufacturer;
                if (primary.Supplier.Length == 0) primary.Supplier = other.Supplier;
                if (primary.PackUnits == 0) primary.PackUnits = other.PackUnits;
                if (primary.PackPrice == 0) primary.PackPrice = other.PackPrice;
                foreach (var table in new[] { "medication_plan", "stock_movements" })
                {
                    using var update = db.CreateCommand(); update.Transaction = transaction;
                    update.CommandText = $"UPDATE {table} SET product_id=$to WHERE product_id=$from";
                    update.Parameters.AddWithValue("$to", primary.Id); update.Parameters.AddWithValue("$from", other.Id);
                    update.ExecuteNonQuery();
                }
                using var delete = db.CreateCommand(); delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM medication_products WHERE id=$id";
                delete.Parameters.AddWithValue("$id", other.Id); delete.ExecuteNonQuery();
            }
            using var details = db.CreateCommand(); details.Transaction = transaction;
            details.CommandText = "UPDATE medication_products SET manufacturer=$manufacturer,supplier=$supplier,pack_units=$units,pack_price=$price WHERE id=$id";
            details.Parameters.AddWithValue("$manufacturer", primary.Manufacturer); details.Parameters.AddWithValue("$supplier", primary.Supplier);
            details.Parameters.AddWithValue("$units", (double)primary.PackUnits); details.Parameters.AddWithValue("$price", (double)primary.PackPrice);
            details.Parameters.AddWithValue("$id", primary.Id); details.ExecuteNonQuery();
        }
        if (replacements.Count > 0)
        {
            var changed = new List<(long Id, string Data)>();
            using (var read = db.CreateCommand())
            {
                read.Transaction = transaction; read.CommandText = "SELECT id,data FROM entries WHERE kind='Einnahme'";
                using var reader = read.ExecuteReader();
                while (reader.Read())
                {
                    var data = JsonSerializer.Deserialize<IntakeData>(reader.GetString(1));
                    if (data is null || !replacements.TryGetValue(data.ProductId, out var targetId)) continue;
                    data.ProductId = targetId; changed.Add((reader.GetInt64(0), JsonSerializer.Serialize(data)));
                }
            }
            foreach (var (id, data) in changed)
            {
                using var update = db.CreateCommand(); update.Transaction = transaction;
                update.CommandText = "UPDATE entries SET data=$data WHERE id=$id";
                update.Parameters.AddWithValue("$data", data); update.Parameters.AddWithValue("$id", id);
                update.ExecuteNonQuery();
            }
        }
        transaction.Commit();
    }
    private static long EnsureProduct(SqliteConnection db, SqliteTransaction? transaction, string name, string dose, string form)
    {
        using (var find = db.CreateCommand())
        {
            find.Transaction = transaction;
            find.CommandText = "SELECT id FROM medication_products WHERE name=$name AND form=$form ORDER BY id LIMIT 1";
            find.Parameters.AddWithValue("$name", name); find.Parameters.AddWithValue("$dose", dose); find.Parameters.AddWithValue("$form", form);
            if (find.ExecuteScalar() is long id) return id;
        }
        using var create = db.CreateCommand(); create.Transaction = transaction;
        create.CommandText = "INSERT INTO medication_products(name,dose,form) VALUES($name,$dose,$form)";
        create.Parameters.AddWithValue("$name", name); create.Parameters.AddWithValue("$dose", dose); create.Parameters.AddWithValue("$form", form);
        create.ExecuteNonQuery();
        using var last = db.CreateCommand(); last.Transaction = transaction;
        last.CommandText = "SELECT last_insert_rowid()";
        return (long)last.ExecuteScalar()!;
    }
    public List<string> GoalOptions()
    {
        var names = new List<string>();
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT name FROM goal_options";
        using var reader = command.ExecuteReader();
        while (reader.Read()) names.Add(reader.GetString(0));
        return names.OrderBy(n => n, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("de-DE"), true)).ToList();
    }
    public void SaveAiAnalysis(int period, string language, string model, string snapshotHash, string response)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO ai_analyses(period,language,model,snapshot_hash,created,response) VALUES($period,$language,$model,$hash,$created,$response)";
        command.Parameters.AddWithValue("$period", period);
        command.Parameters.AddWithValue("$language", language);
        command.Parameters.AddWithValue("$model", model);
        command.Parameters.AddWithValue("$hash", snapshotHash);
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$response", response);
        command.ExecuteNonQuery();
    }
    public SavedAiAnalysis? LatestAiAnalysis(int period, string language)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT model,snapshot_hash,created,response FROM ai_analyses WHERE period=$period AND language=$language ORDER BY id DESC LIMIT 1";
        command.Parameters.AddWithValue("$period", period);
        command.Parameters.AddWithValue("$language", language);
        using var reader = command.ExecuteReader();
        return reader.Read() ? new SavedAiAnalysis { Model = reader.GetString(0), SnapshotHash = reader.GetString(1),
            Created = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind), Response = reader.GetString(3) } : null;
    }
    public List<string> ChoiceOptions(string key, IEnumerable<string> defaults)
    {
        if (key is not ("measures" or "activity_dimensions" or "rest_dimensions" or "symptoms" or "pain_locations" or "hearing_protection")) throw new ArgumentException("Unknown choice list", nameof(key));
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT value FROM app_settings WHERE key=$key";
        command.Parameters.AddWithValue("$key", key);
        var value = command.ExecuteScalar() as string;
        if (value is null) return defaults.ToList();
        try { return JsonSerializer.Deserialize<List<string>>(value) ?? defaults.ToList(); }
        catch (JsonException) { return defaults.ToList(); }
    }
    public void SetChoiceOptions(string key, IEnumerable<string> values)
    {
        if (key is not ("measures" or "activity_dimensions" or "rest_dimensions" or "symptoms" or "pain_locations" or "hearing_protection")) throw new ArgumentException("Unknown choice list", nameof(key));
        var names = values.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO app_settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(names));
        command.ExecuteNonQuery();
    }
    public bool AddGoalOption(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || GoalOptions().Any(n => string.Equals(n, name, StringComparison.CurrentCultureIgnoreCase))) return false;
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO goal_options(name) VALUES($name)";
        command.Parameters.AddWithValue("$name", name); command.ExecuteNonQuery();
        return true;
    }
    public void DeleteGoalOption(string name)
    {
        using var db = Open(); using var transaction = db.BeginTransaction();
        var changed = new List<(long Id, string Goals)>();
        using (var get = db.CreateCommand())
        {
            get.Transaction = transaction;
            get.CommandText = "SELECT id,goals FROM medication_plan";
            using var reader = get.ExecuteReader();
            while (reader.Read())
            {
                var goals = JsonSerializer.Deserialize<List<string>>(reader.GetString(1)) ?? new();
                if (goals.RemoveAll(g => g == name) > 0) changed.Add((reader.GetInt64(0), JsonSerializer.Serialize(goals)));
            }
        }
        foreach (var (id, goals) in changed)
        {
            using var update = db.CreateCommand(); update.Transaction = transaction;
            update.CommandText = "UPDATE medication_plan SET goals=$goals WHERE id=$id";
            update.Parameters.AddWithValue("$goals", goals); update.Parameters.AddWithValue("$id", id);
            update.ExecuteNonQuery();
        }
        using var delete = db.CreateCommand(); delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM goal_options WHERE name=$name";
        delete.Parameters.AddWithValue("$name", name); delete.ExecuteNonQuery();
        transaction.Commit();
    }
    public List<MedicationPlan> MedicationPlans()
    {
        var plans = new List<MedicationPlan>();
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT id,time,name,dose,form,quantity,goals,product_id,start_date,end_date FROM medication_plan ORDER BY time,name,id";
        using var reader = command.ExecuteReader();
        while (reader.Read()) plans.Add(new MedicationPlan { Id = reader.GetInt64(0), Time = reader.GetString(1), Name = reader.GetString(2), Dose = reader.GetString(3), Form = reader.GetString(4), Quantity = reader.GetString(5), Goals = System.Text.Json.JsonSerializer.Deserialize<List<string>>(reader.GetString(6)) ?? new(), ProductId = reader.GetInt64(7), StartDate = reader.GetString(8), EndDate = reader.IsDBNull(9) ? null : reader.GetString(9) });
        return plans;
    }
    public void SaveMedicationPlan(MedicationPlan plan)
    {
        using var db = Open(); using var command = db.CreateCommand();
        long productId = EnsureProduct(db, null, plan.Name, plan.Dose, plan.Form);
        command.CommandText = plan.Id == 0 ? "INSERT INTO medication_plan(time,name,dose,form,quantity,goals,product_id,start_date,end_date) VALUES($time,$name,$dose,$form,$quantity,$goals,$product,$start,$end)" : "UPDATE medication_plan SET time=$time,name=$name,dose=$dose,form=$form,quantity=$quantity,goals=$goals,product_id=$product,start_date=$start,end_date=$end WHERE id=$id";
        command.Parameters.AddWithValue("$id", plan.Id);
        command.Parameters.AddWithValue("$time", plan.Time); command.Parameters.AddWithValue("$name", plan.Name); command.Parameters.AddWithValue("$dose", plan.Dose);
        command.Parameters.AddWithValue("$form", plan.Form);
        command.Parameters.AddWithValue("$quantity", plan.Quantity);
        command.Parameters.AddWithValue("$goals", System.Text.Json.JsonSerializer.Serialize(plan.Goals));
        command.Parameters.AddWithValue("$product", productId);
        command.Parameters.AddWithValue("$start", plan.StartDate);
        command.Parameters.AddWithValue("$end", (object?)plan.EndDate ?? DBNull.Value);
        command.ExecuteNonQuery();
        PruneUnusedProducts(db);
    }
    public void DeleteMedicationPlan(long id)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "DELETE FROM medication_plan WHERE id=$id";
        command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery();
        PruneUnusedProducts(db);
    }
    private static decimal Number(string text) =>
        decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.GetCultureInfo("de-DE"), out var value)
            ? value : decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : 0;
    public List<ProductStock> ProductStocks()
    {
        var result = new List<ProductStock>();
        var plans = MedicationPlans();
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT p.id,p.name,p.dose,p.form,p.manufacturer,p.supplier,p.pack_units,p.pack_price, COALESCE(SUM(m.units),0), COALESCE(SUM(CASE WHEN m.kind='Nachkauf' THEN m.total_cost ELSE 0 END),0), COALESCE(MAX(CASE WHEN m.kind IN ('Nachkauf','Korrektur') THEN 1 ELSE 0 END),0) FROM medication_products p LEFT JOIN stock_movements m ON m.product_id=p.id GROUP BY p.id ORDER BY p.name,p.dose,p.form";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var product = new MedicationProduct { Id = reader.GetInt64(0), Name = reader.GetString(1), Dose = reader.GetString(2), Form = reader.GetString(3), Manufacturer = reader.GetString(4), Supplier = reader.GetString(5), PackUnits = Convert.ToDecimal(reader.GetValue(6)), PackPrice = Convert.ToDecimal(reader.GetValue(7)) };
            result.Add(new ProductStock { Product = product, Current = Convert.ToDecimal(reader.GetValue(8)), StockKnown = Convert.ToInt64(reader.GetValue(10)) != 0,
                WeeklyNeed = plans.Where(p => p.ProductId == product.Id && p.IsActiveOn(DateOnly.FromDateTime(DateTime.Today))).Sum(p => Number(p.Quantity)) * 7 });
        }
        return result;
    }
    public decimal TotalPurchases()
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT COALESCE(SUM(total_cost),0) FROM stock_movements WHERE kind='Nachkauf'";
        return Convert.ToDecimal(command.ExecuteScalar());
    }
    public void SaveProductDetails(MedicationProduct product)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "UPDATE medication_products SET manufacturer=$manufacturer,supplier=$supplier,pack_units=$units,pack_price=$price WHERE id=$id";
        command.Parameters.AddWithValue("$id", product.Id); command.Parameters.AddWithValue("$manufacturer", product.Manufacturer);
        command.Parameters.AddWithValue("$supplier", product.Supplier); command.Parameters.AddWithValue("$units", (double)product.PackUnits);
        command.Parameters.AddWithValue("$price", (double)product.PackPrice); command.ExecuteNonQuery();
    }
    private void AddStockMovement(long productId, string kind, decimal units, decimal packages, decimal cost)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO stock_movements(product_id,day,kind,units,packages,total_cost) VALUES($id,$day,$kind,$units,$packages,$cost)";
        command.Parameters.AddWithValue("$id", productId); command.Parameters.AddWithValue("$day", DateTime.Today.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$kind", kind); command.Parameters.AddWithValue("$units", (double)units);
        command.Parameters.AddWithValue("$packages", (double)packages); command.Parameters.AddWithValue("$cost", (double)cost);
        command.ExecuteNonQuery();
    }
    public void RecordPurchase(MedicationProduct product, decimal packages)
    {
        if (product.PackUnits <= 0 || packages <= 0) throw new ArgumentException("Bitte Packungsinhalt und Anzahl der gekauften Packungen angeben.");
        AddStockMovement(product.Id, "Nachkauf", product.PackUnits * packages, packages, product.PackPrice * packages);
    }
    public void SetStock(long productId, decimal target)
    {
        var current = ProductStocks().First(x => x.Product.Id == productId).Current;
        AddStockMovement(productId, "Korrektur", target - current, 0, 0);
    }
    public List<StockMovement> StockMovements(long productId)
    {
        var result = new List<StockMovement>();
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT id,day,kind,units,packages,total_cost FROM stock_movements WHERE product_id=$id ORDER BY day DESC,id DESC";
        command.Parameters.AddWithValue("$id", productId);
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(new StockMovement { Id = reader.GetInt64(0), Day = reader.GetString(1), Kind = reader.GetString(2),
            Units = Convert.ToDecimal(reader.GetValue(3)), Packages = Convert.ToDecimal(reader.GetValue(4)), Cost = Convert.ToDecimal(reader.GetValue(5)) });
        return result;
    }
    public void DeleteStockMovement(long id)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "DELETE FROM stock_movements WHERE id=$id AND kind IN ('Nachkauf','Korrektur')";
        command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery();
    }
    public void SaveIntakes(DateTime day, IReadOnlyList<Entry> previous, IReadOnlyList<Entry> replacements)
    {
        using var db = Open(); using var transaction = db.BeginTransaction();
        foreach (var old in previous)
        {
            using var delete = db.CreateCommand(); delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM entries WHERE id=$id AND kind='Einnahme'";
            delete.Parameters.AddWithValue("$id", old.Id); delete.ExecuteNonQuery();
        }
        foreach (var entry in replacements)
        {
            using var insert = db.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO entries(kind,start,end,data,note) VALUES('Einnahme',$start,NULL,$data,'')";
            insert.Parameters.AddWithValue("$start", entry.Start.ToString("O"));
            insert.Parameters.AddWithValue("$data", entry.Data); insert.ExecuteNonQuery();
        }
        RebuildStockForDay(db, transaction, day);
        transaction.Commit();
    }
    private static void RebuildStockForDay(SqliteConnection db, SqliteTransaction transaction, DateTime day)
    {
        var key = day.ToString("yyyy-MM-dd");
        using (var clear = db.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM stock_movements WHERE kind='Einnahme' AND day=$day";
            clear.Parameters.AddWithValue("$day", key); clear.ExecuteNonQuery();
        }
        var intakes = new List<IntakeData>();
        using (var read = db.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT data FROM entries WHERE kind='Einnahme' AND substr(start,1,10)=$day";
            read.Parameters.AddWithValue("$day", key);
            using var reader = read.ExecuteReader();
            while (reader.Read()) intakes.Add(JsonSerializer.Deserialize<IntakeData>(reader.GetString(0)) ?? new());
        }
        foreach (var intake in intakes)
        {
            if (intake.ProductId <= 0 || intake.Status is not ("taken" or "Taken" or "Genommen")) continue;
            var units = Number(intake.ActualQuantity);
            if (units <= 0) continue;
            using var movement = db.CreateCommand(); movement.Transaction = transaction;
            movement.CommandText = "INSERT INTO stock_movements(product_id,day,kind,units) VALUES($product,$day,'Einnahme',$units)";
            movement.Parameters.AddWithValue("$product", intake.ProductId);
            movement.Parameters.AddWithValue("$day", key);
            movement.Parameters.AddWithValue("$units", -(double)units); movement.ExecuteNonQuery();
        }
    }
    public List<Entry> All()
    {
        var entries = new List<Entry>();
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT id,kind,start,end,data,note FROM entries ORDER BY start DESC,id DESC";
        using var reader = command.ExecuteReader();
        while (reader.Read()) entries.Add(new Entry { Id = reader.GetInt64(0), Kind = reader.GetString(1), Start = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind), End = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind), Data = reader.GetString(4), Note = reader.GetString(5) });
        return entries;
    }
    public void Save(Entry entry)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = entry.Id == 0 ? "INSERT INTO entries(kind,start,end,data,note) VALUES($kind,$start,$end,$data,$note)" : "UPDATE entries SET kind=$kind,start=$start,end=$end,data=$data,note=$note WHERE id=$id";
        command.Parameters.AddWithValue("$id", entry.Id);
        command.Parameters.AddWithValue("$kind", entry.Kind);
        command.Parameters.AddWithValue("$start", entry.Start.ToString("O"));
        command.Parameters.AddWithValue("$end", entry.End is null ? DBNull.Value : entry.End.Value.ToString("O"));
        command.Parameters.AddWithValue("$data", entry.Data);
        command.Parameters.AddWithValue("$note", entry.Note);
        command.ExecuteNonQuery();
    }
    public void Delete(long id)
    {
        using var db = Open(); using var transaction = db.BeginTransaction();
        DateTime? intakeDay = null;
        using (var find = db.CreateCommand())
        {
            find.Transaction = transaction; find.CommandText = "SELECT kind,start FROM entries WHERE id=$id";
            find.Parameters.AddWithValue("$id", id);
            using var reader = find.ExecuteReader();
            if (reader.Read() && reader.GetString(0) == "Einnahme") intakeDay = DateTime.Parse(reader.GetString(1)).Date;
        }
        using var command = db.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "DELETE FROM entries WHERE id=$id"; command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery();
        if (intakeDay is not null) RebuildStockForDay(db, transaction, intakeDay.Value);
        transaction.Commit();
    }
    public void Backup(string path)
    {
        using var source = Open();
        using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        target.Open(); source.BackupDatabase(target);
    }
    public void Restore(string path)
    {
        using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
        source.Open();
        using (var check = source.CreateCommand()) { check.CommandText = "SELECT count(*) FROM entries"; check.ExecuteScalar(); }
        using var target = Open(); source.BackupDatabase(target); target.Close(); Initialize();
    }
    public void ExportCsv(string path)
    {
        static string Q(object? s) => "\"" + Convert.ToString(s)?.Replace("\"", "\"\"") + "\"";
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("Id;Typ;Beginn;Ende;Daten_JSON;Notiz");
        foreach (var e in All()) writer.WriteLine(string.Join(";", new[] { Q(e.Id),Q(e.Kind),Q(e.Start.ToString("O")),Q(e.End?.ToString("O")),Q(e.Data),Q(e.Note) }));
        foreach (var p in MedicationPlans()) writer.WriteLine(string.Join(";", new[] { Q(p.Id), Q("Einnahmeplan"), Q(p.StartDate + " " + p.Time), Q(p.EndDate), Q(System.Text.Json.JsonSerializer.Serialize(p)), Q("") }));
        foreach (var p in ProductStocks()) writer.WriteLine(string.Join(";", new[] { Q(p.Product.Id), Q("Packung/Vorrat"), Q(""), Q(""), Q(JsonSerializer.Serialize(p)), Q("") }));
        foreach (var goal in GoalOptions()) writer.WriteLine(string.Join(";", new[] { Q(""), Q("Behandlungsziel"), Q(""), Q(""), Q(goal), Q("") }));
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT id,product_id,day,kind,units,packages,total_cost FROM stock_movements ORDER BY id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var movement = new { ProductId = reader.GetInt64(1), Day = reader.GetString(2), Kind = reader.GetString(3), Units = reader.GetDouble(4), Packages = reader.GetDouble(5), TotalCost = reader.GetDouble(6) };
            writer.WriteLine(string.Join(";", new[] { Q(reader.GetInt64(0)), Q("Bestandsbewegung"), Q(movement.Day), Q(""), Q(JsonSerializer.Serialize(movement)), Q("") }));
        }
    }
}
