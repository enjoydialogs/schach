namespace Schach.Domain.Modell;

/// <summary>Ein Spruch des Gegners (SCHACH-SPEC 6.7, 8.12). Mehrere Sprueche je Halbzug sind erlaubt
/// (Partieende durch einen Spielerzug), darum ein eigener Schluessel.</summary>
public class Spruch
{
    public long Id { get; set; }

    public Guid PartieId { get; set; }

    public int NachHalbzug { get; set; }

    public SpruchEreignis Ereignis { get; set; }

    public string Text { get; set; } = string.Empty;

    public SpruchQuelle Quelle { get; set; }
}
