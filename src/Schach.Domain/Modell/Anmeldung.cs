namespace Schach.Domain.Modell;

/// <summary>Eine offene Magic-Link-Anfrage (SCHACH-SPEC 9.2).</summary>
public class Anmeldung
{
    /// <summary>Zufall v4, Geheimnis des anfragenden Geraets. Die Anwendung setzt sie,
    /// weder EF noch die Datenbank erzeugen sie.</summary>
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>SHA-256 des Tokens, 32 Byte.</summary>
    public byte[] TokenHash { get; set; } = [];

    /// <summary>SHA-256(Id-Bytes || Code), 32 Byte.</summary>
    public byte[] CodeHash { get; set; } = [];

    public short Fehlversuche { get; set; }

    public DateTimeOffset ErstelltAm { get; set; }

    public DateTimeOffset LaeuftAbAm { get; set; }
}
