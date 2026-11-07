using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Schach.Infrastructure.Identity;

/// <summary>
/// Token und Code einer Magic-Link-Anmeldung (SCHACH-SPEC 9.2). Gespeichert werden nur Hashes:
/// <c>TokenHash = SHA-256(Token-Bytes)</c>, <c>CodeHash = SHA-256(Id-Bytes || Code)</c>.
/// Vergleiche laufen zeitkonstant ueber <see cref="CryptographicOperations.FixedTimeEquals"/>.
/// </summary>
public static class MagicLinkKrypto
{
    private const int TokenBytes = 32;

    /// <summary>32 Byte aus <see cref="RandomNumberGenerator"/>, base64url ohne Auffuellung (43 Zeichen).</summary>
    public static string NeuesToken() => Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

    /// <summary>SHA-256 der Token-Bytes; <c>null</c>, wenn das Token kein gueltiges 32-Byte-base64url ist.</summary>
    public static byte[]? TokenHash(string? token)
    {
        var bytes = Base64UrlDecode(token);
        return bytes is { Length: TokenBytes } ? SHA256.HashData(bytes) : null;
    }

    /// <summary>6 Ziffern aus <c>GetInt32(0, 1_000_000)</c>, mit fuehrenden Nullen.</summary>
    public static string NeuerCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    /// <summary>Entfernt Leerzeichen, wie die Mail den Code zeigt („123 456“).</summary>
    public static string? NormalisiereCode(string? code) =>
        code?.Replace(" ", "", StringComparison.Ordinal).Trim();

    public static bool IstCodeFormat(string? code) =>
        code is { Length: 6 } && code.All(char.IsAsciiDigit);

    public static byte[] CodeHash(Guid anmeldungId, string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return SHA256.HashData([.. anmeldungId.ToByteArray(), .. Encoding.ASCII.GetBytes(code)]);
    }

    public static bool CodePasst(Guid anmeldungId, string code, byte[] gespeicherterHash) =>
        IstCodeFormat(code) && CryptographicOperations.FixedTimeEquals(CodeHash(anmeldungId, code), gespeicherterHash);

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? Base64UrlDecode(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
        {
            return null;
        }

        var b64 = text.Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + ((4 - (b64.Length % 4)) % 4), '=');
        try
        {
            return Convert.FromBase64String(b64);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
