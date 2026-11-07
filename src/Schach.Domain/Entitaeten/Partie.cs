namespace Schach.Domain.Entitaeten;

/// <summary>
/// Eine Partie (SCHACH-SPEC 8.12). Huelle aus Session 0b: nur die gespeicherten Spalten, das
/// Verhalten (Starten, Zug, Beenden) folgt ab S1c. <see cref="Id"/> vergibt der Aufrufer per
/// <see cref="Guid.NewGuid"/> (128 Bit Zufall), weil sie fuer Gaeste zugleich der Zugriffsschluessel ist.
/// </summary>
public class Partie
{
    public Guid Id { get; set; }

    /// <summary><c>null</c> bei Gast-Partien.</summary>
    public Guid? SpielerId { get; set; }

    /// <summary>Gegner-Key aus dem Katalog, kein Fremdschluessel.</summary>
    public string GegnerId { get; set; } = "";

    public Farbe SpielerFarbe { get; set; }

    public Bedenkzeit Bedenkzeit { get; set; }

    public PartieStatus Status { get; set; }

    public Ergebnis? Ergebnis { get; set; }

    public Grund? Grund { get; set; }

    public DateTimeOffset ErstelltAm { get; set; }

    public DateTimeOffset LetzterZugAm { get; set; }

    public DateTimeOffset? BeendetAm { get; set; }

    /// <summary>Zahl der gespielten Halbzuege, zugleich das optimistische Token <c>halbzug</c>.</summary>
    public int Halbzuege { get; set; }

    public string Fen { get; set; } = "";

    public long? SpielerRestMs { get; set; }

    public long? GegnerRestMs { get; set; }

    public DateTimeOffset? AmZugSeit { get; set; }

    public bool ZaehltFuerElo { get; set; }

    public int? LetztesRemisAngebotBeiZug { get; set; }

    public DateTimeOffset? LetzterVersuchAm { get; set; }

    public int? EloVorher { get; set; }

    public int? EloNachher { get; set; }

    /// <summary>Seed des Partie-PRNG (SCHACH-SPEC 8.10).</summary>
    public long Seed { get; set; }

    /// <summary>EF-Concurrency-Token (SCHACH-SPEC 8.8). Jede Aenderung zaehlt ihn hoch.</summary>
    public int Version { get; set; }

    public int MistralAufrufe { get; set; }
}
