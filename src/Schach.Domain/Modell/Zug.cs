namespace Schach.Domain.Modell;

/// <summary>Ein Halbzug einer Partie (SCHACH-SPEC 8.12). Schluessel ist (PartieId, Halbzug).</summary>
public class Zug
{
    public Guid PartieId { get; set; }

    public int Halbzug { get; set; }

    public Farbe Seite { get; set; }

    public string Uci { get; set; } = string.Empty;

    public string San { get; set; } = string.Empty;

    public string FenDanach { get; set; } = string.Empty;

    public DateTimeOffset GespieltAm { get; set; }

    /// <summary>Nur bei Gegnerzuegen.</summary>
    public ZugEngine? Engine { get; set; }

    public int? MaiaRang { get; set; }

    /// <summary>p / p_max (SCHACH-SPEC 5.3 Schritt 7).</summary>
    public double? PAnteil { get; set; }

    /// <summary>Stilgewicht w(m) (SCHACH-SPEC 5.3 Schritt 7).</summary>
    public double? W { get; set; }

    /// <summary>Maia-Gewinnwahrscheinlichkeit aus Sicht des Gegners nach diesem Zug, fuer den
    /// Bewertungssprung (SCHACH-SPEC 6.1).</summary>
    public double? Wdl { get; set; }
}
