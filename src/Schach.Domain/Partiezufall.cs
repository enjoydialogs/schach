namespace Schach.Domain;

/// <summary>Wofür eine Zufallszahl der Partie gezogen wird. Jeder Zweck hat einen eigenen Strom,
/// damit etwa Denkzeit und Zugwahl nicht miteinander korrelieren. Werte nie ändern: sie sind Teil
/// der Determinismus-Zusage (Spec 5.3 Schritt 6, 8.10).</summary>
public enum Zufallszweck
{
    Zugwahl = 1,
    Denkzeit = 2,
}

/// <summary>
/// Deterministischer PRNG der Partie: eine Zahl aus (Seed, Halbzug, Zweck). Eigener SplitMix64-Mix
/// statt <see cref="Random"/>, weil dessen Algorithmus über .NET-Versionen nicht zugesichert ist.
/// Gleiche Eingaben ergeben auf jeder Plattform dieselbe Zahl.
/// </summary>
public static class Partiezufall
{
    /// <summary>Gleichverteilte Zahl in [0, 1).</summary>
    public static double Zahl(long seed, int halbzug, Zufallszweck zweck)
    {
        var x = Mix((ulong)seed);
        x = Mix(x ^ (uint)halbzug);
        x = Mix(x ^ ((ulong)(uint)zweck << 32));
        // 53 Bit Mantisse.
        return (x >> 11) * (1.0 / (1UL << 53));
    }

    private static ulong Mix(ulong z)
    {
        unchecked
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}

/// <summary>Künstliche Denkzeit D des Gegners (Spec 8.6).</summary>
public static class Gegnerdenkzeit
{
    public const int MinMs = 1_000;

    public const int MaxMs = 3_000;

    /// <summary>D in [1 000; 3 000] ms aus dem Partie-PRNG; mit Uhr gedeckelt auf 10 % von
    /// <paramref name="gegnerRestMs"/>.</summary>
    /// <param name="gegnerRestMs"><c>null</c> ohne Uhr.</param>
    public static int Ms(long seed, int halbzug, long? gegnerRestMs)
    {
        var d = MinMs + (int)Math.Floor(Partiezufall.Zahl(seed, halbzug, Zufallszweck.Denkzeit) * (MaxMs - MinMs + 1));
        if (gegnerRestMs is { } rest)
        {
            d = (int)Math.Min(d, Math.Max(0, rest / 10));
        }

        return d;
    }
}
