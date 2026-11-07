using System.Text.RegularExpressions;
using Schach.Domain.Entitaeten;

namespace Schach.Application.Sprueche;

/// <summary>
/// Werte fuer die Platzhalter eines Ersatzspruchs (SCHACH-SPEC 6.5).
/// </summary>
/// <param name="Figur">Figur des Ereignisses als FEN-Buchstabe (p, n, b, r, q; Gross/Klein egal), sonst <c>null</c>.</param>
/// <param name="Zugnummer">Zugnummer fuer <c>{{zugnr}}</c>.</param>
public sealed record ErsatzWerte(char? Figur, int Zugnummer)
{
    /// <summary>Nominativ mit Artikel, z. B. „der Turm“.</summary>
    public string FigurNominativ => Namen().Nominativ;

    /// <summary>Akkusativ mit Artikel, z. B. „den Turm“.</summary>
    public string FigurAkkusativ => Namen().Akkusativ;

    private (string Nominativ, string Akkusativ) Namen() => char.ToLowerInvariant(Figur ?? ' ') switch
    {
        'q' => ("die Dame", "die Dame"),
        'r' => ("der Turm", "den Turm"),
        'b' => ("der Läufer", "den Läufer"),
        'n' => ("der Springer", "den Springer"),
        'p' => ("der Bauer", "den Bauern"),
        _ => ("eine Figur", "eine Figur"),
    };

    public string Einsetzen(string vorlage) => vorlage
        .Replace("{{figur_akk}}", FigurAkkusativ, StringComparison.Ordinal)
        .Replace("{{figur}}", FigurNominativ, StringComparison.Ordinal)
        .Replace("{{zugnr}}", Zugnummer.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
}

/// <summary>
/// Format einer Ersatzspruch-Datei <c>prompts/ersatz/{key}.de.md</c> (SCHACH-SPEC 6.5):
/// <c>## &lt;Ereignis&gt;</c> mit <c>- </c>-Zeilen. Eine Zeile mit <c># </c> (Titel) und Leerzeilen
/// werden uebersprungen.
/// </summary>
public static partial class ErsatzDatei
{
    /// <summary>Stufe A: mindestens 3 Varianten.</summary>
    public static readonly IReadOnlyList<SpruchEreignis> StufeA =
    [
        SpruchEreignis.Partiebeginn, SpruchEreignis.Sieg, SpruchEreignis.Niederlage, SpruchEreignis.Remis,
        SpruchEreignis.RemisAbgelehnt, SpruchEreignis.Zuruecknahme, SpruchEreignis.FigurGewonnen,
        SpruchEreignis.FigurVerloren,
    ];

    /// <summary>Stufe B ohne die Figur-Ereignisse (die zaehlen schon als Stufe A): mindestens 2.</summary>
    public static readonly IReadOnlyList<SpruchEreignis> StufeB = [SpruchEreignis.SchachGegeben, SpruchEreignis.SchachErhalten];

    /// <summary>Optional, sonst Rueckfall auf <see cref="SpruchEreignis.Zug"/>.</summary>
    public static readonly IReadOnlyList<SpruchEreignis> Optional =
        [SpruchEreignis.Tausch, SpruchEreignis.StellungBesser, SpruchEreignis.StellungSchlechter];

    public const int MinStufeA = 3;
    public const int MinStufeB = 2;
    public const int MinZug = 4;

    private static readonly HashSet<string> ErlaubtePlatzhalter = ["{{figur}}", "{{figur_akk}}", "{{zugnr}}"];

    public static IReadOnlyDictionary<SpruchEreignis, IReadOnlyList<string>> Parse(string inhalt, string dateiName)
    {
        ArgumentNullException.ThrowIfNull(inhalt);
        var ergebnis = new Dictionary<SpruchEreignis, List<string>>();
        List<string>? aktuell = null;
        var nummer = 0;

        foreach (var rohZeile in inhalt.Split('\n'))
        {
            nummer++;
            var zeile = rohZeile.Trim();
            if (zeile.Length == 0 || (zeile.StartsWith('#') && !zeile.StartsWith("##", StringComparison.Ordinal)))
            {
                continue;
            }

            if (zeile.StartsWith("## ", StringComparison.Ordinal))
            {
                var name = zeile[3..].Trim();
                if (!Enum.TryParse<SpruchEreignis>(name, ignoreCase: false, out var ereignis) || !Enum.IsDefined(ereignis))
                {
                    throw new FormatException($"{dateiName}:{nummer}: unbekanntes Ereignis '{name}'.");
                }

                if (ergebnis.ContainsKey(ereignis))
                {
                    throw new FormatException($"{dateiName}:{nummer}: Ereignis '{name}' doppelt.");
                }

                aktuell = [];
                ergebnis[ereignis] = aktuell;
                continue;
            }

            if (zeile.StartsWith("- ", StringComparison.Ordinal) && aktuell is not null)
            {
                aktuell.Add(zeile[2..].Trim());
                continue;
            }

            throw new FormatException($"{dateiName}:{nummer}: erwartet '## <Ereignis>' oder '- <Spruch>'.");
        }

        return ergebnis.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);
    }

