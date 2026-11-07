using Schach.Domain.Stil;

namespace Schach.Domain.Entitaeten;

/// <summary>Wie ein angefragter Spielerzug zum Konflikttoken <c>halbzug</c> steht (SCHACH-SPEC 8.8).</summary>
public enum Zugeinordnung
{
    /// <summary>Der Spieler ist am Zug und das Token passt: spielen.</summary>
    Neu,

    /// <summary>Derselbe Zug an bereits gespielter Position: 200 mit aktuellem Zustand.</summary>
    Wiederholung,

    /// <summary>Anderer Zug an alter Position oder Token aus der Zukunft: 409.</summary>
    HalbzugVeraltet,

    /// <summary>Token passt, aber der Gegnerzug steht aus: 409.</summary>
    NichtAmZug,

    /// <summary>Die Partie ist beendet: 409.</summary>
    PartieBeendet,
}

/// <summary>Die Zugwahl fuer den Gegner, wie sie am Zug gespeichert wird (SCHACH-SPEC 5.3 Schritt 7, 8.10).</summary>
/// <param name="MaiaRang">Nur Maia-Pfad.</param>
/// <param name="PAnteil">Nur Maia-Pfad: <c>p / p_max</c>.</param>
/// <param name="W">Nur Maia-Pfad: Stilgewicht <c>w</c>.</param>
public sealed record Gegnerwahl(string Uci, ZugEngine Engine, int? MaiaRang, double? PAnteil, double? W, Wdl? Wdl);

/// <summary>Ergebnis eines Halbzugs. <see cref="Zug"/> ist <c>null</c>, wenn die Uhr des Gegners vor
/// seinem Zug ablief; dann traegt <see cref="Ende"/> das Zeitergebnis.</summary>
public sealed record Halbzugergebnis(Zug? Zug, Partieende? Ende);

/// <summary>Zuruecknehmen ist nicht erlaubt (API: 409 <c>ZuruecknehmenNichtErlaubt</c>, SCHACH-SPEC 3.3).</summary>
public sealed class ZuruecknehmenNichtErlaubtException : InvalidOperationException
{
    public ZuruecknehmenNichtErlaubtException()
        : base("Zuruecknehmen ist hier nicht erlaubt.")
    {
    }

    public ZuruecknehmenNichtErlaubtException(string message)
        : base(message)
    {
    }

    public ZuruecknehmenNichtErlaubtException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Partie-Kern ohne Datenbank (SCHACH-SPEC 3.1 bis 3.3, 8.6, 8.8): Starten, Spielerzug, Gegnerzug,
/// Einordnen des Konflikttokens, Zuruecknehmen. Aendert nur die uebergebenen Objekte; Laden,
/// Transaktionen und Engine liegen beim Aufrufer. Jede Aenderung zaehlt <see cref="Partie.Version"/>
/// hoch (EF-Concurrency-Token). Die <paramref name="zugfolge"/>-Parameter sind die gespeicherten
/// Zuege der Partie, aufsteigend nach Halbzug; der Aufrufer haengt neue Zuege selbst an.
/// </summary>
public static class Partiefuehrung
{
    /// <summary>Neue offene Partie in der Grundstellung. Die Uhr startet bei Erstellung (SCHACH-SPEC 3.2).</summary>
    /// <param name="id">128 Bit Zufall; fuer Gaeste zugleich der Zugriffsschluessel.</param>
    /// <param name="spielerId"><c>null</c> fuer Gaeste; nur Spieler-Partien zaehlen fuer die Elo.</param>
    public static Partie Starte(Guid id, Guid? spielerId, Gegner gegner, Farbe spielerFarbe, Bedenkzeit bedenkzeit, long seed, TimeProvider zeit)
    {
        ArgumentNullException.ThrowIfNull(gegner);
        ArgumentNullException.ThrowIfNull(zeit);
        var jetzt = zeit.GetUtcNow();
        var uhr = PartieUhr.Starte(bedenkzeit, spielerFarbe == Farbe.Weiss ? UhrSeite.Spieler : UhrSeite.Gegner, zeit);
        return new Partie
        {
            Id = id,
            SpielerId = spielerId,
            GegnerId = gegner.Key,
            SpielerFarbe = spielerFarbe,
            Bedenkzeit = bedenkzeit,
            Status = PartieStatus.Offen,
            ErstelltAm = jetzt,
            LetzterZugAm = jetzt,
            Halbzuege = 0,
            Fen = SchachDomain.GrundstellungFen,
            SpielerRestMs = uhr?.SpielerRestMs,
            GegnerRestMs = uhr?.GegnerRestMs,
            AmZugSeit = uhr?.AmZugSeit,
            ZaehltFuerElo = spielerId is not null,
            Seed = seed,
        };
    }

