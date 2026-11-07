using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace Schach.Infrastructure.Identity;

/// <summary>
/// E-Mail-Adresse einer Anmeldung. Speicherform in <c>Spieler</c> und <c>Anmeldung</c>: getrimmt und
/// klein geschrieben (SCHACH-SPEC 8.12). Fuer die Mail-Limits zaehlt nur der Hash der Adresse ohne
/// <c>+tag</c> (SCHACH-SPEC 11.3), nie die Adresse selbst.
/// </summary>
public static class EmailAdresse
{
    public const int MaxLaenge = 254;

    /// <summary>Speicherform, oder <c>null</c> bei Formfehler (<c>MailAddress.TryCreate</c>, ≤ 254 Zeichen).</summary>
    public static string? Normalisiere(string? eingabe)
    {
        var adresse = eingabe?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(adresse) || adresse.Length > MaxLaenge)
        {
            return null;
        }

        // Nur die nackte Adresse: kein Anzeigename („Anna <anna@…>“).
        return MailAddress.TryCreate(adresse, out var parsed) && parsed.Address == adresse ? adresse : null;
    }

    /// <summary>SHA-256 der Speicherform ohne <c>+tag</c> im lokalen Teil.</summary>
    public static byte[] ZaehlHash(string normalisiert)
    {
        ArgumentNullException.ThrowIfNull(normalisiert);
        var at = normalisiert.LastIndexOf('@');
        var lokal = at < 0 ? normalisiert : normalisiert[..at];
        var plus = lokal.IndexOf('+', StringComparison.Ordinal);
        var ohneTag = plus < 0 ? normalisiert : string.Concat(lokal.AsSpan(0, plus), normalisiert.AsSpan(at));
        return SHA256.HashData(Encoding.UTF8.GetBytes(ohneTag));
    }
}
