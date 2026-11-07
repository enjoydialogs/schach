namespace Schach.Domain.Entitaeten;

/// <summary>
/// Ton-Cache der Sprachausgabe (SCHACH-SPEC 6.9, 8.12). Schluessel = SHA-256 ueber Modell, Voice-Id
/// und Text; haengt an keiner Partie, damit der Schlussspruch einer geloeschten Gast-Partie bleibt.
/// Sweep-Block E loescht Eintraege nach 90 Tagen ohne Nutzung.
/// </summary>
public class SpruchStimme
{
    /// <summary>SHA-256, 32 Byte.</summary>
    public byte[] Hash { get; set; } = [];

    /// <summary>MP3.</summary>
    public byte[] Audio { get; set; } = [];

    public int Zeichen { get; set; }

    public DateTimeOffset ErstelltAm { get; set; }

    public DateTimeOffset ZuletztGenutztAm { get; set; }
}

/// <summary>
/// Zaehlwerk der Sprachausgabe je Monat (SCHACH-SPEC 8.12, 11.1), getrennt von <see cref="LlmVerbrauch"/>.
/// Atomar per <c>INSERT … ON CONFLICT DO UPDATE SET x = x + n</c>.
/// </summary>
public class SprachausgabeVerbrauch
{
    /// <summary>Erster Tag des Monats (UTC).</summary>
    public DateOnly Monat { get; set; }

    public long Zeichen { get; set; }

    public int Aufrufe { get; set; }

    public int Zeitueberschreitungen { get; set; }

    public int Fehler { get; set; }
}