    /// <summary>Seite am Zug. Jede Partie beginnt in der Grundstellung: gerade Halbzuege heisst Weiss.</summary>
    public static Farbe AmZug(this Partie partie)
    {
        ArgumentNullException.ThrowIfNull(partie);
        return partie.Halbzuege % 2 == 0 ? Farbe.Weiss : Farbe.Schwarz;
    }

    /// <summary>Offen und der Spieler ist am Zug.</summary>
    public static bool SpielerAmZug(this Partie partie) =>
        partie.Status == PartieStatus.Offen && partie.AmZug() == partie.SpielerFarbe;

    /// <summary>Offen und der Gegner ist am Zug (SCHACH-SPEC 8.2, abgeleitet).</summary>
    public static bool GegnerZugAusstehend(this Partie partie) =>
        partie.Status == PartieStatus.Offen && partie.AmZug() != partie.SpielerFarbe;

    /// <summary>Uhr der Partie oder <c>null</c> ohne Bedenkzeit.</summary>
    public static PartieUhr? Uhr(this Partie partie)
    {
        ArgumentNullException.ThrowIfNull(partie);
        if (partie.Bedenkzeit == Bedenkzeit.OhneUhr || partie.SpielerRestMs is not { } spieler
            || partie.GegnerRestMs is not { } gegner || partie.AmZugSeit is not { } seit)
        {
            return null;
        }

        return new PartieUhr(partie.Bedenkzeit, spieler, gegner, partie.AmZug() == partie.SpielerFarbe ? UhrSeite.Spieler : UhrSeite.Gegner, seit);
    }

    /// <summary>Ordnet <c>{halbzug, zug}</c> ein (SCHACH-SPEC 8.8). Derselbe Spielerzug an bereits
    /// gespielter Position ist eine <see cref="Zugeinordnung.Wiederholung"/>, auch nach Partieende.</summary>
    public static Zugeinordnung OrdneEin(this Partie partie, IReadOnlyList<Zug> zugfolge, int halbzug, string uci)
    {
        ArgumentNullException.ThrowIfNull(partie);
        ArgumentNullException.ThrowIfNull(zugfolge);
        if (halbzug >= 0 && halbzug < partie.Halbzuege)
        {
            var gespielt = zugfolge.FirstOrDefault(z => z.Halbzug == halbzug + 1);
            return gespielt is not null && gespielt.Seite == partie.SpielerFarbe && string.Equals(gespielt.Uci, uci, StringComparison.Ordinal)
                ? Zugeinordnung.Wiederholung
                : partie.Status == PartieStatus.Beendet ? Zugeinordnung.PartieBeendet : Zugeinordnung.HalbzugVeraltet;
        }

        if (partie.Status == PartieStatus.Beendet)
        {
            return Zugeinordnung.PartieBeendet;
        }

        if (halbzug != partie.Halbzuege)
        {
            return Zugeinordnung.HalbzugVeraltet;
        }

        return partie.SpielerAmZug() ? Zugeinordnung.Neu : Zugeinordnung.NichtAmZug;
    }

