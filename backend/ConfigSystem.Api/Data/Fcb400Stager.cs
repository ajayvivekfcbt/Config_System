using System.Data.Odbc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ConfigSystem.Api.Data;

/// <summary>
/// Stages the Configuration System tables directly from the remote FCB AS/400
/// (IBM i, DB2 for i) into the local FCB SQLite database, mirroring the legacy
/// staging programs (UT2080-UT2087) that copied UTCFG* from the remote system.
///
/// Connection is via the IBM i Access ODBC Driver using a dedicated service
/// account. Configure (any config provider, e.g. environment variables or user
/// secrets):
///   Ibmi:System     - host/IP of the FCB system (falls back to Ibmi:System)
///   Ibmi:Driver     - ODBC driver name (default "IBM i Access ODBC Driver")
///   Fcb400:Library  - the library/schema holding the UTCFG* tables on FCB
///   Fcb400:Uid      - service-account user profile
///   Fcb400:Pwd      - service-account password (prefer env var Fcb400__Pwd)
///
/// When the connection is not configured or staging fails, returns false so the
/// caller can fall back to the local DATCOMN clone and keep the app usable.
/// </summary>
public static class Fcb400Stager
{
    private enum Kind { Int, Str, Flag }

    private sealed record ColMap(string Source, string Target, Kind Kind);

    private sealed record TableSpec(string Table, ColMap[] Columns);

    // Parent-to-child order so foreign keys are satisfied on insert.
    // Source = DB2 column on the FCB AS/400; Target = column in the local SQLite table.
    private static readonly TableSpec[] Specs =
    {
        new("UTCFGVTP", new[]
        {
            new ColMap("VTP_ID", "Id", Kind.Int),
            new ColMap("VTP_NAME", "Name", Kind.Str),
            new ColMap("VTP_DESC", "Description", Kind.Str),
            new ColMap("VTP_PROC", "ValidationProcedure", Kind.Str),
            new ColMap("VTP_INFO", "Information", Kind.Str),
        }),
        new("UTCFGSRM", new[]
        {
            new ColMap("SRM_ID", "Id", Kind.Int),
            new ColMap("SRM_NAME", "Name", Kind.Str),
            new ColMap("SRM_DESC", "Description", Kind.Str),
            new ColMap("SRM_INFO", "Information", Kind.Str),
        }),
        new("UTCFGSRV", new[]
        {
            new ColMap("SRV_ID", "Id", Kind.Int),
            new ColMap("SRV_NAME", "Name", Kind.Str),
            new ColMap("SRV_DESC", "Description", Kind.Str),
            new ColMap("SRV_MATCH", "MatchValue", Kind.Str),
            new ColMap("SRV_INFO", "Information", Kind.Str),
        }),
        new("UTCFGCTX", new[]
        {
            new ColMap("CTX_ID", "Id", Kind.Int),
            new ColMap("CTX_NAME", "Name", Kind.Str),
            new ColMap("CTX_DESC", "Description", Kind.Str),
            new ColMap("CTX_IS_SRV", "IsServer", Kind.Flag),
            new ColMap("CTX_INFO", "Information", Kind.Str),
        }),
        new("UTCFGXTN", new[]
        {
            new ColMap("XTN_ID", "Id", Kind.Int),
            new ColMap("CTX_ID", "ContextId", Kind.Int),
            new ColMap("XTN_NAME", "Name", Kind.Str),
            new ColMap("XTN_DESC", "Description", Kind.Str),
            new ColMap("XTN_INFO", "Information", Kind.Str),
        }),
        new("UTCFGSCP", new[]
        {
            new ColMap("SCP_ID", "Id", Kind.Int),
            new ColMap("SRV_ID", "ServerId", Kind.Int),
            new ColMap("SRM_ID", "ScopeResolutionMethodId", Kind.Int),
            new ColMap("SCP_NAME", "Name", Kind.Str),
            new ColMap("SCP_DESC", "Description", Kind.Str),
            new ColMap("SCP_IS_SRV", "IsServer", Kind.Flag),
            new ColMap("SCP_MATCH", "MatchValue", Kind.Str),
            new ColMap("SCP_INFO", "Information", Kind.Str),
        }),
        new("UTCFGVDF", new[]
        {
            new ColMap("VDF_ID", "Id", Kind.Int),
            new ColMap("XTN_ID", "ExtentId", Kind.Int),
            new ColMap("VTP_ID", "VariableTypeId", Kind.Int),
            new ColMap("VDF_NAME", "Name", Kind.Str),
            new ColMap("VDF_DESC", "Description", Kind.Str),
            new ColMap("VDF_RSTRCT", "ValuesAreRestricted", Kind.Flag),
            new ColMap("VDF_INFO", "Information", Kind.Str),
        }),
        new("UTCFGVVL", new[]
        {
            new ColMap("VVL_ID", "Id", Kind.Int),
            new ColMap("VDF_ID", "VariableDefinitionId", Kind.Int),
            new ColMap("VVL_VALUE", "Value", Kind.Str),
            new ColMap("VVL_DESC", "Description", Kind.Str),
            new ColMap("VVL_INFO", "Information", Kind.Str),
        }),
        new("UTCFGVAL", new[]
        {
            new ColMap("VAL_ID", "Id", Kind.Int),
            new ColMap("VDF_ID", "VariableDefinitionId", Kind.Int),
            new ColMap("SCP_ID", "ScopeId", Kind.Int),
            new ColMap("VAL_VARVAL", "Value", Kind.Str),
        }),
    };

