namespace Schach.Domain.Modell;

/// <summary>Siege, Remis und Niederlagen eines Spielers gegen einen Gegner (SCHACH-SPEC 3.8).
/// Schluessel ist (SpielerId, GegnerId).</summary>
public class Bilanz
{
    public Guid SpielerId { get; set; }

    /// <summary>Gegner-Key aus dem Gegner-Katalog.</summary>
    public string GegnerId { get; set; } = string.Empty;

    public int Siege { get; set; }

    public int Remis { get; set; }

    public int Niederlagen { get; set; }

    /// <summary>Nur nach einem Sieg, der fuer die Elo zaehlt.</summary>
    public DateTimeOffset? BesiegtAm { get; set; }
}
