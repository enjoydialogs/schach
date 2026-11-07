using Schach.Application.Sprueche;
using Schach.Domain;
using Schach.Domain.Entitaeten;

namespace Schach.Application.Partien;

/// <summary>
/// Was eine Gegnerantwort auf dem Brett bewirkt hat, aus Sicht des Gegners (SCHACH-SPEC 6.1).
/// </summary>
/// <param name="MaterialAenderung">Material nach der Gegnerantwort minus Material vor dem Spielerzug.</param>
/// <param name="GegnerGibtSchach">Die Gegnerantwort setzt den Spieler ins Schach.</param>
/// <param name="SpielerGabSchach">Der Spielerzug davor setzte den Gegner ins Schach.</param>
/// <param name="BeideSchlagen">Spielerzug und Gegnerantwort schlagen beide.</param>
/// <param name="WVorher">Maia-Gewinnwahrscheinlichkeit beim vorigen Gegnerzug, sonst <c>null</c>.</param>
/// <param name="WJetzt">Maia-Gewinnwahrscheinlichkeit bei diesem Gegnerzug, sonst <c>null</c>.</param>
public sealed record Gegnerzuglage(
    int MaterialAenderung,
    bool GegnerGibtSchach,
    bool SpielerGabSchach,
    bool BeideSchlagen,
    double? WVorher,
    double? WJetzt);

/// <summary>
/// Waehlt das Spruch-Ereignis nach den Stufen und Abstaenden aus SCHACH-SPEC 6.1 und 6.2. Rein; die
/// Zaehler kommen aus den gespeicherten Spruechen, darum korrigieren sie sich nach einer Zuruecknahme
/// selbst. <c>null</c> heisst: kein Spruch.
/// </summary>
public static class SpruchAuswahl
{
    /// <summary>Ab diesem |Δ| ist eine gewonnene oder verlorene Figur Pflicht (Stufe A).</summary>
    public const int FigurPflichtDelta = 5;

    /// <summary>Ab diesem |Δ| zaehlt eine gewonnene oder verlorene Figur in Stufe B.</summary>
    public const int FigurDelta = 2;

    /// <summary>Hoechstens dieses |Δ| bei einem Tausch.</summary>
    public const int TauschDelta = 1;

    /// <summary>Ereignisse, die nie gegen <see cref="SpruchOptions.MaxJePartie"/> zaehlen.</summary>
    private static readonly HashSet<SpruchEreignis> Pflicht =
    [
        SpruchEreignis.Partiebeginn, SpruchEreignis.Sieg, SpruchEreignis.Niederlage, SpruchEreignis.Remis,
        SpruchEreignis.RemisAbgelehnt, SpruchEreignis.Zuruecknahme,
    ];

    /// <summary>Partieende aus Sicht des Gegners: ein Sieg des Spielers ist seine <see cref="SpruchEreignis.Niederlage"/>.</summary>
    public static SpruchEreignis BeiEnde(Ergebnis ergebnis, Farbe spielerFarbe) =>
        PartieBeenden.AusgangFuerSpieler(ergebnis, spielerFarbe) switch
        {
            Partieausgang.Sieg => SpruchEreignis.Niederlage,
            Partieausgang.Niederlage => SpruchEreignis.Sieg,
            _ => SpruchEreignis.Remis,
        };

    /// <summary>Nach einer Gegnerantwort, die die Partie nicht beendet.</summary>
    /// <param name="gegnerzuegeSeitSpruch">Gegnerzuege nach dem letzten Spruch, diese Antwort eingeschlossen.</param>
    /// <param name="nichtPflichtBisher">Gespeicherte Sprueche ausserhalb der Pflicht-Ereignisse.</param>
    public static SpruchEreignis? NachGegnerzug(Gegnerzuglage lage, int gegnerzuegeSeitSpruch, int nichtPflichtBisher, SpruchOptions options)
    {
        ArgumentNullException.ThrowIfNull(lage);
        ArgumentNullException.ThrowIfNull(options);
        var delta = lage.MaterialAenderung;

        // Stufe A: ohne Abstand und ohne Deckel.
        if (delta >= FigurPflichtDelta)
        {
            return SpruchEreignis.FigurGewonnen;
        }

        if (delta <= -FigurPflichtDelta)
        {
            return SpruchEreignis.FigurVerloren;
        }

        if (nichtPflichtBisher >= options.MaxJePartie)
        {
            return null;
        }

        // Stufe B.
        if (gegnerzuegeSeitSpruch >= options.AbstandTierB)
        {
            if (lage.GegnerGibtSchach)
            {
                return SpruchEreignis.SchachGegeben;
            }

            if (delta >= FigurDelta)
            {
                return SpruchEreignis.FigurGewonnen;
            }

            if (delta <= -FigurDelta)
            {
                return SpruchEreignis.FigurVerloren;
            }

            if (lage.SpielerGabSchach)
            {
                return SpruchEreignis.SchachErhalten;
            }

            if (options.Bewertungssprung && lage is { WVorher: { } vorher, WJetzt: { } jetzt })
            {
                if (jetzt - vorher >= options.BewertungssprungSchwelle)
                {
                    return SpruchEreignis.StellungBesser;
                }

                if (vorher - jetzt >= options.BewertungssprungSchwelle)
                {
                    return SpruchEreignis.StellungSchlechter;
                }
            }
        }

        // Stufe C.
        if (gegnerzuegeSeitSpruch >= options.AbstandTierC)
        {
            return lage.BeideSchlagen && Math.Abs(delta) <= TauschDelta ? SpruchEreignis.Tausch : SpruchEreignis.Zug;
        }

        return null;
    }

    /// <summary>Nach einer Zuruecknahme: die erste je Partie ist Pflicht, jede weitere Stufe B (niedrigster Rang).</summary>
    /// <param name="sprueche">Die Sprueche, die nach der Zuruecknahme bleiben.</param>
    /// <param name="zugfolge">Die Zuege, die nach der Zuruecknahme bleiben.</param>
    /// <param name="schonZurueckgenommen">Vor der Zuruecknahme gab es schon einen <c>Zuruecknahme</c>-Spruch.</param>
    public static SpruchEreignis? NachZuruecknahme(
        IReadOnlyList<Spruch> sprueche, IReadOnlyList<Zug> zugfolge, Farbe spielerFarbe, bool schonZurueckgenommen, SpruchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!schonZurueckgenommen)
        {
            return SpruchEreignis.Zuruecknahme;
        }

        return NichtPflicht(sprueche) < options.MaxJePartie && GegnerzuegeSeitSpruch(zugfolge, sprueche, spielerFarbe) >= options.AbstandTierB
            ? SpruchEreignis.Zuruecknahme
            : null;
    }

    /// <summary>Gegnerzuege nach dem letzten gespeicherten Spruch.</summary>
    public static int GegnerzuegeSeitSpruch(IReadOnlyList<Zug> zugfolge, IReadOnlyList<Spruch> sprueche, Farbe spielerFarbe)
    {
        ArgumentNullException.ThrowIfNull(zugfolge);
        ArgumentNullException.ThrowIfNull(sprueche);
        var letzter = sprueche.Count == 0 ? -1 : sprueche.Max(s => s.NachHalbzug);
        return zugfolge.Count(z => z.Seite != spielerFarbe && z.Halbzug > letzter);
    }

    public static int NichtPflicht(IReadOnlyList<Spruch> sprueche)
    {
        ArgumentNullException.ThrowIfNull(sprueche);
        return sprueche.Count(s => !Pflicht.Contains(s.Ereignis));
    }
}