    /// <summary>Spielt den Zug des Spielers (T1). Prueft danach das automatische Partieende.</summary>
    /// <exception cref="ZugUnzulaessigException">Der Zug ist nicht erlaubt.</exception>
    /// <exception cref="UhrAbgelaufenException">Die Uhr des Spielers ist abgelaufen.</exception>
    public static Halbzugergebnis SpieleSpielerzug(this Partie partie, IReadOnlyList<Zug> zugfolge, string uci, TimeProvider zeit)
    {
        ArgumentNullException.ThrowIfNull(zeit);
        if (!partie.SpielerAmZug())
        {
            throw new InvalidOperationException("Der Spieler ist nicht am Zug.");
        }

        var verlauf = Verlauf(zugfolge);
        var nach = verlauf[^1].Ziehe(uci);
        var san = verlauf[^1].San(uci);
        var jetzt = zeit.GetUtcNow();
        var uhr = partie.Uhr()?.ZugGespielt(zeit);

        var zug = Anhaengen(partie, uci, san, nach, jetzt, uhr);
        verlauf.Add(nach);
        return new Halbzugergebnis(zug, Remisregeln.Pruefe(verlauf));
    }

    /// <summary>
    /// Spielt den Gegnerzug (T2). <c>gespieltAm = max(LetzterZugAm + D, jetzt)</c> mit der Denkzeit D
    /// aus dem Partie-PRNG (SCHACH-SPEC 8.6); der Client zeigt ihn ab diesem Zeitpunkt. Laeuft die Uhr
    /// des Gegners vorher ab, kommt kein Zug, sondern das Zeitergebnis.
    /// </summary>
    /// <exception cref="ZugUnzulaessigException">Die Engine lieferte einen unerlaubten Zug.</exception>
    public static Halbzugergebnis SpieleGegnerzug(this Partie partie, IReadOnlyList<Zug> zugfolge, Gegnerwahl wahl, TimeProvider zeit)
    {
        ArgumentNullException.ThrowIfNull(wahl);
        ArgumentNullException.ThrowIfNull(zeit);
        if (!partie.GegnerZugAusstehend())
        {
            throw new InvalidOperationException("Der Gegner ist nicht am Zug.");
        }

        var verlauf = Verlauf(zugfolge);
        var nach = verlauf[^1].Ziehe(wahl.Uci);
        var san = verlauf[^1].San(wahl.Uci);
        var d = Gegnerdenkzeit.Ms(partie.Seed, partie.Halbzuege + 1, partie.GegnerRestMs);
        var gespieltAm = PartieUhr.GegnerzugGespieltAm(partie.LetzterZugAm, d, zeit.GetUtcNow());

        PartieUhr? uhr;
        try
        {
            uhr = partie.Uhr()?.GegnerzugGespielt(gespieltAm);
        }
        catch (UhrAbgelaufenException)
        {
            return new Halbzugergebnis(null, Remisregeln.Zeitueberschreitung(verlauf[^1], partie.AmZug()));
        }

        var zug = Anhaengen(partie, wahl.Uci, san, nach, gespieltAm, uhr);
        zug.Engine = wahl.Engine;
        zug.MaiaRang = wahl.MaiaRang;
        zug.PAnteil = wahl.PAnteil;
        zug.W = wahl.W;
        zug.Wdl = wahl.Wdl;
        verlauf.Add(nach);
        return new Halbzugergebnis(zug, Remisregeln.Pruefe(verlauf));
    }

