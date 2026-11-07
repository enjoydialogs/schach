namespace Schach.Domain.Modell;

/// <summary>Eine Partie (SCHACH-SPEC 8.12). Huelle aus Phase 0b: nur Spalten, das Verhalten folgt ab S1c.</summary>
public class Partie
{
    /// <summary>128-Bit-Zufalls-Id (v4), fuer Gaeste zugleich das Zugangsgeheimnis (SCHACH-SPEC 8.3).
    /// Die Anwendung setzt sie, weder EF noch die Datenbank erzeugen sie.</summary>
    public Guid Id { get; set; }

    /// <summary><c>null</c> bei Gast-Partien.</summary>
    public Guid? SpielerId { get; set; }

    /// <summary>Gegner-Key aus dem Gegner-Katalog (kein Fremdschluessel).</summary>
    public string GegnerId { get; set; } = string.Empty;

    public Farbe SpielerFarbe { get; set; }

    public Bedenkzeit Bedenkzeit { get; set; }

    public PartieStatus Status { get; set; }

    public Ergebnis? Ergebnis { get; set; }

    public Grund? Grund { get; set; }

    public DateTimeOffset ErstelltAm { get; set; }

    public DateTimeOffset LetzterZugAm { get; set; }

    public DateTimeOffset? BeendetAm { get; set; }

    public int Halbzuege { get; set; }

    public string Fen { get; set; } = string.Empty;

    public long? SpielerRestMs { get; set; }

    public long? GegnerRestMs { get; set; }

    public DateTimeOffset? AmZugSeit { get; set; }

    public bool ZaehltFuerElo { get; set; }

    public int? LetztesRemisAngebotBeiZug { get; set; }

    public DateTimeOffset? LetzterVersuchAm { get; set; }

    public int? EloVorher { get; set; }

    public int? EloNachher { get; set; }

    /// <summary>Seed des Partie-PRNG (SCHACH-SPEC 5.3 Schritt 6, 8.10).</summary>
    public long Seed { get; set; }

    /// <summary>EF-Concurrency-Token (SCHACH-SPEC 8.8). Die Anwendung erhoeht ihn bei jeder Aenderung.</summary>
    public int Version { get; set; }

    /// <summary>Monoton, beim Zuruecknehmen nicht zurueckgesetzt (SCHACH-SPEC 6.2).</summary>
    public int MistralAufrufe { get; set; }

    /// <summary>Zugfolge; geloescht mit der Partie.</summary>
    public ICollection<Zug> Zuege { get; } = [];

    /// <summary>Sprueche; geloescht mit der Partie.</summary>
    public ICollection<Spruch> Sprueche { get; } = [];
}
