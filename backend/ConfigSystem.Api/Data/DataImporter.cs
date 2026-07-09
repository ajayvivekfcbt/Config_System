using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ConfigSystem.Api.Data;

/// <summary>
/// Loads the real Configuration System data exported directly from the IBM i
/// (DB2 for i) database into the local SQLite database. One CSV per table lives
/// in the "data" folder; columns are positional and match the entity order.
/// When the full real dataset is present it fully replaces the demo seed.
/// </summary>
public static class DataImporter
{
    private enum Kind { Int, Str, Flag }

    private sealed record TableSpec(string Table, string[] Columns, Kind[] Kinds, string Csv);

    // Parent-to-child order so foreign keys are satisfied on insert.
    private static readonly TableSpec[] Specs =
    {
        new("UTCFGVTP",
            new[] { "Id", "Name", "Description", "ValidationProcedure", "Information" },
            new[] { Kind.Int, Kind.Str, Kind.Str, Kind.Str, Kind.Str }, "UTCFGVTP.csv"),
        new("UTCFGSRM",
            new[] { "Id", "Name", "Description", "Information" },
            new[] { Kind.Int, Kind.Str, Kind.Str, Kind.Str }, "UTCFGSRM.csv"),
        new("UTCFGSRV",
            new[] { "Id", "Name", "Description", "MatchValue", "Information" },
            new[] { Kind.Int, Kind.Str, Kind.Str, Kind.Str, Kind.Str }, "UTCFGSRV.csv"),
        new("UTCFGCTX",
            new[] { "Id", "Name", "Description", "IsServer", "Information" },
            new[] { Kind.Int, Kind.Str, Kind.Str, Kind.Flag, Kind.Str }, "UTCFGCTX.csv"),
        new("UTCFGXTN",
            new[] { "Id", "ContextId", "Name", "Description", "Information" },
            new[] { Kind.Int, Kind.Int, Kind.Str, Kind.Str, Kind.Str }, "UTCFGXTN.csv"),
        new("UTCFGSCP",
            new[] { "Id", "ServerId", "ScopeResolutionMethodId", "Name", "Description", "IsServer", "MatchValue", "Information" },
            new[] { Kind.Int, Kind.Int, Kind.Int, Kind.Str, Kind.Str, Kind.Flag, Kind.Str, Kind.Str }, "UTCFGSCP.csv"),
        new("UTCFGVDF",
            new[] { "Id", "ExtentId", "VariableTypeId", "Name", "Description", "ValuesAreRestricted", "Information" },
            new[] { Kind.Int, Kind.Int, Kind.Int, Kind.Str, Kind.Str, Kind.Flag, Kind.Str }, "UTCFGVDF.csv"),
        new("UTCFGVVL",
            new[] { "Id", "VariableDefinitionId", "Value", "Description", "Information" },
            new[] { Kind.Int, Kind.Int, Kind.Str, Kind.Str, Kind.Str }, "UTCFGVVL.csv"),
        new("UTCFGVAL",
            new[] { "Id", "VariableDefinitionId", "ScopeId", "Value" },
            new[] { Kind.Int, Kind.Int, Kind.Int, Kind.Str }, "UTCFGVAL.csv"),
    };

    /// <summary>
    /// Loads the full real dataset if all CSV exports are present.
    /// Returns true when real data is in place (already-loaded or freshly imported).
    /// </summary>
    public static bool ImportAll(ConfigDbContext db)
    {
        var dir = ResolveDataDir();
        if (dir == null) return false;
        if (Specs.Any(s => !File.Exists(Path.Combine(dir, s.Csv)))) return false;

        // Already imported? (real export has thousands of value rows)
        if (db.VariableValues.Count() > 50) return true;

        var conn = (SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();

        // The source DB2 data contains some references that SQLite's FK
        // enforcement would reject, so load with FK checks off to mirror the
        // source faithfully, then re-enable. (Pragma must be set outside a tx.)
        Exec(conn, null, "PRAGMA foreign_keys=OFF");
        try
        {
            using var tx = conn.BeginTransaction();

            // Clear any demo rows (child-to-parent order).
            foreach (var spec in Specs.Reverse())
                Exec(conn, tx, $"DELETE FROM {spec.Table}");

            // Insert real data (parent-to-child order).
            foreach (var spec in Specs)
                InsertTable(conn, tx, spec, Path.Combine(dir, spec.Csv));

            tx.Commit();
        }
        finally
        {
            Exec(conn, null, "PRAGMA foreign_keys=ON");
        }
        return true;
    }

    private static void InsertTable(SqliteConnection conn, SqliteTransaction tx, TableSpec spec, string csvPath)
    {
        var colList = string.Join(", ", spec.Columns);
        var paramList = string.Join(", ", spec.Columns.Select((_, i) => "$p" + i));

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"INSERT INTO {spec.Table} ({colList}) VALUES ({paramList})";
        var ps = new SqliteParameter[spec.Columns.Length];
        for (int i = 0; i < ps.Length; i++)
        {
            ps[i] = cmd.CreateParameter();
            ps[i].ParameterName = "$p" + i;
            cmd.Parameters.Add(ps[i]);
        }

        bool header = true;
        foreach (var fields in ReadCsv(csvPath))
        {
            if (header) { header = false; continue; }
            if (fields.Length < spec.Columns.Length) continue;

            for (int i = 0; i < spec.Columns.Length; i++)
            {
                var raw = fields[i];
                ps[i].Value = spec.Kinds[i] switch
                {
                    Kind.Int => int.TryParse(raw.Trim(), out var n) ? n : 0,
                    Kind.Flag => (raw.Trim() is "1" or "Y" or "y" or "true" or "True") ? 1 : 0,
                    _ => raw.TrimEnd(),
                };
            }
            cmd.ExecuteNonQuery();
        }
    }

    private static void Exec(SqliteConnection conn, SqliteTransaction? tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string? ResolveDataDir()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data"),
            Path.Combine(Directory.GetCurrentDirectory(), "data"),
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    /// <summary>Parses a CSV file. Fields may be quoted with "" escaping; no embedded newlines.</summary>
    private static IEnumerable<string[]> ReadCsv(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0) continue;
            yield return SplitLine(line);
        }
    }

    private static string[] SplitLine(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }
}