    /// <summary>
    /// Zuruecknehmen (SCHACH-SPEC 3.3): entfernt den letzten Spielerzug und die Gegnerantwort, bei
    /// ausstehendem Gegnerzug nur den Spielerzug. Setzt <c>ZaehltFuerElo = false</c> und baut die
    /// Stellung aus der Zugfolge neu auf. Der Aufrufer loescht Zuege und Sprueche nach dem
    /// gelieferten Halbzug.
    /// </summary>
    /// <returns>Die Zahl der verbleibenden Halbzuege.</returns>
    /// <exception cref="ZuruecknehmenNichtErlaubtException">Mit Bedenkzeit oder ohne eigenen Zug.</exception>
    public static int NimmZurueck(this Partie partie, IReadOnlyList<Zug> zugfolge)
    {
        ArgumentNullException.ThrowIfNull(partie);
        ArgumentNullException.ThrowIfNull(zugfolge);
        if (partie.Status != PartieStatus.Offen)
        {
            throw new InvalidOperationException("Die Partie ist beendet.");
        }

        if (partie.Bedenkzeit != Bedenkzeit.OhneUhr)
        {
            throw new ZuruecknehmenNichtErlaubtException("Mit Bedenkzeit gibt es kein Zuruecknehmen.");
        }

        var letzterEigener = zugfolge.Where(z => z.Seite == partie.SpielerFarbe && z.Halbzug <= partie.Halbzuege)
            .Select(z => (int?)z.Halbzug).Max()
            ?? throw new ZuruecknehmenNichtErlaubtException("Noch kein eigener Zug.");

        var neu = letzterEigener - 1;
        partie.Halbzuege = neu;
        partie.Fen = neu == 0 ? SchachDomain.GrundstellungFen : zugfolge.First(z => z.Halbzug == neu).FenDanach;
        partie.ZaehltFuerElo = false;
        partie.Version++;
        return neu;
    }

    /// <summary>
    /// Waehlt den Gegnerzug aus der Maia-Policy (SCHACH-SPEC 5.3) mit Remis-Vermeidung nach Schritt 4a:
    /// ein Zug loest sofort Remis aus, wenn danach Patt, totes Material, Wiederholung oder 50 Zuege gelten.
    /// </summary>
    /// <param name="verlauf">Alle Stellungen ab Partiebeginn bis zur aktuellen (Gegner am Zug).</param>
    /// <param name="halbzug">Der Halbzug, fuer den gewaehlt wird.</param>
    public static Stilwahl WaehleGegnerzug(
        IReadOnlyList<Stellung> verlauf, IReadOnlyList<Policyzug> policy, Stilparameter parameter, long seed, int halbzug)
    {
        ArgumentNullException.ThrowIfNull(verlauf);
        var aktuell = verlauf[^1];
        var remis = new Remislage(
            Materialbilanz.FuerSeiteAmZug(aktuell.Fen),
            uci => LoestSofortRemisAus(verlauf, uci));
        return Stilgewichtung.Waehle(policy, parameter, uci => Zugmerkmale.Bestimme(aktuell.Fen, uci), remis, seed, halbzug);
    }

    /// <summary>Alle Stellungen ab der Grundstellung bis zur aktuellen, aus <c>Zug.FenDanach</c>.</summary>
    public static List<Stellung> Verlauf(IReadOnlyList<Zug> zugfolge)
    {
        ArgumentNullException.ThrowIfNull(zugfolge);
        var verlauf = new List<Stellung>(zugfolge.Count + 1) { Stellung.AusFen(SchachDomain.GrundstellungFen) };
        verlauf.AddRange(zugfolge.OrderBy(z => z.Halbzug).Select(z => Stellung.AusFen(z.FenDanach)));
        return verlauf;
    }

    private static bool LoestSofortRemisAus(IReadOnlyList<Stellung> verlauf, string uci)
    {
        var danach = new List<Stellung>(verlauf) { verlauf[^1].Ziehe(uci) };
        return Remisregeln.Pruefe(danach) is { Ergebnis: Ergebnis.Remis };
    }

    private static Zug Anhaengen(Partie partie, string uci, string san, Stellung nach, DateTimeOffset gespieltAm, PartieUhr? uhr)
    {
        var zug = new Zug
        {
            PartieId = partie.Id,
            Halbzug = partie.Halbzuege + 1,
            Seite = partie.AmZug(),
            Uci = uci,
            San = san,
            FenDanach = nach.Fen,
            GespieltAm = gespieltAm,
        };
        partie.Halbzuege = zug.Halbzug;
        partie.Fen = nach.Fen;
        partie.LetzterZugAm = gespieltAm;
        if (uhr is not null)
        {
            partie.SpielerRestMs = uhr.SpielerRestMs;
            partie.GegnerRestMs = uhr.GegnerRestMs;
            partie.AmZugSeit = uhr.AmZugSeit;
        }

        partie.Version++;
        return zug;
    }
}
