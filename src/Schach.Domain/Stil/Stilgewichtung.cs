namespace Schach.Domain.Stil;

/// <summary>Zugmerkmale nach Spec 5.3 Schritt 4.</summary>
[Flags]
public enum Zugmerkmal
{
    Keine = 0,
    Schlagzug = 1 << 0,
    Schach = 1 << 1,
    Koenigszone = 1 << 2,
    Riskant = 1 << 3,
    Tausch = 1 << 4,
    Rueckzug = 1 << 5,
    Entwicklung = 1 << 6,
    Ungewoehnlich = 1 << 7,
}

/// <summary>Ein Eintrag der Maia-Policy: erlaubter Zug in UCI mit Wahrscheinlichkeit <c>p</c>.</summary>
public sealed record Policyzug(string Uci, double P);

/// <summary>Stil-Einstellung eines Gegners für eine Zugwahl.</summary>
/// <param name="Temperatur">τ je Spielstärke (Start 1,0, aus der Eichung).</param>
/// <param name="BandFaktor"><c>r</c>: Band C = Züge mit <c>p ≥ r · p_max</c>.</param>
/// <param name="BandMax">Höchstens so viele Züge im Band.</param>
/// <param name="GewichtMin">Untere Grenze für <c>w</c>.</param>
/// <param name="GewichtMax">Obere Grenze für <c>w</c> (Eichung 5.4 b darf sie auf 1,5 senken).</param>
public sealed record Stilparameter(
    Stilgewichte Gewichte,
    double Temperatur,
    double BandFaktor = 0.25,
    int BandMax = 8,
    double GewichtMin = 0.5,
    double GewichtMax = 2.0)
{
    /// <summary>Stil eines Gegners: Gewichte aus seiner Persönlichkeit, τ aus der Eichung seiner Spielstärke.</summary>
    public static Stilparameter Fuer(Gegner gegner, Eichung eichung)
    {
        ArgumentNullException.ThrowIfNull(gegner);
        ArgumentNullException.ThrowIfNull(eichung);
        return new Stilparameter(gegner.Gewichte, eichung.Temperatur);
    }
}

/// <summary>Lage für die Remis-Vermeidung (Spec 5.3 Schritt 4a, 3.5).</summary>
/// <param name="Materialbilanz">Aus Sicht des Gegners vor seinem Zug (<see cref="Materialbilanz"/>).</param>
/// <param name="LoestSofortRemisAus">Wahr, wenn der Zug sofort Remis auslöst (Patt, Material,
/// Wiederholung, 50 Züge). Liefert der Aufrufer aus <c>Remisregeln</c>.</param>
public sealed record Remislage(int Materialbilanz, Func<string, bool> LoestSofortRemisAus)
{
    /// <summary><c>KlarVornMaterial</c>: ab dieser Bilanz gilt der Gegner als klar vorn, ab der negativen als klar hinten.</summary>
    public const int KlarVornMaterial = 2;

    /// <summary>Ausgeglichen, ohne Remisprüfung (z. B. für Tests oder den Fairy-Pfad).</summary>
    public static Remislage Keine { get; } = new(0, _ => false);
}

/// <summary>Ergebnis der Zugwahl; wird je Zug gespeichert (Spec 5.3 Schritt 7).</summary>
/// <param name="MaiaRang">1 = höchstes <c>p</c>.</param>
/// <param name="PAnteil"><c>p / p_max</c>.</param>
/// <param name="W">Stilgewicht <c>w</c>; außerhalb des Bandes 1,0.</param>
public sealed record Stilwahl(string Uci, int MaiaRang, double PAnteil, double W);

/// <summary>
/// Stil-Algorithmus (Spec 5.3). Reine Funktion: gleiche Policy, Parameter, Merkmale, Seed und Halbzug
/// ergeben immer denselben Zug.
/// <para>
/// Auslegung des Ziehpools: Gezogen wird einmal aus <c>q</c> über alle erlaubten Züge (außerhalb
/// des Bandes <c>q = p</c>), damit auch schwächere menschliche Züge möglich bleiben. Die
/// Remis-Vermeidung wirkt auf den Pool: klar vorn fallen Remiszüge heraus (außer das Band bestünde
/// nur aus ihnen; dann bleibt es unverändert), klar hinten wird der Pool auf die Remiszüge im Band
/// beschränkt, falls es welche gibt.
/// </para>
/// </summary>
public static class Stilgewichtung
{
    /// <summary>Ab diesem Anteil von <c>p_max</c> abwärts gilt ein Zug als ungewöhnlich (<c>p &lt; 0,5 · p_max</c>).</summary>
    public const double UngewoehnlichAnteil = 0.5;

    /// <summary>Band C in fester Reihenfolge (p absteigend, dann UCI).</summary>
    public static IReadOnlyList<string> Kandidatenband(IReadOnlyList<Policyzug> policy, Stilparameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        var sortiert = Sortiere(policy);
        var pMax = sortiert[0].P;
        return sortiert.Where(z => z.P >= parameter.BandFaktor * pMax).Take(parameter.BandMax).Select(z => z.Uci).ToList();
    }

