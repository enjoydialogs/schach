namespace Schach.Domain.Entitaeten;

/// <summary>
/// Ein Spruch des Gegners (SCHACH-SPEC 6.7, 8.12). Nach einem Halbzug sind mehrere moeglich, darum
/// ein eigener Ersatzschluessel <see cref="Id"/> und kein Unique-Index auf <c>(PartieId, NachHalbzug)</c>.
/// </summary>
public class Spruch
{
    /// <summary>Von der Datenbank vergeben.</summary>
    public long Id { get; set; }

    public Guid PartieId { get; set; }

    public int NachHalbzug { get; set; }

    public SpruchEreignis Ereignis { get; set; }

    public string Text { get; set; } = "";

    public SpruchQuelle Quelle { get; set; }
}
