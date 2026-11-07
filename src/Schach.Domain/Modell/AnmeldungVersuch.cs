namespace Schach.Domain.Modell;

/// <summary>Eine angenommene Anmelde-Anfrage fuer die Mail-Limits je Adresse (SCHACH-SPEC 8.12, 11.3).
/// Bleibt 24 h.</summary>
public class AnmeldungVersuch
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 der normalisierten Adresse (lowercase, <c>+tag</c> gestrichen), 32 Byte.
    /// Nie die Adresse selbst.</summary>
    public byte[] EmailHash { get; set; } = [];

    public DateTimeOffset ErstelltAm { get; set; }
}
