using Chess;

namespace Schach.Domain.Stil;

/// <summary>
/// Zugmerkmale nach Spec 5.3 Schritt 4 als reine Funktionen: Eingabe ist die FEN vor dem Zug und
/// der Zug in UCI. Gera.Chess prüft nur, ob der Zug erlaubt ist; Angriffe und Deckungen rechnet
/// <see cref="Merkmalbrett"/> geometrisch (Gera.Chess erzeugt nur Züge der Seite am Zug und kann
/// darum keine Deckung eigener Figuren melden).
/// </summary>
public static class Zugmerkmale
{
    /// <summary>Alle Brettmerkmale des Zugs. <see cref="Zugmerkmal.Ungewoehnlich"/> hängt an der
    /// Policy und kommt aus <see cref="Stilgewichtung.Merkmale"/>.</summary>
    /// <exception cref="ArgumentException">Der Zug ist in der Stellung nicht erlaubt.</exception>
    public static Zugmerkmal Bestimme(string fen, string uci)
    {
        var a = Analyse.Von(fen, uci);
        var m = Zugmerkmal.Keine;
        if (a.IstSchlag) m |= Zugmerkmal.Schlagzug;
        if (a.GibtSchach) m |= Zugmerkmal.Schach;
        if (a.InKoenigszone) m |= Zugmerkmal.Koenigszone;
        if (a.IstRiskant) m |= Zugmerkmal.Riskant;
        if (a.IstTausch) m |= Zugmerkmal.Tausch;
        if (a.IstRueckzug) m |= Zugmerkmal.Rueckzug;
        if (a.IstEntwicklung) m |= Zugmerkmal.Entwicklung;
        return m;
    }

    /// <summary>Der Zug schlägt eine Figur (auch en passant).</summary>
    public static bool Schlagzug(string fen, string uci) => Analyse.Von(fen, uci).IstSchlag;

    /// <summary>Der Zug setzt den gegnerischen König ins Schach (auch Abzugsschach).</summary>
    public static bool Schach(string fen, string uci) => Analyse.Von(fen, uci).GibtSchach;

    /// <summary>Zielfeld höchstens 2 Felder (Königsabstand) vom gegnerischen König entfernt.</summary>
    public static bool Koenigszone(string fen, string uci) => Analyse.Von(fen, uci).InKoenigszone;

    /// <summary>Die gezogene Figur ist danach angegriffen und ungedeckt oder von einer niedrigeren
    /// Figur angegriffen. Königszüge sind nie riskant.</summary>
    public static bool Riskant(string fen, string uci) => Analyse.Von(fen, uci).IstRiskant;

    /// <summary>Gleichwertiger Schlag (S und L gelten gleich) mit möglichem Rückschlag auf dem Zielfeld.</summary>
    public static bool Tausch(string fen, string uci) => Analyse.Von(fen, uci).IstTausch;

    /// <summary>Eine Figur außer Bauer zieht ohne Schlag Richtung eigene Grundreihe.</summary>
    public static bool Rueckzug(string fen, string uci) => Analyse.Von(fen, uci).IstRueckzug;

    /// <summary>Rochade oder eine Leichtfigur verlässt ihr Startfeld (b1, c1, f1, g1 bzw. b8, c8, f8, g8).</summary>
    public static bool Entwicklung(string fen, string uci) => Analyse.Von(fen, uci).IstEntwicklung;

    /// <summary><c>p &lt; 0,5 · p_max</c>.</summary>
    public static bool Ungewoehnlich(double p, double pMax) => p < Stilgewichtung.UngewoehnlichAnteil * pMax;

    private sealed record Analyse(
        bool IstSchlag,
        bool GibtSchach,
        bool InKoenigszone,
        bool IstRiskant,
        bool IstTausch,
        bool IstRueckzug,
        bool IstEntwicklung)
    {
        public static Analyse Von(string fen, string uci)
        {
            PruefeErlaubt(fen, uci);
            var vorher = Merkmalbrett.AusFen(fen);
            var zug = Merkmalbrett.Zug(uci);
            var nachher = vorher.Nach(zug);

            var weiss = vorher.WeissAmZug;
            var figur = char.ToLowerInvariant(nachher[zug.Nach]);
            var gegnerKoenig = nachher.Koenig(!weiss);
            var schlag = vorher.IstSchlag(zug);

            var angreifer = nachher.Angreifer(zug.Nach, durchWeiss: !weiss);
            var verteidiger = nachher.Angreifer(zug.Nach, durchWeiss: weiss);
            var wertFigur = Merkmalbrett.Wert(figur);
            var riskant = figur != 'k' && angreifer.Count > 0
                && (verteidiger.Count == 0 || angreifer.Any(a => Merkmalbrett.Wert(a) < wertFigur));

            var geschlagen = schlag ? char.ToLowerInvariant(vorher.GeschlageneFigur(zug)) : ' ';
            var tausch = schlag && Merkmalbrett.Wert(geschlagen) == Merkmalbrett.Wert(char.ToLowerInvariant(vorher[zug.Von]))
                && angreifer.Count > 0;

            var vorwaerts = weiss ? 1 : -1;
            var rueckzug = !schlag && figur != 'p' && char.ToLowerInvariant(vorher[zug.Von]) != 'p'
                && (zug.Nach.Reihe - zug.Von.Reihe) * vorwaerts < 0;

            var startfelder = weiss ? new[] { "b1", "c1", "f1", "g1" } : ["b8", "c8", "f8", "g8"];
            var leichtfigur = char.ToLowerInvariant(vorher[zug.Von]) is 'n' or 'b';
            var entwicklung = vorher.IstRochade(zug) || (leichtfigur && startfelder.Contains(zug.Von.Name));

            return new Analyse(
                schlag,
                nachher.Angreifer(gegnerKoenig, durchWeiss: weiss).Count > 0,
                Feld.Abstand(zug.Nach, gegnerKoenig) <= 2,
                riskant,
                tausch,
                rueckzug,
                entwicklung);
        }

        private static void PruefeErlaubt(string fen, string uci)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fen);
            ArgumentException.ThrowIfNullOrWhiteSpace(uci);
            if (uci.Length is not (4 or 5))
            {
                throw new ArgumentException($"Kein UCI-Zug: {uci}", nameof(uci));
            }

            var brett = ChessBoard.LoadFromFen(fen);
            var von = uci[..2];
            var nach = uci[2..4];
            var erlaubt = brett.Moves(allowAmbiguousCastle: true, generateSan: false).Any(m =>
                string.Equals(m.OriginalPosition.ToString(), von, StringComparison.Ordinal)
                && string.Equals(m.NewPosition.ToString(), nach, StringComparison.Ordinal));
            if (!erlaubt)
            {
                throw new ArgumentException($"Zug {uci} ist in der Stellung nicht erlaubt.", nameof(uci));
            }
        }
    }
}