    /// <summary>Brettmerkmale des Zugs plus <see cref="Zugmerkmal.Ungewoehnlich"/> aus der Policy.</summary>
    public static Zugmerkmal Merkmale(IReadOnlyList<Policyzug> policy, string uci, Func<string, Zugmerkmal> brettmerkmale)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(brettmerkmale);
        var pMax = policy.Max(z => z.P);
        var p = policy.First(z => string.Equals(z.Uci, uci, StringComparison.Ordinal)).P;
        var merkmale = brettmerkmale(uci) & ~Zugmerkmal.Ungewoehnlich;
        return p < UngewoehnlichAnteil * pMax ? merkmale | Zugmerkmal.Ungewoehnlich : merkmale;
    }

    /// <summary><c>w</c> = Produkt der Gewichte aller vorhandenen Merkmale, begrenzt auf [min; max].</summary>
    public static double Gewicht(Zugmerkmal merkmale, Stilgewichte gewichte, double min = 0.5, double max = 2.0)
    {
        ArgumentNullException.ThrowIfNull(gewichte);
        var w = 1.0;
        if (merkmale.HasFlag(Zugmerkmal.Schlagzug)) w *= gewichte.Schlagzug;
        if (merkmale.HasFlag(Zugmerkmal.Schach)) w *= gewichte.Schach;
        if (merkmale.HasFlag(Zugmerkmal.Koenigszone)) w *= gewichte.Koenigszone;
        if (merkmale.HasFlag(Zugmerkmal.Riskant)) w *= gewichte.Riskant;
        if (merkmale.HasFlag(Zugmerkmal.Tausch)) w *= gewichte.Tausch;
        if (merkmale.HasFlag(Zugmerkmal.Rueckzug)) w *= gewichte.Rueckzug;
        if (merkmale.HasFlag(Zugmerkmal.Entwicklung)) w *= gewichte.Entwicklung;
        if (merkmale.HasFlag(Zugmerkmal.Ungewoehnlich)) w *= gewichte.Ungewoehnlich;
        return Math.Clamp(w, min, max);
    }

    /// <summary>Wählt den Gegnerzug (Spec 5.3 Schritte 2 bis 7).</summary>
    /// <param name="policy">Maia-Policy über alle erlaubten Züge.</param>
    /// <param name="brettmerkmale">Merkmale je UCI-Zug, in der Regel <see cref="Zugmerkmale.Bestimme"/>.</param>
    /// <param name="seed">Partie-Seed.</param>
    /// <param name="halbzug">Halbzug, für den gewählt wird.</param>
    public static Stilwahl Waehle(
        IReadOnlyList<Policyzug> policy,
        Stilparameter parameter,
        Func<string, Zugmerkmal> brettmerkmale,
        Remislage remis,
        long seed,
        int halbzug)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(brettmerkmale);
        ArgumentNullException.ThrowIfNull(remis);
        if (parameter.Temperatur <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parameter), "Die Temperatur muss positiv sein.");
        }

        var sortiert = Sortiere(policy);
        var pMax = sortiert[0].P;
        var band = Kandidatenband(sortiert, parameter).ToHashSet(StringComparer.Ordinal);

        var pool = Remisfilter(sortiert, band, remis);

        var gewichte = new double[pool.Count];
        var w = new double[pool.Count];
        for (var i = 0; i < pool.Count; i++)
        {
            var zug = pool[i];
            w[i] = band.Contains(zug.Uci)
                ? Gewicht(Merkmale(sortiert, zug.Uci, brettmerkmale), parameter.Gewichte, parameter.GewichtMin, parameter.GewichtMax)
                : 1.0;
            var q = zug.P * w[i];
            // Temperatur relativ zum Bandkopf, damit kleine τ nicht nach 0 unterlaufen.
            gewichte[i] = q <= 0 ? 0 : Math.Pow(q / pMax, 1.0 / parameter.Temperatur);
        }

        var gewaehlt = Ziehe(gewichte, Partiezufall.Zahl(seed, halbzug, Zufallszweck.Zugwahl));
        var uci = pool[gewaehlt].Uci;
        var rang = sortiert.FindIndex(z => string.Equals(z.Uci, uci, StringComparison.Ordinal)) + 1;
        return new Stilwahl(uci, rang, pool[gewaehlt].P / pMax, w[gewaehlt]);
    }

    private static List<Policyzug> Remisfilter(List<Policyzug> sortiert, HashSet<string> band, Remislage remis)
    {
        if (remis.Materialbilanz >= Remislage.KlarVornMaterial)
        {
            var bandOhneRemis = sortiert.Any(z => band.Contains(z.Uci) && !remis.LoestSofortRemisAus(z.Uci));
            if (!bandOhneRemis)
            {
                // C wird nie leer: das Band bleibt, wie es ist; Remiszüge außerhalb fallen weg.
                return sortiert.Where(z => band.Contains(z.Uci) || !remis.LoestSofortRemisAus(z.Uci)).ToList();
            }

            return sortiert.Where(z => !remis.LoestSofortRemisAus(z.Uci)).ToList();
        }

        if (remis.Materialbilanz <= -Remislage.KlarVornMaterial)
        {
            var remisImBand = sortiert.Where(z => band.Contains(z.Uci) && remis.LoestSofortRemisAus(z.Uci)).ToList();
            if (remisImBand.Count > 0)
            {
                return remisImBand;
            }
        }

        return sortiert;
    }

    private static int Ziehe(double[] gewichte, double zufall)
    {
        var summe = gewichte.Sum();
        if (summe <= 0)
        {
            return 0;
        }

        var ziel = zufall * summe;
        var kumuliert = 0.0;
        for (var i = 0; i < gewichte.Length; i++)
        {
            kumuliert += gewichte[i];
            if (ziel < kumuliert)
            {
                return i;
            }
        }

        return Array.FindLastIndex(gewichte, g => g > 0);
    }

    private static List<Policyzug> Sortiere(IReadOnlyList<Policyzug> policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.Count == 0)
        {
            throw new ArgumentException("Die Policy enthält keinen Zug.", nameof(policy));
        }

        return policy.OrderByDescending(z => z.P).ThenBy(z => z.Uci, StringComparer.Ordinal).ToList();
    }
}
