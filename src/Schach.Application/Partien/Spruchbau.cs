using Schach.Application.Sprueche;
using Schach.Domain;
using Schach.Domain.Entitaeten;
using Schach.Domain.Stil;

namespace Schach.Application.Partien;

/// <summary>Baut Lage und <see cref="SpruchAnfrage"/> aus der gespeicherten Zugfolge (SCHACH-SPEC 6.1, 6.3).</summary>
public static class Spruchbau
{
    /// <summary>Die Lage nach dem letzten Zug der Zugfolge, der ein Gegnerzug sein muss.</summary>
    public static Gegnerzuglage Lage(IReadOnlyList<Zug> zugfolge, Farbe spielerFarbe)
    {
        ArgumentNullException.ThrowIfNull(zugfolge);
        var folge = zugfolge.OrderBy(z => z.Halbzug).ToList();
        var g = folge[^1];
        if (g.Seite == spielerFarbe)
        {
            throw new ArgumentException("Der letzte Zug ist kein Gegnerzug.", nameof(zugfolge));
        }

        var gegner = g.Seite;
        var fenVorG = FenVor(folge, g.Halbzug);
        var s = folge.FirstOrDefault(z => z.Halbzug == g.Halbzug - 1 && z.Seite == spielerFarbe);
        var fenVorS = s is null ? fenVorG : FenVor(folge, s.Halbzug);
        return new Gegnerzuglage(
            Brettlage.Material(g.FenDanach, gegner) - Brettlage.Material(fenVorS, gegner),
            Stellung.AusFen(g.FenDanach).IstSchach,
            s is not null && Stellung.AusFen(s.FenDanach).IstSchach,
            s is not null && Zugmerkmale.Schlagzug(fenVorS, s.Uci) && Zugmerkmale.Schlagzug(fenVorG, g.Uci),
            folge.FirstOrDefault(z => z.Halbzug == g.Halbzug - 2 && z.Seite == gegner)?.Wdl?.W,
            g.Wdl?.W);
    }

    /// <summary>Anfrage fuer den Spruch nach <paramref name="nachHalbzug"/>.</summary>
    /// <param name="grund">Grund in Worten (Partieende, Remis-Ablehnung), sonst <c>null</c>.</param>
    public static SpruchAnfrage Anfrage(
        Partie partie, IReadOnlyList<Zug> zugfolge, IReadOnlyList<Spruch> sprueche, SpruchEreignis ereignis, int nachHalbzug, string? grund, bool nurErsatz)
    {
        ArgumentNullException.ThrowIfNull(partie);
        ArgumentNullException.ThrowIfNull(zugfolge);
        ArgumentNullException.ThrowIfNull(sprueche);
        var folge = zugfolge.Where(z => z.Halbzug <= nachHalbzug).OrderBy(z => z.Halbzug).ToList();
        var fen = folge.Count == 0 ? SchachDomain.GrundstellungFen : folge[^1].FenDanach;
        var gegnerFarbe = Brettlage.Andere(partie.SpielerFarbe);
        var letzter = folge.Count == 0 ? null : folge[^1];
        var gegnerZug = letzter is not null && letzter.Seite == gegnerFarbe ? letzter : null;
        var spielerZug = folge.LastOrDefault(z => z.Seite == partie.SpielerFarbe);
        var lage = gegnerZug is null ? null : Lage(folge, partie.SpielerFarbe);
        var zugnummer = Brettlage.Zugnummer(fen);

        var daten = new SpruchDaten(
            ereignis.ToString(),
            zugnummer,
            Brettlage.Phase(fen),
            partie.SpielerFarbe.ToString(),
            partie.Bedenkzeit.ToString(),
            spielerZug?.San,
            gegnerZug?.San,
            Stellung.AusFen(fen).IstSchach,
            lage?.MaterialAenderung ?? 0,
            Brettlage.Material(fen, gegnerFarbe),
            Stellungswort(gegnerZug?.Wdl),
            gegnerZug is null ? [] : Merkmale(Zugmerkmale.Bestimme(FenVor(folge, gegnerZug.Halbzug), gegnerZug.Uci)),
            grund,
            Laune(partie, nachHalbzug),
            [.. sprueche.Select(s => s.Text)]);

        var name = GegnerKatalog.Finde(partie.GegnerId)?.Name ?? partie.GegnerId;
        return new SpruchAnfrage(
            partie,
            name,
            ereignis,
            nachHalbzug,
            daten,
            new ErsatzWerte(Figur(folge, ereignis, gegnerZug, spielerZug), zugnummer),
            [.. sprueche.Select(s => s.Text)],
            nurErsatz);
    }

    /// <summary>FEN vor dem Halbzug <paramref name="halbzug"/>.</summary>
    public static string FenVor(IReadOnlyList<Zug> folge, int halbzug)
    {
        ArgumentNullException.ThrowIfNull(folge);
        return halbzug <= 1 ? SchachDomain.GrundstellungFen : folge.First(z => z.Halbzug == halbzug - 1).FenDanach;
    }

    private static char? Figur(IReadOnlyList<Zug> folge, SpruchEreignis ereignis, Zug? gegnerZug, Zug? spielerZug)
    {
        var zug = ereignis switch
        {
            SpruchEreignis.FigurGewonnen or SpruchEreignis.Tausch => gegnerZug,
            SpruchEreignis.FigurVerloren => spielerZug,
            _ => null,
        };
        if (zug is null)
        {
            return null;
        }

        var fenVor = FenVor(folge, zug.Halbzug);
        // Kein Stein auf dem Zielfeld: en passant, geschlagen wurde ein Bauer.
        return Zugmerkmale.Schlagzug(fenVor, zug.Uci) ? Brettlage.FigurAuf(fenVor, zug.Uci[2..4]) ?? 'p' : null;
    }

    /// <summary>Laune aus dem Partie-PRNG (6.3); unbekannte Gegner bleiben trocken.</summary>
    private static string Laune(Partie partie, int nachHalbzug)
    {
        var laune = GegnerKatalog.Finde(partie.GegnerId) is { } gegner
            ? Launenwahl.Waehle(gegner, partie.Seed, nachHalbzug)
            : Domain.Laune.Trocken;
        return laune.ToString().ToLowerInvariant();
    }

    private static string? Stellungswort(Wdl? wdl) => wdl switch
    {
        null => null,
        { W: >= 0.6 } => "Gegner steht besser",
        { L: >= 0.6 } => "Spieler steht besser",
        _ => "ausgeglichen",
    };

    private static List<string> Merkmale(Zugmerkmal m)
    {
        var namen = new List<string>();
        if (m.HasFlag(Zugmerkmal.Schlagzug)) namen.Add("Schlagzug");
        if (m.HasFlag(Zugmerkmal.Schach)) namen.Add("Schach");
        if (m.HasFlag(Zugmerkmal.Koenigszone)) namen.Add("Königszone");
        if (m.HasFlag(Zugmerkmal.Riskant)) namen.Add("Riskant");
        if (m.HasFlag(Zugmerkmal.Tausch)) namen.Add("Tausch");
        if (m.HasFlag(Zugmerkmal.Rueckzug)) namen.Add("Rückzug");
        if (m.HasFlag(Zugmerkmal.Entwicklung)) namen.Add("Entwicklung");
        return namen;
    }
}
