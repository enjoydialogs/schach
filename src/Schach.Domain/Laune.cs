namespace Schach.Domain;

/// <summary>Wie frech ein Gegner über den Spieler reden darf (Spec 4.2). Harte Grenzen gelten für jede Frechheit.</summary>
public enum Frechheit
{
    /// <summary>Über den Spieler nur neutral oder freundlich.</summary>
    Freundlich = 0,

    /// <summary>Darf Züge des Spielers mit einem Augenzwinkern necken.</summary>
    Neckend = 1,

    /// <summary>Darf angeben, sticheln und nerven, solange es um Züge und Figuren geht.</summary>
    Vorlaut = 2,
}

/// <summary>Laune eines einzelnen Spruchs (Feld <c>laune</c> im Prompt, Spec 6.3).</summary>
public enum Laune
{
    Trocken,
    Lustig,

    /// <summary>Erst ab <see cref="Frechheit.Neckend"/>.</summary>
    Vorlaut,

    /// <summary>Erst ab <see cref="Frechheit.Vorlaut"/>.</summary>
    Genervt,
}

/// <summary>Relative Gewichte der Launen eines Gegners. Launen über seiner Frechheit zählen nie.</summary>
public sealed record Launengewichte(double Trocken, double Lustig, double Vorlaut, double Genervt);

/// <summary>Wählt die Laune je Spruch aus dem Partie-PRNG (Spec 6.3). Deterministisch je Seed und Halbzug.</summary>
public static class Launenwahl
{
    public static Laune Waehle(Gegner gegner, long seed, int halbzug)
    {
        ArgumentNullException.ThrowIfNull(gegner);
        return Waehle(gegner.Frechheit, gegner.Launen, seed, halbzug);
    }

    public static Laune Waehle(Frechheit frechheit, Launengewichte gewichte, long seed, int halbzug)
    {
        ArgumentNullException.ThrowIfNull(gewichte);
        (Laune Laune, double Gewicht)[] kandidaten =
        [
            (Laune.Trocken, gewichte.Trocken),
            (Laune.Lustig, gewichte.Lustig),
            (Laune.Vorlaut, frechheit >= Frechheit.Neckend ? gewichte.Vorlaut : 0),
            (Laune.Genervt, frechheit >= Frechheit.Vorlaut ? gewichte.Genervt : 0),
        ];

        var summe = kandidaten.Sum(k => Math.Max(0, k.Gewicht));
        if (summe <= 0)
        {
            return Laune.Trocken;
        }

        var ziel = Partiezufall.Zahl(seed, halbzug, Zufallszweck.Laune) * summe;
        foreach (var (laune, gewicht) in kandidaten)
        {
            ziel -= Math.Max(0, gewicht);
            if (ziel < 0)
            {
                return laune;
            }
        }

        return kandidaten.Last(k => k.Gewicht > 0).Laune;
    }
}
