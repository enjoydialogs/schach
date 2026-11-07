namespace Schach.Domain.Entitaeten;

/// <summary>Ein Mensch mit Konto (SCHACH-SPEC 8.12, 9). Loeschen kaskadiert auf alle abhaengigen Zeilen.</summary>
public class Spieler
{
    public Guid Id { get; set; }

    /// <summary>Getrimmt und klein geschrieben, eindeutig.</summary>
    public string Email { get; set; } = "";

    public DateTimeOffset ErstelltAm { get; set; }

    public DateTimeOffset? ZuletztAngemeldetAm { get; set; }

    /// <summary>Wechselt, um alle Sitzungen des Spielers zu entwerten (SCHACH-SPEC 9).</summary>
    public Guid Sicherheitsstempel { get; set; }

    public int Elo { get; set; }
}
