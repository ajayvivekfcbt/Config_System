using Microsoft.AspNetCore.Http;

namespace ConfigSystem.Api.Data;

/// <summary>
/// Selects which configuration data source (environment) a request targets.
/// Mirrors the legacy Dev vs FCB staging model (see UT2080-UT2087) where
/// config could be read from the local Dev system or the remote FCB system.
/// Each source maps to its own SQLite database so switching changes which
/// store the API reads from and writes to.
/// </summary>
public static class ConfigSource
{
    public const string HeaderName = "X-Config-Source";
    public const string Default = "Dev";

    /// <summary>Known source names; also the connection-string keys in configuration.</summary>
    public static readonly string[] Known = { "Dev", "Fcb" };

    /// <summary>
    /// Resolves the requested source to a known value, defaulting to Dev.
    /// Only known values are accepted, so the result is safe to use as a
    /// configuration key (no untrusted input reaches a connection string).
    /// </summary>
    public static string Resolve(HttpContext? ctx)
    {
        var raw = ctx?.Request.Headers[HeaderName].ToString();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            var match = Array.Find(Known, k => string.Equals(k, raw.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return Default;
    }

    /// <summary>Resolves the SQLite connection string for a given source name.</summary>
    public static string ConnectionString(IConfiguration config, string source) =>
        config.GetConnectionString(source) ?? $"Data Source=configsystem-{source.ToLowerInvariant()}.db";

    /// <summary>
    /// The FCB source is the staged master copy and is read-only; only the Dev
    /// source can be modified. Returns true when the request targets FCB.
    /// </summary>
    public static bool IsReadOnly(HttpContext? ctx) =>
        string.Equals(Resolve(ctx), "Fcb", StringComparison.Ordinal);
}
