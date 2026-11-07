namespace Schach.Domain.Entitaeten;

/// <summary>Was der Aufrufer nach <see cref="PartieBeenden.Beenden"/> noch tun muss.</summary>
/// <param name="Loeschen">Gast-Partie: die Zeilen (Partie, Züge, Sprüche) in derselben Transaktion löschen.</param>
public sealed record BeendenFolge(bool Loeschen);

/// <summary>
/// Domänenweg <c>Partie.Beenden(ergebnis, grund)</c> (Spec 8.11). Ändert nur die übergebenen
/// Objekte; Laden, Transaktion und Löschen der Gast-Zeilen liegen beim Aufrufer (S1c). Als
/// Erweiterung, damit die Entitäten-Hülle aus 0b unverändert bleibt.
/// </summary>
public static class PartieBeenden
{
    /// <summary>
    /// Beendet die Partie endgültig: <c>Status</c>, <c>Ergebnis</c>, <c>Grund</c>, <c>BeendetAm</c>;
    /// bei Spieler-Partien mit <c>ZaehltFuerElo</c> zusätzlich <c>EloVorher</c>, <c>EloNachher</c> und
    /// <c>Spieler.Elo</c> (K = 32, Untergrenze 100); bei jeder Spieler-Partie die <see cref="Bilanz"/>,
    /// <c>BesiegtAm</c> nur beim ersten Sieg, der für die Elo zählt.
    /// </summary>
    /// <param name="spieler">Besitzer der Partie; <c>null</c> bei Gast-Partien.</param>
    /// <param name="bilanz">Zeile <c>(SpielerId, GegnerId)</c>, ggf. neu angelegt; <c>null</c> bei Gast-Partien.</param>
    /// <exception cref="InvalidOperationException">Die Partie ist schon beendet.</exception>
    public static BeendenFolge Beenden(
        this Partie partie,
        Ergebnis ergebnis,
        Grund grund,
        TimeProvider zeit,
        Spieler? spieler,
        Bilanz? bilanz)
    {
        ArgumentNullException.ThrowIfNull(partie);
        ArgumentNullException.ThrowIfNull(zeit);
        if (partie.Status == PartieStatus.Beendet)
        {
            throw new InvalidOperationException("Die Partie ist schon beendet; das Ergebnis ist endgültig.");
        }

        var jetzt = zeit.GetUtcNow();
        partie.Status = PartieStatus.Beendet;
        partie.Ergebnis = ergebnis;
        partie.Grund = grund;
        partie.BeendetAm = jetzt;

        if (partie.SpielerId is not { } spielerId)
        {
            partie.EloVorher = null;
            partie.EloNachher = null;
            return new BeendenFolge(Loeschen: true);
        }

        if (spieler is null || spieler.Id != spielerId)
        {
            throw new ArgumentException("Spieler-Partie braucht ihren Spieler.", nameof(spieler));
        }

        if (bilanz is null || bilanz.SpielerId != spielerId || !string.Equals(bilanz.GegnerId, partie.GegnerId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Spieler-Partie braucht die Bilanz (SpielerId, GegnerId).", nameof(bilanz));
        }

        var ausgang = AusgangFuerSpieler(ergebnis, partie.SpielerFarbe);
        if (partie.ZaehltFuerElo)
        {
            var gegner = GegnerKatalog.Finde(partie.GegnerId)
                ?? throw new InvalidOperationException($"Unbekannter Gegner '{partie.GegnerId}'.");
            partie.EloVorher = spieler.Elo;
            partie.EloNachher = Elo.Neu(spieler.Elo, gegner.Elo, ausgang);
            spieler.Elo = partie.EloNachher.Value;
        }
        else
        {
            partie.EloVorher = null;
            partie.EloNachher = null;
        }

        switch (ausgang)
        {
            case Partieausgang.Sieg:
                bilanz.Siege++;
                if (partie.ZaehltFuerElo && bilanz.BesiegtAm is null)
                {
                    bilanz.BesiegtAm = jetzt;
                }

                break;
            case Partieausgang.Remis:
                bilanz.Remis++;
                break;
            default:
                bilanz.Niederlagen++;
                break;
        }

        return new BeendenFolge(Loeschen: false);
    }

    /// <summary>Ergebnis aus Sicht des Spielers mit der Farbe <paramref name="spielerFarbe"/>.</summary>
    public static Partieausgang AusgangFuerSpieler(Ergebnis ergebnis, Farbe spielerFarbe) => ergebnis switch
    {
        Ergebnis.Remis => Partieausgang.Remis,
        Ergebnis.WeissGewinnt => spielerFarbe == Farbe.Weiss ? Partieausgang.Sieg : Partieausgang.Niederlage,
        Ergebnis.SchwarzGewinnt => spielerFarbe == Farbe.Schwarz ? Partieausgang.Sieg : Partieausgang.Niederlage,
        _ => throw new ArgumentOutOfRangeException(nameof(ergebnis)),
    };
}
