namespace Schach.Domain.Modell;

/// <summary>Ein Spieler mit Konto (SCHACH-SPEC 8.12, 9.1). Entsteht erst bei der Bestaetigung.</summary>
public class Spieler
{
    public Guid Id { get; set; }

    /// <summary>Getrimmt und kleingeschrieben, eindeutig.</summary>
    public string Email { get; set; } = string.Empty;

    public DateTimeOffset ErstelltAm { get; set; }

    public DateTimeOffset? ZuletztAngemeldetAm { get; set; }

    /// <summary>Neu gesetzt bei „ueberall abmelden“ (SCHACH-SPEC 9.4).</summary>
    public Guid Sicherheitsstempel { get; set; }

    public int Elo { get; set; }
}