    /// <summary>
    /// Stages all tables from the FCB AS/400 into the given (FCB) database.
    /// Credentials come from the signed-in user (uid/pwd) when supplied, else
    /// from the Fcb400:Uid / Fcb400:Pwd configuration. Returns true on success;
    /// false when unconfigured or on any failure (the local database is left
    /// unchanged in that case).
    /// </summary>
    public static bool TryStage(ConfigDbContext db, IConfiguration config, ILogger? log = null,
        string? uid = null, string? pwd = null)
    {
        var system = config["Fcb400:System"] ?? config["Ibmi:System"];
        var driver = config["Ibmi:Driver"] ?? "IBM i Access ODBC Driver";
        var library = config["Fcb400:Library"];
        var user = !string.IsNullOrWhiteSpace(uid) ? uid : config["Fcb400:Uid"];
        var password = !string.IsNullOrWhiteSpace(pwd) ? pwd : config["Fcb400:Pwd"];

        if (string.IsNullOrWhiteSpace(system) || string.IsNullOrWhiteSpace(library) ||
            string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            log?.LogInformation("FCB AS/400 staging is not configured; using the local DATCOMN clone.");
            return false;
        }

        var odbcCs = $"Driver={{{driver}}};System={system};Uid={user.Trim()};Pwd={password};Naming=sql;";

        try
        {
            // 1) Read every table from the remote FCB system into memory.
            var staged = new Dictionary<string, List<object?[]>>();
            using (var odbc = new OdbcConnection(odbcCs))
            {
                odbc.Open();
                foreach (var spec in Specs)
                {
                    var cols = string.Join(", ", spec.Columns.Select(c => c.Source));
                    using var cmd = new OdbcCommand($"SELECT {cols} FROM {library}.{spec.Table}", odbc);
                    using var rd = cmd.ExecuteReader();
                    var rows = new List<object?[]>();
                    while (rd.Read())
                    {
                        var vals = new object?[spec.Columns.Length];
                        for (int i = 0; i < spec.Columns.Length; i++)
                            vals[i] = rd.IsDBNull(i) ? null : rd.GetValue(i);
                        rows.Add(vals);
                    }
                    staged[spec.Table] = rows;
                }
            }

            // 2) Replace the local FCB tables with the staged data, in one transaction.
            var conn = (SqliteConnection)db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) conn.Open();

            Exec(conn, null, "PRAGMA foreign_keys=OFF");
            try
            {
                using var tx = conn.BeginTransaction();
                foreach (var spec in Specs.Reverse())
                    Exec(conn, tx, $"DELETE FROM {spec.Table}");
                foreach (var spec in Specs)
                    InsertRows(conn, tx, spec, staged[spec.Table]);
                tx.Commit();
            }
            finally
            {
                Exec(conn, null, "PRAGMA foreign_keys=ON");
            }

            var total = staged.Values.Sum(v => v.Count);
            log?.LogInformation("Staged {Rows} rows from the FCB AS/400 ({Library}).", total, library);
            return true;
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "FCB AS/400 staging failed; falling back to the local DATCOMN clone.");
            return false;
        }
    }

    private static void InsertRows(SqliteConnection conn, SqliteTransaction tx, TableSpec spec, List<object?[]> rows)
    {
        var colList = string.Join(", ", spec.Columns.Select(c => c.Target));
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

        foreach (var row in rows)
        {
            for (int i = 0; i < spec.Columns.Length; i++)
                ps[i].Value = Convert(spec.Columns[i].Kind, row[i]);
            cmd.ExecuteNonQuery();
        }
    }

    private static object Convert(Kind kind, object? raw)
    {
        if (raw is null) return kind == Kind.Str ? string.Empty : 0;
        return kind switch
        {
            Kind.Int => (long)System.Convert.ToDecimal(raw),
            Kind.Flag => System.Convert.ToDecimal(raw) != 0 ? 1 : 0,
            _ => raw.ToString()?.TrimEnd() ?? string.Empty,
        };
    }

    private static void Exec(SqliteConnection conn, SqliteTransaction? tx, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    // ---- Staging marker: staging is on-demand, not run on every startup. ----
    // A marker file next to the FCB database records that it has been staged
    // from the AS/400, so restarts keep the staged data instead of re-cloning
    // or re-staging.

    private static string MarkerPath(IConfiguration config)
    {
        var db = new SqliteConnectionStringBuilder(ConfigSource.ConnectionString(config, "Fcb")).DataSource;
        return db + ".staged";
    }

    /// <summary>True once the FCB database has been staged from the AS/400.</summary>
    public static bool IsStaged(IConfiguration config) => File.Exists(MarkerPath(config));

    /// <summary>Records that the FCB database has been staged from the AS/400.</summary>
    public static void MarkStaged(IConfiguration config) =>
        File.WriteAllText(MarkerPath(config), DateTime.UtcNow.ToString("o"));
}
