using Microsoft.Data.Sqlite;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PaceAtlas;

public sealed record GiReference(string Name, double Gi, string Source);

public sealed partial class Store
{
    public List<GiReference> FindGiReferences(string search)
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT name,gi,source FROM gi_references
            WHERE name LIKE $term ESCAPE '\' COLLATE NOCASE
            ORDER BY name COLLATE NOCASE LIMIT 100
            """;
        var escaped = search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        command.Parameters.AddWithValue("$term", "%" + escaped + "%");
        var result = new List<GiReference>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(new GiReference(reader.GetString(0), reader.GetDouble(1), reader.GetString(2)));
        return result;
    }

    private static void InitializeGiReferences(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS gi_references (
              name TEXT NOT NULL PRIMARY KEY COLLATE NOCASE,
              gi REAL NOT NULL, source TEXT NOT NULL);
            """;
        command.ExecuteNonQuery();
    }

    // A user-supplied table is stored separately from both nutrition catalogs and personal overrides.
    // Only identical names (apart from casing/whitespace) are linked; a similar product is not a measured GI.
    public (int Imported, int Matched, int Ambiguous) ImportGiReferences(
        string path, CancellationToken cancellationToken = default)
    {
        using var probe = new StreamReader(path, Encoding.UTF8, true);
        var headerLine = probe.ReadLine() ?? throw new InvalidDataException("Die Datei ist leer.");
        var delimiter = headerLine.Count(c => c == '\t') > 0 ? "\t" :
            headerLine.Count(c => c == ';') >= headerLine.Count(c => c == ',') ? ";" : ",";
        using var parser = new TextFieldParser(path, Encoding.UTF8, true);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(delimiter);
        parser.HasFieldsEnclosedInQuotes = true;
        var headers = parser.ReadFields() ?? throw new InvalidDataException("Die Kopfzeile fehlt.");
        var nameIndex = Array.FindIndex(headers, h => h.Trim().TrimStart('\uFEFF').Equals("name", StringComparison.OrdinalIgnoreCase));
        var giIndex = Array.FindIndex(headers, h => h.Trim().Equals("gi", StringComparison.OrdinalIgnoreCase));
        var sourceIndex = Array.FindIndex(headers, h => h.Trim().Equals("source", StringComparison.OrdinalIgnoreCase));
        if (nameIndex < 0 || giIndex < 0)
            throw new InvalidDataException("Die Kopfzeile braucht die Spalten name und gi (optional: source).");

        var entries = new Dictionary<string, (double Gi, string Source)>(StringComparer.OrdinalIgnoreCase);
        var conflicting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cells = parser.ReadFields();
            if (cells is null || cells.Length <= Math.Max(nameIndex, giIndex)) continue;
            var name = Regex.Replace(cells[nameIndex].Trim(), @"\s+", " ");
            if (name.Length == 0 || name.Length > 300 || conflicting.Contains(name)) continue;
            var rawGi = cells[giIndex].Trim();
            if (!double.TryParse(rawGi, NumberStyles.Float, CultureInfo.InvariantCulture, out var gi) &&
                !double.TryParse(rawGi, NumberStyles.Float, CultureInfo.GetCultureInfo("de-DE"), out gi)) continue;
            if (!double.IsFinite(gi) || gi < 0 || gi > 150) continue;
            var source = sourceIndex >= 0 && cells.Length > sourceIndex && !string.IsNullOrWhiteSpace(cells[sourceIndex])
                ? cells[sourceIndex].Trim() : "Lokal importierte GI-Tabelle";
            if (entries.TryGetValue(name, out var previous) && Math.Abs(previous.Gi - gi) > 0.001)
            {
                entries.Remove(name);
                conflicting.Add(name);
            }
            else entries[name] = (gi, source);
        }
        if (entries.Count == 0)
            throw new InvalidDataException("Keine eindeutigen Namen mit gültigem GI gefunden. Bisherige Werte bleiben erhalten.");

        using var db = Open();
        using var transaction = db.BeginTransaction();
        using (var clear = db.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM gi_references";
            clear.ExecuteNonQuery();
        }
        using var insert = db.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO gi_references(name,gi,source) VALUES($name,$gi,$source)";
        var nameParam = insert.Parameters.Add("$name", SqliteType.Text);
        var giParam = insert.Parameters.Add("$gi", SqliteType.Real);
        var sourceParam = insert.Parameters.Add("$source", SqliteType.Text);
        foreach (var (name, entry) in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            nameParam.Value = name;
            giParam.Value = entry.Gi;
            sourceParam.Value = entry.Source;
            insert.ExecuteNonQuery();
        }
        using var count = db.CreateCommand();
        count.Transaction = transaction;
        count.CommandText = """
            SELECT COUNT(*) FROM gi_references r WHERE EXISTS
              (SELECT 1 FROM foods f WHERE f.name=r.name COLLATE NOCASE AND f.gi IS NULL)
              OR EXISTS (SELECT 1 FROM bls_foods b WHERE b.name=r.name COLLATE NOCASE)
              OR EXISTS (SELECT 1 FROM off_foods o WHERE o.name=r.name COLLATE NOCASE)
            """;
        var matched = Convert.ToInt32(count.ExecuteScalar());
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return (entries.Count, matched, conflicting.Count);
    }
}
