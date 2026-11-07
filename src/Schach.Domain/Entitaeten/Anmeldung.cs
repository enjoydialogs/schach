namespace Schach.Domain.Entitaeten;

/// <summary>Eine offene Magic-Link-Anmeldung (SCHACH-SPEC 9.2, 8.12). <see cref="Id"/> ist Zufall (v4).</summary>
public class Anmeldung
{
    public Guid Id { get; set; }

    public string Email { get; set; } = "";

    /// <summary>SHA-256 des Link-Tokens, 32 Byte, eindeutig.</summary>
    public byte[] TokenHash { get; set; } = [];

    /// <summary>SHA-256 des Codes, 32 Byte.</summary>
    public byte[] CodeHash { get; set; } = [];

    public short Fehlversuche { get; set; }

    public DateTimeOffset ErstelltAm { get; set; }

    public DateTimeOffset LaeuftAbAm { get; set; }
}
