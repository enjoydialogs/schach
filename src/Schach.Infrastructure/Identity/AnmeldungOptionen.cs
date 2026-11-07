namespace Schach.Infrastructure.Identity;

/// <summary>
/// Schluessel der Anmeldung. Limits aus Abschnitt <c>Limits</c> (SCHACH-SPEC 11.3), Link-Basis aus
/// <c>Anmeldung:LinkBasis</c>.
/// </summary>
public sealed class AnmeldungOptionen
{
    /// <summary>Ursprung des Links <c>{LinkBasis}/anmelden#t=…</c> (SCHACH-SPEC 9.5).</summary>
    public string LinkBasis { get; set; } = "https://schach.enjoy-dialogs.de";

    /// <summary><c>Limits:AnmeldungenJeAdresseUnd15Minuten</c>.</summary>
    public int AnmeldungenJeAdresseUnd15Minuten { get; set; } = 3;

    /// <summary><c>Limits:AnmeldungenJeAdresseUnd24Stunden</c>.</summary>
    public int AnmeldungenJeAdresseUnd24Stunden { get; set; } = 6;

    /// <summary><c>Limits:AnmeldungenJeIpUnd10Minuten</c> (Limiter <c>magic-link-je-ip</c>).</summary>
    public int AnmeldungenJeIpUnd10Minuten { get; set; } = 5;

    public static readonly TimeSpan Gueltigkeit = TimeSpan.FromMinutes(15);

    public const int MaxFehlversuche = 5;

    public const int StartElo = 800;
}