    /// <summary>
    /// Ladepruefung einer Datei: Mindestvarianten, nur erlaubte Platzhalter, und jede Zeile besteht
    /// mit eingesetzten Werten den <see cref="SpruchPruefer"/> ohne Kuerzung. Leer heisst gueltig.
    /// </summary>
    public static IReadOnlyList<string> Pruefe(IReadOnlyDictionary<SpruchEreignis, IReadOnlyList<string>> datei, string gegnerName)
    {
        ArgumentNullException.ThrowIfNull(datei);
        var fehler = new List<string>();

        void Mindestens(SpruchEreignis ereignis, int min)
        {
            var anzahl = datei.TryGetValue(ereignis, out var zeilen) ? zeilen.Count : 0;
            if (anzahl < min)
            {
                fehler.Add($"{ereignis}: {anzahl} Varianten, mindestens {min}.");
            }
        }

        foreach (var e in StufeA)
        {
            Mindestens(e, MinStufeA);
        }

        foreach (var e in StufeB)
        {
            Mindestens(e, MinStufeB);
        }

        Mindestens(SpruchEreignis.Zug, MinZug);

        // Laengster Figurname als Probewert: „der Springer“ / „den Springer“, Zug 100.
        var probe = new ErsatzWerte('n', 100);
        foreach (var (ereignis, zeilen) in datei)
        {
            foreach (var zeile in zeilen)
            {
                foreach (Match m in Platzhalter().Matches(zeile))
                {
                    if (!ErlaubtePlatzhalter.Contains(m.Value))
                    {
                        fehler.Add($"{ereignis}: unbekannter Platzhalter {m.Value} in '{zeile}'.");
                    }
                }

                // Die Figurnamen tragen einen kleinen Artikel („der Turm“): nie am Satzanfang.
                if (FigurAmSatzanfang().IsMatch(zeile))
                {
                    fehler.Add($"{ereignis}: '{zeile}' hat eine Figur am Satzanfang.");
                }

                var text = probe.Einsetzen(zeile);
                if (Platzhalter().IsMatch(text))
                {
                    continue;
                }

                var pruefung = SpruchPruefer.Pruefe(text, gegnerName);
                if (!pruefung.Ok || pruefung.RohLaengeUeber100 || pruefung.Text != text)
                {
                    fehler.Add($"{ereignis}: '{zeile}' besteht die Pruefung nicht ({pruefung.Grund?.ToString() ?? "veraendert"}).");
                }
            }
        }

        return fehler;
    }

    [GeneratedRegex(@"\{\{[^}]*\}\}")]
    private static partial Regex Platzhalter();

    [GeneratedRegex(@"(^|[.!?…]\s+)\{\{figur")]
    private static partial Regex FigurAmSatzanfang();
}

/// <summary>
/// Ersatzsprueche aller Gegner (SCHACH-SPEC 6.5). Waehlt ohne Wiederholung in der Partie und
/// deterministisch aus Seed und Halbzug. Fehlende optionale Ereignisse fallen auf
/// <see cref="SpruchEreignis.Zug"/> zurueck.
/// </summary>
public sealed class ErsatzKatalog(IReadOnlyDictionary<string, IReadOnlyDictionary<SpruchEreignis, IReadOnlyList<string>>> dateien)
{
    /// <summary>Die 8 Gegner-Keys aus SCHACH-SPEC 4.3. Der Gegner-Katalog (#26) liegt in Schach.Domain.</summary>
    public static readonly IReadOnlyList<string> ErwarteteGegner =
        ["ilse", "finn", "oscar", "nele", "theo", "sementa", "konrad", "maren"];

    public IReadOnlyCollection<string> Gegner => dateien.Keys.ToList();

    public string Waehle(
        string gegnerKey,
        SpruchEreignis ereignis,
        long seed,
        int halbzug,
        IReadOnlyCollection<string> bereitsGesagt,
        ErsatzWerte werte)
    {
        ArgumentNullException.ThrowIfNull(bereitsGesagt);
        ArgumentNullException.ThrowIfNull(werte);
        if (!dateien.TryGetValue(gegnerKey, out var datei))
        {
            throw new KeyNotFoundException($"Keine Ersatzsprueche fuer Gegner '{gegnerKey}'.");
        }

        if (!datei.TryGetValue(ereignis, out var vorlagen) || vorlagen.Count == 0)
        {
            vorlagen = datei[SpruchEreignis.Zug];
        }

        var texte = vorlagen.Select(werte.Einsetzen).ToList();
        var frei = texte.Where(t => !bereitsGesagt.Contains(t)).ToList();
        var auswahl = frei.Count > 0 ? frei : texte;

        var index = (int)(Mische(seed, halbzug, (int)ereignis) % (ulong)auswahl.Count);
        return auswahl[index];
    }

    /// <summary>SplitMix64: fest definiert, unabhaengig von <see cref="Random"/> und der .NET-Version.</summary>
    internal static ulong Mische(long seed, int halbzug, int salz)
    {
        var z = unchecked((ulong)seed + (0x9E3779B97F4A7C15UL * (ulong)(uint)halbzug) + ((ulong)(uint)salz << 32));
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        return z ^ (z >> 31);
    }
}
