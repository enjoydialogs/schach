using System.Globalization;

namespace Schach.Domain;

/// <summary>Ausgang einer Partie aus Sicht des Spielers.</summary>
public enum Partieausgang
{
    Sieg,
    Remis,
    Niederlage,
}

/// <summary>Elo nach SCHACH-SPEC 3.7: Standardformel, K = 32 fest, ganzzahlig, Untergrenze 100.</summary>
public static class Elo
{
    /// <summary>Startwert eines neuen Spielers; zugleich die angenommene Elo eines Gastes.</summary>
    public const int Start = 800;

    public const int K = 32;

    public const int Untergrenze = 100;

    /// <summary>Erwartung <c>E = 1 / (1 + 10^((EloGegner − EloSpieler) / 400))</c>.</summary>
    public static double Erwartung(int eloSpieler, int eloGegner) =>
        1.0 / (1.0 + Math.Pow(10, (eloGegner - eloSpieler) / 400.0));

    /// <summary>Elo des Spielers nach der Partie.</summary>
    public static int Neu(int eloSpieler, int eloGegner, Partieausgang ausgang)
    {
        var s = ausgang switch
        {
            Partieausgang.Sieg => 1.0,
            Partieausgang.Remis => 0.5,
            Partieausgang.Niederlage => 0.0,
            _ => throw new ArgumentOutOfRangeException(nameof(ausgang)),
        };
        var delta = Runde(K * (s - Erwartung(eloSpieler, eloGegner)));
        return Math.Max(Untergrenze, eloSpieler + delta);
    }

    /// <summary><c>(int)Math.Round(x, MidpointRounding.AwayFromZero)</c>.</summary>
    public static int Runde(double wert) => (int)Math.Round(wert, MidpointRounding.AwayFromZero);

    /// <summary>„+12“, „−8“ (U+2212), „±0“.</summary>
    public static string FormatiereAenderung(int delta) => delta switch
    {
        > 0 => "+" + delta.ToString(CultureInfo.InvariantCulture),
        < 0 => "−" + (-delta).ToString(CultureInfo.InvariantCulture),
        _ => "±0",
    };
}
