namespace Schach.Domain;

/// <summary>
/// Automatisches Partieende nach SCHACH-SPEC 3.4. Gera.Chess liefert nur Legalitaet, Matt und
/// Patt (<see cref="Stellung"/>); Wiederholung, 50 Zuege, totes Material und Mattmaterial
/// entscheidet diese Klasse selbst. Es gibt kein "Remis beanspruchen".
/// </summary>
public static class Remisregeln
{
    /// <summary>Halbzuege ohne Bauernzug und ohne Schlag fuer die 50-Zuege-Regel.</summary>
    public const int FuenfzigZuegeHalbzuege = 100;

    /// <summary>Wie oft eine Stellung fuer die Wiederholung auftreten muss.</summary>
    public const int Wiederholungen = 3;

    /// <summary>Spielt die Zugfolge ab <paramref name="startFen"/> und prueft nach dem letzten Halbzug.</summary>
    /// <exception cref="ZugUnzulaessigException">Ein Zug der Zugfolge ist nicht erlaubt.</exception>
    public static Partieende? Pruefe(string startFen, IEnumerable<string> zugfolge)
    {
        ArgumentNullException.ThrowIfNull(zugfolge);
        var verlauf = new List<Stellung> { Stellung.AusFen(startFen) };
        foreach (var uci in zugfolge)
        {
            verlauf.Add(verlauf[^1].Ziehe(uci));
        }

        return Pruefe(verlauf);
    }

    /// <summary>
    /// Prueft die letzte Stellung des Verlaufs in der Reihenfolge Matt, Patt, Material,
    /// Wiederholung, 50 Zuege (SCHACH-SPEC 3.4.8). <paramref name="verlauf"/> enthaelt alle
    /// Stellungen ab Partiebeginn einschliesslich der aktuellen. <c>null</c>: die Partie laeuft weiter.
    /// </summary>
    public static Partieende? Pruefe(IReadOnlyList<Stellung> verlauf)
    {
        ArgumentNullException.ThrowIfNull(verlauf);
        ArgumentOutOfRangeException.ThrowIfZero(verlauf.Count);
        var aktuell = verlauf[^1];

        if (aktuell.IstMatt)
        {
            return Partieende.Sieg(Gegenseite(aktuell.AmZug), Grund.Matt);
        }

        if (aktuell.IstPatt)
        {
            return Partieende.Remis(Grund.Patt);
        }

        if (IstTotesMaterial(aktuell))
        {
            return Partieende.Remis(Grund.Material);
        }

        var schluessel = Wiederholungsschluessel(aktuell);
        if (verlauf.Count(s => Wiederholungsschluessel(s) == schluessel) >= Wiederholungen)
        {
            return Partieende.Remis(Grund.Wiederholung);
        }

        if (Halbzugzaehler(aktuell) >= FuenfzigZuegeHalbzuege)
        {
            return Partieende.Remis(Grund.FuenfzigZuege);
        }

        return null;
    }

    /// <summary>
    /// Ende durch Zeitueberschreitung (SCHACH-SPEC 3.4.7): wer ueberzieht, verliert; hat die andere
    /// Seite kein Mattmaterial, ist es Remis.
    /// </summary>
    public static Partieende Zeitueberschreitung(Stellung stellung, Farbe ueberzogen)
    {
        ArgumentNullException.ThrowIfNull(stellung);
        var andere = Gegenseite(ueberzogen);
        return HatMattmaterial(stellung, andere)
            ? Partieende.Sieg(andere, Grund.Zeit)
            : Partieende.Remis(Grund.ZeitOhneMattmaterial);
    }

    /// <summary>
    /// Mattmaterial (SCHACH-SPEC 3.4.6): fehlt, wenn die Seite nur den Koenig oder Koenig und genau
    /// eine Leichtfigur hat.
    /// </summary>
    public static bool HatMattmaterial(Stellung stellung, Farbe seite)
    {
        ArgumentNullException.ThrowIfNull(stellung);
        var figuren = Figuren(stellung).Where(f => f.Farbe == seite && f.Art != 'k').ToList();
        return !(figuren.Count == 0 || (figuren.Count == 1 && figuren[0].Art is 'n' or 'b'));
    }

    /// <summary>
    /// Totes Material (SCHACH-SPEC 3.4.5): K gegen K, K+L gegen K, K+S gegen K, oder nur Koenige und
    /// Laeufer, alle Laeufer auf gleicher Feldfarbe (wie chess.js <c>isInsufficientMaterial()</c>).
    /// </summary>
    public static bool IstTotesMaterial(Stellung stellung)
    {
        ArgumentNullException.ThrowIfNull(stellung);
        var figuren = Figuren(stellung).Where(f => f.Art != 'k').ToList();
        if (figuren.Count == 0)
        {
            return true;
        }

        if (figuren.Count == 1 && figuren[0].Art is 'n' or 'b')
        {
            return true;
        }

        return figuren.All(f => f.Art == 'b')
            && figuren.Select(f => f.FeldIstHell).Distinct().Count() == 1;
    }

    /// <summary>
    /// Schluessel fuer die Wiederholung: die ersten vier FEN-Felder, das En-passant-Feld nur, wenn
    /// der Schlag erlaubt ist (SCHACH-SPEC 3.4.2).
    /// </summary>
    public static string Wiederholungsschluessel(Stellung stellung)
    {
        ArgumentNullException.ThrowIfNull(stellung);
        var felder = stellung.Fen.Split(' ');
        var ep = felder[3] != "-" && stellung.HatEnPassantSchlag ? felder[3] : "-";
        return string.Join(' ', felder[0], felder[1], felder[2], ep);
    }

    private static int Halbzugzaehler(Stellung stellung) =>
        int.Parse(stellung.Fen.Split(' ')[4], System.Globalization.CultureInfo.InvariantCulture);

    private static Farbe Gegenseite(Farbe farbe) => farbe == Farbe.Weiss ? Farbe.Schwarz : Farbe.Weiss;

    private static IEnumerable<(Farbe Farbe, char Art, bool FeldIstHell)> Figuren(Stellung stellung)
    {
        var reihen = stellung.Fen.Split(' ')[0].Split('/');
        for (var r = 0; r < reihen.Length; r++)
        {
            var linie = 0;
            foreach (var zeichen in reihen[r])
            {
                if (char.IsAsciiDigit(zeichen))
                {
                    linie += zeichen - '0';
                    continue;
                }

                // a1 ist dunkel, also ist ein Feld hell, wenn Linie (a = 0) plus Reihe (1..8) gerade ist.
                var hell = (linie + (8 - r)) % 2 == 0;
                yield return (char.IsUpper(zeichen) ? Farbe.Weiss : Farbe.Schwarz, char.ToLowerInvariant(zeichen), hell);
                linie++;
            }
        }
    }
}
