using Microsoft.Data.Sqlite;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PaceAtlas;

public sealed partial class Store
{
    private static string CleanOpenFoodFactsName(string raw)
    {
        var value = WebUtility.HtmlDecode(WebUtility.HtmlDecode(raw)).Trim();
        value = Regex.Replace(value, @"\s*\(OFF\s+\d+\)\s*$", "", RegexOptions.IgnoreCase);
        // Pack size and shop price are not part of the food's identity.
        value = Regex.Replace(value, @"(?<![\p{L}\p{N}])\(?\s*\d+(?:[.,]\d+)?\s*(?:kg|mg|ml|g|l)\b\s*\)?",
            " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"(?<![\p{L}\p{N}])\d+(?:[.,]\d{1,2})\s*(?:€|eur\b|euro\b)",
            " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = Regex.Replace(value, @"€\s*\d+(?:[.,]\d{1,2})", " ");
        while (true)
        {
            var start = 0;
            while (start < value.Length && (char.IsWhiteSpace(value[start]) ||
                (value[start] != '"' && value[start] != '\'' &&
                    (char.IsPunctuation(value[start]) || char.IsSymbol(value[start])))))
                start++;
            value = value[start..];
            var withoutNumber = Regex.Replace(value, @"^\s*\(\d+\)\s*", "");
            if (withoutNumber == value) break;
            value = withoutNumber;
        }
        value = Regex.Replace(value.Trim().TrimEnd(' ', ',', ';', '-', '_', '·'), @"\s+", " ");
        return value;
    }

    private static void InitializeOpenFoodFacts(SqliteConnection db)
    {
        using var schema = db.CreateCommand();
        schema.CommandText = """
            CREATE TABLE IF NOT EXISTS off_foods (
              id INTEGER PRIMARY KEY, code TEXT NOT NULL UNIQUE, name TEXT NOT NULL,
              brand TEXT NOT NULL, carbs REAL, fat REAL, protein REAL,
              nutrients TEXT NOT NULL, imported_at TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_off_foods_name ON off_foods(name COLLATE NOCASE);
            """;
        schema.ExecuteNonQuery();
        using var columns = db.CreateCommand();
        columns.CommandText = "PRAGMA table_info(foods)";
        using var reader = columns.ExecuteReader();
        var hasCode = false;
        while (reader.Read()) if (reader.GetString(1) == "off_code") hasCode = true;
        reader.Close();
        if (!hasCode)
        {
            using var alter = db.CreateCommand();
            alter.CommandText = "ALTER TABLE foods ADD COLUMN off_code TEXT";
            alter.ExecuteNonQuery();
        }
        using var index = db.CreateCommand();
        index.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS ix_foods_off_code ON foods(off_code)";
        index.ExecuteNonQuery();
    }

    public int OpenFoodFactsCount()
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM off_foods";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    // Stream the official tab-separated CSV export, optionally gzip-compressed. Product
    // data stays separate from BLS values and personal overrides; no images are imported.
    public (int Imported, int Rows, int Germany, int Named, int Nutrition) ImportOpenFoodFacts(
        string path, CancellationToken cancellationToken = default,
        Action<int>? reportRows = null)
    {
        using var file = File.OpenRead(path);
        using var gzip = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(file, CompressionMode.Decompress) : null;
        using var text = new StreamReader((Stream?)gzip ?? file);
        using var parser = new TextFieldParser(text) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters("\t");
        var headers = parser.ReadFields() ?? throw new InvalidDataException("Open-Food-Facts-Kopfzeile fehlt.");
        var columns = headers.Select((name, index) => (name, index))
            .GroupBy(item => item.name.TrimStart('\uFEFF'), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "code", "carbohydrates_100g" })
            if (!columns.ContainsKey(required))
                throw new InvalidDataException($"Open-Food-Facts-Spalte fehlt: {required}");
        if (!columns.ContainsKey("product_name") && !columns.ContainsKey("product_name_de"))
            throw new InvalidDataException("Open-Food-Facts-Produktname fehlt.");
        string Field(string[] row, string name) => columns.TryGetValue(name, out var index) && index < row.Length
            ? row[index].Trim() : "";
        using var db = Open(); using var transaction = db.BeginTransaction();
        // Preserve the last source carb value for personal overrides before replacing
        // the entire OFF catalog. All changes are rolled back if import fails/cancels.
        using (var clear = db.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = """
                UPDATE foods SET carbs=(SELECT carbs FROM off_foods WHERE code=foods.off_code)
                WHERE off_code IS NOT NULL AND carbs IS NULL
                  AND EXISTS(SELECT 1 FROM off_foods WHERE code=foods.off_code);
                DELETE FROM off_foods;
                """;
            clear.ExecuteNonQuery();
        }
        using var insert = db.CreateCommand(); insert.Transaction = transaction;
        insert.CommandText = """
            INSERT INTO off_foods(code,name,brand,carbs,fat,protein,nutrients,imported_at)
            VALUES($code,$name,$brand,$carbs,$fat,$protein,$nutrients,$at)
            ON CONFLICT(code) DO UPDATE SET name=excluded.name,brand=excluded.brand,
              carbs=excluded.carbs,fat=excluded.fat,protein=excluded.protein,
              nutrients=excluded.nutrients,imported_at=excluded.imported_at
            """;
        var code = insert.Parameters.Add("$code", SqliteType.Text);
        var name = insert.Parameters.Add("$name", SqliteType.Text);
        var brand = insert.Parameters.Add("$brand", SqliteType.Text);
        var carbs = insert.Parameters.Add("$carbs", SqliteType.Real);
        var fat = insert.Parameters.Add("$fat", SqliteType.Real);
        var protein = insert.Parameters.Add("$protein", SqliteType.Real);
        var nutrients = insert.Parameters.Add("$nutrients", SqliteType.Text);
        var imported = insert.Parameters.Add("$at", SqliteType.Text);
        imported.Value = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using var removePrevious = db.CreateCommand(); removePrevious.Transaction = transaction;
        removePrevious.CommandText = "DELETE FROM off_foods WHERE code=$code";
        var previousCode = removePrevious.Parameters.Add("$code", SqliteType.Text);
        object Number(string raw) => double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value) && value >= 0 ? value : DBNull.Value;
        string KeyPart(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var key = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark &&
                    char.IsLetterOrDigit(character)) key.Append(char.ToLowerInvariant(character));
            return key.ToString();
        }
        var selected = new Dictionary<string, (int Score, string Code)>(StringComparer.Ordinal);
        var count = 0;
        var rowsRead = 0;
        var germanCountryRows = 0;
        var namedRows = 0;
        var nutritionRows = 0;
        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[]? row;
            try { row = parser.ReadFields(); }
            catch (MalformedLineException) { continue; }
            if (row is null) continue;
            if (++rowsRead % 10000 == 0) reportRows?.Invoke(rowsRead);
            var barcode = Field(row, "code");
            var countries = Field(row, "countries_tags");
            if (countries.Length == 0) countries = Field(row, "countries");
            var germanCountry = countries.Contains("en:germany", StringComparison.OrdinalIgnoreCase) ||
                countries.Contains("de:deutschland", StringComparison.OrdinalIgnoreCase) ||
                countries.Contains("Deutschland", StringComparison.OrdinalIgnoreCase) ||
                countries.Contains("Germany", StringComparison.OrdinalIgnoreCase);
            if (!germanCountry) continue;
            germanCountryRows++;
            var label = Field(row, "product_name_de");
            // Some CSV exports omit translated names and the language column entirely.
            // In that case retain the original label of a product sold in Germany.
            if (label.Length == 0 && (Field(row, "lang").Length == 0 ||
                Field(row, "lang").Equals("de", StringComparison.OrdinalIgnoreCase)))
                label = Field(row, "product_name");
            label = CleanOpenFoodFactsName(label);
            if (barcode.Length == 0 || label.Length == 0) continue;
            namedRows++;
            if (Number(Field(row, "carbohydrates_100g")) is DBNull) continue;
            nutritionRows++;
            var maker = CleanOpenFoodFactsName(Field(row, "brands"));
            var carbohydrates = (double)Number(Field(row, "carbohydrates_100g"));
            var fatValue = Number(Field(row, "fat_100g"));
            var proteinValue = Number(Field(row, "proteins_100g"));
            var displayName = label + (maker.Length == 0 || label.Contains(maker, StringComparison.OrdinalIgnoreCase)
                ? "" : " · " + maker);
            var duplicateKey = KeyPart(displayName);
            if (duplicateKey.Length == 0) continue;
            var values = new Dictionary<string, double>();
            foreach (var field in new[] { "carbohydrates_100g", "fat_100g", "proteins_100g",
                "energy-kcal_100g", "energy-kj_100g", "sugars_100g", "fiber_100g", "salt_100g" })
                if (Number(Field(row, field)) is double amount) values[field] = amount;
            var score = values.Count;
            if (selected.TryGetValue(duplicateKey, out var existing))
            {
                if (score <= existing.Score) continue;
                previousCode.Value = existing.Code;
                removePrevious.ExecuteNonQuery();
            }
            else count++;
            selected[duplicateKey] = (score, barcode);
            code.Value = barcode;
            name.Value = displayName;
            brand.Value = maker;
            carbs.Value = carbohydrates;
            fat.Value = fatValue;
            protein.Value = proteinValue;
            nutrients.Value = JsonSerializer.Serialize(values);
            insert.ExecuteNonQuery();
        }
        if (count == 0)
        {
            transaction.Rollback();
            return (0, rowsRead, germanCountryRows, namedRows, nutritionRows);
        }
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return (count, rowsRead, germanCountryRows, namedRows, nutritionRows);
    }
}
