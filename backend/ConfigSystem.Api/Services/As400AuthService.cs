using System.Data.Odbc;
using System.Text.RegularExpressions;

namespace ConfigSystem.Api.Services;

/// <summary>
/// Validates a sign-in against the AS/400 (IBM i) by attempting an ODBC
/// connection with the supplied user profile and password. The credentials are
/// never stored; a successful connection means the profile/password are valid.
/// </summary>
public class As400AuthService
{
    private readonly string _system;
    private readonly string _driver;

    public As400AuthService(IConfiguration config)
    {
        _system = config["Ibmi:System"] ?? "10.10.1.55";
        _driver = config["Ibmi:Driver"] ?? "IBM i Access ODBC Driver";
    }

    // Strip chars that can escape or inject new key-value pairs into an ODBC connection string.
    private static string SanitizeOdbcValue(string value) =>
        Regex.Replace(value.Trim(), @"[;{}=]", string.Empty);

    public (bool ok, string? error) Validate(string? userId, string? password)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            return (false, "User ID and password are required.");

        var safeUser = SanitizeOdbcValue(userId);
        var safePassword = SanitizeOdbcValue(password);
        var connectionString =
            $"Driver={{{_driver}}};System={_system};Uid={safeUser};Pwd={safePassword};Naming=sql;";

        try
        {
            using var conn = new OdbcConnection(connectionString);
            conn.Open();
            return (true, null);
        }
        catch (OdbcException ex)
        {
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
