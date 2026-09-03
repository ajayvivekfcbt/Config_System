using Microsoft.AspNetCore.DataProtection;

namespace ConfigSystem.Api.Services;

/// <summary>
/// Encrypts and decrypts sensitive configuration values (passwords/credentials)
/// so they are never persisted to the database in clear text. Encryption uses the
/// ASP.NET Core Data Protection stack, which manages and persists the key ring.
/// A marker prefix lets us detect already-encrypted values and stay backward
/// compatible with existing rows that still hold clear-text data.
/// </summary>
public class SensitiveValueProtector
{
    private const string Marker = "enc:v1:";
    private readonly IDataProtector _protector;

    public SensitiveValueProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("ConfigSystem.SensitiveConfigValues.v1");
    }

    /// <summary>True when the value is already encrypted by this protector.</summary>
    public static bool IsProtected(string? value) =>
        value != null && value.StartsWith(Marker, StringComparison.Ordinal);

    /// <summary>
    /// Returns an encrypted, marker-prefixed representation of <paramref name="value"/>.
    /// Null/empty input and already-encrypted input are returned unchanged.
    /// </summary>
    public string? Protect(string? value)
    {
        if (string.IsNullOrEmpty(value) || IsProtected(value))
            return value;

        return Marker + _protector.Protect(value);
    }

    /// <summary>
    /// Returns the clear-text value. Input that is not marker-prefixed (legacy
    /// clear-text rows) is returned unchanged so old data keeps working.
    /// </summary>
    public string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsProtected(value))
            return value;

        try
        {
            return _protector.Unprotect(value[Marker.Length..]);
        }
        catch
        {
            // Undecryptable (e.g. key rotated away) — return the stored value as-is
            // rather than throwing, so a single bad row can't break a whole request.
            return value;
        }
    }
}
