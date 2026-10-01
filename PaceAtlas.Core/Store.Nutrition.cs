using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace PaceAtlas;

public sealed class FoodItem
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public double? CarbsPer100G { get; set; }
    public double? GlycemicIndex { get; set; }
    public double? GlycemicLoadPer100G { get; set; }
    public string Source { get; set; } = "";
    public string Note { get; set; } = "";
    public string BlsCode { get; set; } = "";
    public bool IsBlsBase { get; set; }
}

public sealed class FoodRule
{
    public long Id { get; set; }
    public string Pattern { get; set; } = "";
    public string Decision { get; set; } = "exclude";
    public string Note { get; set; } = "";
    public int Priority { get; set; }
    public string Source { get; set; } = "";
}

public sealed class MealIngredient
{
    public long FoodId { get; set; }
    public string Name { get; set; } = "";
    public double Grams { get; set; }
    public double? CarbsPer100G { get; set; }
    public double? GlycemicLoadPer100G { get; set; }
    public string BlsCode { get; set; } = "";
    public string NutrientSource { get; set; } = "";
}

public sealed class MealRecord
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime At { get; set; }
    public string Status { get; set; } = "planned";
    public List<MealIngredient> Ingredients { get; set; } = new();
    public string Note { get; set; } = "";
}

