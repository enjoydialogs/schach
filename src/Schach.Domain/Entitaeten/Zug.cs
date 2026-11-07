namespace Schach.Domain.Entitaeten;

/// <summary>Ein Halbzug einer Partie (SCHACH-SPEC 8.12). Schluessel <c>(PartieId, Halbzug)</c>.</summary>
public class Zug
{
    public Guid PartieId { get; set; }

    /// <summary>1 fuer den ersten Halbzug.</summary>
    public int Halbzug { get; set; }

    public Farbe Seite { get; set; }

    public string Uci { get; set; } = "";

    public string San { get; set; } = "";

    public string FenDanach { get; set; } = "";

    public DateTimeOffset GespieltAm { get; set; }

    /// <summary>Nur bei Gegnerzuegen.</summary>
    public ZugEngine? Engine { get; set; }

    /// <summary>Rang des gewaehlten Zugs in Maias Policy (SCHACH-SPEC 5.3).</summary>
    public int? MaiaRang { get; set; }

    /// <summary><c>p / p_max</c> des gewaehlten Zugs.</summary>
    public double? PAnteil { get; set; }

    /// <summary>Gewinnwahrscheinlichkeit <c>w</c> aus Sicht des Gegners.</summary>
    public double? W { get; set; }

    /// <summary>Volles <c>wdl</c> der Engine, als JSON gespeichert.</summary>
    public Wdl? Wdl { get; set; }
}

/// <summary>Gewinn, Remis, Verlust aus Sicht der Seite am Zug (engine/API.md).</summary>
public class Wdl
{
    public double W { get; set; }

    public double D { get; set; }

    public double L { get; set; }
}
