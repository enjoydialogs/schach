namespace Schach.Domain.Entitaeten;

/// <summary>Siege, Remis und Niederlagen eines Spielers gegen einen Gegner (SCHACH-SPEC 3.8, 8.12).
/// Schluessel <c>(SpielerId, GegnerId)</c>.</summary>
public class Bilanz
{
    public Guid SpielerId { get; set; }

    /// <summary>Gegner-Key aus dem Katalog, kein Fremdschluessel.</summary>
    public string GegnerId { get; set; } = "";

    public int Siege { get; set; }

    public int Remis { get; set; }

    public int Niederlagen { get; set; }

    /// <summary>Erster Sieg, der fuer die Elo zaehlte.</summary>
    public DateTimeOffset? BesiegtAm { get; set; }
}