public sealed partial class Store
{
    private const string BlsVersion = "4.0-2025";
    private static void InitializeBls(SqliteConnection db)
    {
        using (var schema = db.CreateCommand())
        {
            schema.CommandText = """
                CREATE TABLE IF NOT EXISTS bls_foods (
                  id INTEGER PRIMARY KEY, code TEXT NOT NULL UNIQUE, name TEXT NOT NULL,
                  name_en TEXT NOT NULL, carbs REAL, nutrients TEXT NOT NULL,
                  version TEXT NOT NULL);
                CREATE INDEX IF NOT EXISTS ix_bls_foods_name ON bls_foods(name COLLATE NOCASE);
                """;
            schema.ExecuteNonQuery();
        }
        using (var columns = db.CreateCommand())
        {
            columns.CommandText = "PRAGMA table_info(foods)";
            using var reader = columns.ExecuteReader();
            var hasCode = false;
            while (reader.Read()) if (reader.GetString(1) == "bls_code") hasCode = true;
            reader.Close();
            if (!hasCode)
            {
                using var alter = db.CreateCommand();
                alter.CommandText = "ALTER TABLE foods ADD COLUMN bls_code TEXT";
                alter.ExecuteNonQuery();
            }
        }
        using (var index = db.CreateCommand())
        {
            index.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS ix_foods_bls_code ON foods(bls_code)";
            index.ExecuteNonQuery();
        }
        using var version = db.CreateCommand();
        version.CommandText = "SELECT value FROM app_settings WHERE key='bls_catalog_version'";
        if (version.ExecuteScalar() is string current && current == BlsVersion) return;

        // Smaller update packages may omit the seed. An existing per-user catalog remains usable.
        using var resource = typeof(Store).Assembly.GetManifestResourceStream("PaceAtlas.Bls4Catalog.tsv.gz");
        if (resource is null) return;
        using var gzip = new GZipStream(resource, CompressionMode.Decompress);
        using var text = new StreamReader(gzip);
        using var transaction = db.BeginTransaction();
        using var insert = db.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO bls_foods(code,name,name_en,carbs,nutrients,version)
            VALUES($code,$name,$en,$carbs,$nutrients,$version)
            ON CONFLICT(code) DO UPDATE SET name=excluded.name,name_en=excluded.name_en,
              carbs=excluded.carbs,nutrients=excluded.nutrients,version=excluded.version
            """;
        var code = insert.Parameters.Add("$code", SqliteType.Text);
        var name = insert.Parameters.Add("$name", SqliteType.Text);
        var english = insert.Parameters.Add("$en", SqliteType.Text);
        var carbs = insert.Parameters.Add("$carbs", SqliteType.Real);
        var nutrients = insert.Parameters.Add("$nutrients", SqliteType.Text);
        var seedVersion = insert.Parameters.Add("$version", SqliteType.Text);
        seedVersion.Value = BlsVersion;
        while (text.ReadLine() is { } line)
        {
            var parts = line.Split('\t', 5);
            if (parts.Length != 5) throw new InvalidDataException("BLS-Katalog ist unvollständig.");
            code.Value = parts[0]; name.Value = parts[1]; english.Value = parts[2];
            carbs.Value = double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value : DBNull.Value;
            nutrients.Value = parts[4];
            insert.ExecuteNonQuery();
        }
        using var mark = db.CreateCommand(); mark.Transaction = transaction;
        mark.CommandText = """
            INSERT INTO app_settings(key,value) VALUES('bls_catalog_version',$version)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value
            """;
        mark.Parameters.AddWithValue("$version", BlsVersion);
        mark.ExecuteNonQuery();
        transaction.Commit();
    }

    public bool HasBlsCatalog()
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM bls_foods LIMIT 1)";
        return Convert.ToInt32(command.ExecuteScalar()) != 0;
    }

    private void InitializeNutrition(SqliteConnection db)
    {
        using (var create = db.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE IF NOT EXISTS foods(id INTEGER PRIMARY KEY, name TEXT NOT NULL UNIQUE COLLATE NOCASE,
                  carbs REAL, gi REAL, gl REAL, source TEXT NOT NULL DEFAULT '', note TEXT NOT NULL DEFAULT '');
                CREATE TABLE IF NOT EXISTS food_rules(id INTEGER PRIMARY KEY, pattern TEXT NOT NULL,
                  decision TEXT NOT NULL, note TEXT NOT NULL DEFAULT '', priority INTEGER NOT NULL DEFAULT 0,
                  source TEXT NOT NULL DEFAULT '');
                CREATE TABLE IF NOT EXISTS meals(id INTEGER PRIMARY KEY, name TEXT NOT NULL,
                  at TEXT NOT NULL, status TEXT NOT NULL, ingredients TEXT NOT NULL,
                  note TEXT NOT NULL DEFAULT '');
                CREATE INDEX IF NOT EXISTS ix_meals_at ON meals(at);
                """;
            create.ExecuteNonQuery();
        }
        InitializeBls(db);
        using var check = db.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM app_settings WHERE key='nutrition_seeded'";
        if (Convert.ToInt32(check.ExecuteScalar()) != 0) return;
        using var transaction = db.BeginTransaction();
        using var stream = typeof(Store).Assembly.GetManifestResourceStream("PaceAtlas.NutritionCatalog.tsv")
            ?? throw new InvalidOperationException("Ernährungsdaten fehlen.");
        using var text = new StreamReader(stream);
        while (text.ReadLine() is { } line)
        {
            var parts = line.Split('\t');
            if (parts.Length != 4 || !double.TryParse(parts[1], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var gl) ||
                !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var gi) ||
                !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var carbs)) continue;
            using var insert = db.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = "INSERT OR IGNORE INTO foods(name,carbs,gi,gl,source) VALUES($name,$carbs,$gi,$gl,$source)";
            insert.Parameters.AddWithValue("$name", parts[0]);
            insert.Parameters.AddWithValue("$carbs", carbs);
            insert.Parameters.AddWithValue("$gi", gi);
            insert.Parameters.AddWithValue("$gl", gl);
            insert.Parameters.AddWithValue("$source", "Ärztlicher Plan 2024 · Scan, Eintrag prüfen");
            insert.ExecuteNonQuery();
        }
        void Rule(string pattern, string decision, string note, int priority = 10)
        {
            using var insert = db.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO food_rules(pattern,decision,note,priority,source) VALUES($p,$d,$n,$r,$s)";
            insert.Parameters.AddWithValue("$p", pattern); insert.Parameters.AddWithValue("$d", decision);
            insert.Parameters.AddWithValue("$n", note); insert.Parameters.AddWithValue("$r", priority);
            insert.Parameters.AddWithValue("$s", "Ärztlicher Ernährungsplan 2024, handschriftliche Korrekturen laut Nutzer");
            insert.ExecuteNonQuery();
        }
        foreach (var term in new[] { "Alkohol", "Wein", "Bier", "Gluten", "Weizen", "Schweinefleisch",
                     "Rohkost", "Sonnenblumenöl", "Erdnussöl", "Maisöl", "Palmöl", "Distelöl",
                     "Weizenkeimöl", "Kombucha", "Wasserkefir", "Sauerkraut", "Miso", "Sauerteig" })
            Rule(term, "exclude", "Laut persönlichem Plan vermeiden.");
        foreach (var term in new[] { "Fermentiert", "Fermentation", "Kristallzucker", "Rohrzucker", "Traubenzucker" })
            Rule(term, "exclude", "Laut persönlichem Plan ausgeschlossen.");
        foreach (var term in new[] { "Walnuss", "Erdnuss", "Haselnuss", "Pekannuss", "Cashew",
                     "Pinienkern", "Sonnenblumenkern" })
            Rule(term, "exclude", "Histaminliste: ungeeignet; Durchstreichung betont das Verbot.", 30);
        foreach (var term in new[] { "Milch", "Joghurt", "Quark", "Bohnen", "Linsen", "Erbsen" })
            Rule(term, "exclude", "Allgemeiner Ausschluss im persönlichen Plan; Ausnahmen gesondert prüfen.");
        foreach (var term in new[] { "Kichererbs", "Mandelmilch", "Hafermilch", "Reismilch",
                     "Ziegenmilch", "Schafmilch", "Schafskäse", "Feta", "Hartkäse" })
            Rule(term, "conditional", "Ausnahme im Plan; Menge und weitere Regeln prüfen.", 40);
        foreach (var term in new[] { "Tomate", "Gemüse", "Kartoffel" })
            Rule(term, "conditional", term == "Kartoffel" ? "Geschält kochen, abkühlen lassen und wieder erwärmen." :
                "Zubereitung prüfen: schälen beziehungsweise dünsten.", 15);
        foreach (var term in new[] { "Kaffee", "Schwarztee" })
            Rule(term, "conditional", "Zusammen höchstens zwei Tassen pro Tag laut Plan.", 20);
        foreach (var term in new[] { "Mandel", "Paranuss" })
            Rule(term, "conditional", term == "Paranuss" ? "Höchstens 2–3 Stück täglich." :
                "Kleine Menge vorsichtig testen.", 25);
        using var mark = db.CreateCommand(); mark.Transaction = transaction;
        mark.CommandText = "INSERT INTO app_settings(key,value) VALUES('nutrition_seeded','1')";
        mark.ExecuteNonQuery();
        transaction.Commit();
    }

    public List<FoodItem> Foods()
    {
        var result = new List<FoodItem>();
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = """
            SELECT f.id,f.name,COALESCE(f.carbs,b.carbs),f.gi,
                   COALESCE(f.gl,CASE WHEN f.gi IS NOT NULL AND COALESCE(f.carbs,b.carbs) IS NOT NULL
                     THEN ROUND(f.gi * COALESCE(f.carbs,b.carbs) / 100.0,1) END),
                   f.source,f.note,COALESCE(f.bls_code,'') AS code,0 AS base
            FROM foods f LEFT JOIN bls_foods b ON b.code=f.bls_code
            UNION ALL
            SELECT -b.id,b.name,b.carbs,NULL,NULL,
                   'BLS 4.0 · Max Rubner-Institut · CC BY 4.0','',b.code,1
            FROM bls_foods b WHERE NOT EXISTS
              (SELECT 1 FROM foods f WHERE f.bls_code=b.code OR f.name=b.name COLLATE NOCASE)
            ORDER BY name COLLATE NOCASE
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(new FoodItem { Id = reader.GetInt64(0), Name = reader.GetString(1),
            CarbsPer100G = reader.IsDBNull(2) ? null : reader.GetDouble(2),
            GlycemicIndex = reader.IsDBNull(3) ? null : reader.GetDouble(3),
            GlycemicLoadPer100G = reader.IsDBNull(4) ? null : reader.GetDouble(4),
            Source = reader.GetString(5), Note = reader.GetString(6),
            BlsCode = reader.GetString(7), IsBlsBase = reader.GetInt32(8) != 0 });
        return result;
    }

    public void SaveFood(FoodItem food)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = food.Id <= 0
            ? "INSERT INTO foods(name,carbs,gi,gl,source,note,bls_code) VALUES($n,$c,$i,$g,$s,$t,$b)"
            : "UPDATE foods SET name=$n,carbs=$c,gi=$i,gl=$g,source=$s,note=$t WHERE id=$id";
        command.Parameters.AddWithValue("$id", food.Id); command.Parameters.AddWithValue("$n", food.Name.Trim());
        command.Parameters.AddWithValue("$c", (object?)food.CarbsPer100G ?? DBNull.Value);
        command.Parameters.AddWithValue("$i", (object?)food.GlycemicIndex ?? DBNull.Value);
        command.Parameters.AddWithValue("$g", (object?)food.GlycemicLoadPer100G ?? DBNull.Value);
        command.Parameters.AddWithValue("$s", food.Source); command.Parameters.AddWithValue("$t", food.Note);
        command.Parameters.AddWithValue("$b", string.IsNullOrEmpty(food.BlsCode) ? DBNull.Value : food.BlsCode);
        command.ExecuteNonQuery();
    }

    public void DeleteFood(long id)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "DELETE FROM foods WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public List<FoodRule> FoodRules()
    {
        var result = new List<FoodRule>();
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT id,pattern,decision,note,priority,source FROM food_rules ORDER BY priority DESC,pattern";
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(new FoodRule { Id = reader.GetInt64(0), Pattern = reader.GetString(1),
            Decision = reader.GetString(2), Note = reader.GetString(3), Priority = reader.GetInt32(4), Source = reader.GetString(5) });
        return result;
    }

    public void SaveFoodRule(FoodRule rule)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = rule.Id == 0
            ? "INSERT INTO food_rules(pattern,decision,note,priority,source) VALUES($p,$d,$n,$r,$s)"
            : "UPDATE food_rules SET pattern=$p,decision=$d,note=$n,priority=$r,source=$s WHERE id=$id";
        command.Parameters.AddWithValue("$id", rule.Id); command.Parameters.AddWithValue("$p", rule.Pattern.Trim());
        command.Parameters.AddWithValue("$d", rule.Decision); command.Parameters.AddWithValue("$n", rule.Note);
        command.Parameters.AddWithValue("$r", rule.Priority); command.Parameters.AddWithValue("$s", rule.Source);
        command.ExecuteNonQuery();
    }

    public void DeleteFoodRule(long id)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "DELETE FROM food_rules WHERE id=$id";
        command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery();
    }

    public List<MealRecord> Meals()
    {
        var result = new List<MealRecord>();
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT id,name,at,status,ingredients,note FROM meals ORDER BY at DESC,id DESC";
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(new MealRecord { Id = reader.GetInt64(0), Name = reader.GetString(1),
            At = DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            Status = reader.GetString(3), Ingredients = JsonSerializer.Deserialize<List<MealIngredient>>(reader.GetString(4)) ?? [],
            Note = reader.GetString(5) });
        return result;
    }

    public void SaveMeal(MealRecord meal)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = meal.Id == 0
            ? "INSERT INTO meals(name,at,status,ingredients,note) VALUES($n,$a,$s,$i,$t)"
            : "UPDATE meals SET name=$n,at=$a,status=$s,ingredients=$i,note=$t WHERE id=$id";
        command.Parameters.AddWithValue("$id", meal.Id); command.Parameters.AddWithValue("$n", meal.Name.Trim());
        command.Parameters.AddWithValue("$a", meal.At.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$s", meal.Status);
        command.Parameters.AddWithValue("$i", JsonSerializer.Serialize(meal.Ingredients));
        command.Parameters.AddWithValue("$t", meal.Note); command.ExecuteNonQuery();
    }

    public void DeleteMeal(long id)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "DELETE FROM meals WHERE id=$id";
        command.Parameters.AddWithValue("$id", id); command.ExecuteNonQuery();
    }
}
