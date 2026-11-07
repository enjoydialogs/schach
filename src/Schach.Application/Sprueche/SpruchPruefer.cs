using System.Text.RegularExpressions;

namespace Schach.Application.Sprueche;

/// <summary>Warum eine Modellantwort kein Spruch wird (SCHACH-SPEC 6.4).</summary>
public enum SpruchAblehnung
{
    Leer,
    ZuLang,
    Markdown,
    Url,
    Platzhalter,
    Sperrliste,
}

/// <param name="Ok">Wahr, wenn <paramref name="Text"/> gezeigt werden darf.</param>
/// <param name="Text">Der bereinigte Spruch; leer bei Ablehnung.</param>
/// <param name="Grund">Gesetzt, wenn abgelehnt.</param>
/// <param name="RohLaengeUeber100">Die bereinigte erste Zeile war laenger als 100 Zeichen (Kennzahl der Spruch-Probe, 6.8).</param>
public sealed record SpruchPruefung(bool Ok, string Text, SpruchAblehnung? Grund, bool RohLaengeUeber100)
{
    internal static SpruchPruefung Abgelehnt(SpruchAblehnung grund, bool zuLang) => new(false, "", grund, zuLang);
}

/// <summary>
/// Prueft eine Modellantwort, bevor sie ein Spruch wird (SCHACH-SPEC 6.4). Rein und ohne Zustand.
/// Abgelehnte Antworten ersetzt der Aufrufer durch einen Ersatzspruch.
/// </summary>
public static partial class SpruchPruefer
{
    public const int MaxZeichen = 100;

    private static readonly char[] Anfuehrungszeichen = ['"', '\'', '„', '“', '”', '‚', '‘', '’', '»', '«', '›', '‹', '`'];

    public static SpruchPruefung Pruefe(string? roh, string gegnerName)
    {
        var text = Bereinige(roh ?? "", gegnerName);
        if (text.Length == 0)
        {
            return SpruchPruefung.Abgelehnt(SpruchAblehnung.Leer, false);
        }

        var zuLang = text.Length > MaxZeichen;
        if (zuLang)
        {
            var ende = text.AsSpan(0, MaxZeichen).LastIndexOfAny(".!?…");
            if (ende < 0)
            {
                return SpruchPruefung.Abgelehnt(SpruchAblehnung.ZuLang, true);
            }

            text = text[..(ende + 1)].TrimEnd();
        }

        var grund = Verstoss(text);
        return grund is null
            ? new SpruchPruefung(true, text, null, zuLang)
            : SpruchPruefung.Abgelehnt(grund.Value, zuLang);
    }

    /// <summary>Schritt 1: trimmen, erste Zeile, Anfuehrungszeichen und eigener Name am Anfang.</summary>
    private static string Bereinige(string roh, string gegnerName)
    {
        var zeile = roh.Trim();
        var umbruch = zeile.IndexOfAny(['\r', '\n']);
        if (umbruch >= 0)
        {
            zeile = zeile[..umbruch];
        }

        zeile = zeile.Trim().Trim(Anfuehrungszeichen).Trim();
        if (!string.IsNullOrWhiteSpace(gegnerName))
        {
            var name = Regex.Match(zeile, $@"^{Regex.Escape(gegnerName.Trim())}\s*:\s*", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            if (name.Success)
            {
                zeile = zeile[name.Length..];
            }
        }

        // Einfache Sternchen (*betont*) fallen weg; doppelte bleiben und lehnt die Markdown-Pruefung ab.
        zeile = EinfachesSternchen().Replace(zeile, "");
        return zeile.Trim().Trim(Anfuehrungszeichen).Trim();
    }

    /// <summary>Schritte 3 und 4.</summary>
    private static SpruchAblehnung? Verstoss(string text)
    {
        if (text.Contains("{{", StringComparison.Ordinal))
        {
            return SpruchAblehnung.Platzhalter;
        }

        if (Markdown().IsMatch(text))
        {
            return SpruchAblehnung.Markdown;
        }

        if (Url().IsMatch(text))
        {
            return SpruchAblehnung.Url;
        }

        if (Sperrliste().IsMatch(text) || Beleidigung().IsMatch(text) || Anrede().IsMatch(text))
        {
            return SpruchAblehnung.Sperrliste;
        }

        return null;
    }

    // Fett/kursiv mit ** oder __, Code-Backticks, Ueberschrift, Aufzaehlung, Link.
    [GeneratedRegex(@"\*\*|__|`|^\s*#|^\s*[-*+]\s|^\s*\d+[.)]\s|\[[^\]]*\]\([^)]*\)")]
    private static partial Regex Markdown();

    [GeneratedRegex(@"https?://|www\.|\b[\w-]+\.(de|com|org|net|io|ai)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Url();

    [GeneratedRegex(@"(?<!\*)\*(?!\*)")]
    private static partial Regex EinfachesSternchen();

    // „zieh ich“ / „ziehe ich“ spricht der Gegner ueber den eigenen Zug: kein Ratschlag.
    [GeneratedRegex(
        @"\b(solltest|könntest|musst|müsstest|zieh(?:e)?(?!\s+ich\b)|nimm|pass\s+auf|achtung|vorsicht|aufpassen|hängt|ungedeckt|droht|besser\s+wäre|hättest|empfehle|tipp|rate\s+dir)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Sperrliste();

    // Beleidigungen mit gebeugten Formen (dummer, blöden, Idioten). Gilt fuer jede Frechheit (Spec 4.2).
    [GeneratedRegex(
        @"\b((dumm|doof|blöd)(e|er|es|en|em)?|(idiot|trottel|versager|loser|depp)(e|en|in|innen|s)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Beleidigung();

    // „Sie“/„Ihnen“ als Anrede: gross geschrieben. Kleines „sie“ (die Dame, die Figuren) bleibt erlaubt.
    [GeneratedRegex(@"\b(Sie|Ihnen)\b")]
    private static partial Regex Anrede();
}
