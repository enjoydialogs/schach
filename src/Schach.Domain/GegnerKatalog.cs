namespace Schach.Domain;

// Gegner-Katalog nach SCHACH-SPEC 4.1 bis 4.3. Die Enum-Namen entsprechen den Wire-Enums in
// Schach.Api/Contracts/WireEnums.cs (Spec 8.4a), damit die Abbildung trivial bleibt.

/// <summary>Spielstärke eines Gegners (Feld <c>stufe</c>, Spec 4.1).</summary>
public enum Stufe
{
    Anfaenger,
    Hobbyspieler,
    Vereinsspieler,
    Turnierspieler,
    Meister,
}

/// <summary>Persönlichkeit eines Gegners (Spec 4.2).</summary>
public enum Persoenlichkeit
{
    Aggressiv,
    Vorsichtig,
    Lehrerhaft,
    Verspielt,
    Eitel,
    Gemuetlich,
}

/// <summary>Ein Gegner mit fester Elo (Produktkonstante) und Stilgewichten.</summary>
/// <param name="Key">Stabile Id in DB und API.</param>
public sealed record Gegner(
    string Key,
    string Name,
    int Elo,
    Stufe Stufe,
    Persoenlichkeit Persoenlichkeit,
    string Vorstellung,
    Stilgewichte Gewichte);

/// <summary>Die acht Gegner, Elo aufsteigend (Spec 4.3).</summary>
public static class GegnerKatalog
{
    public static IReadOnlyList<Gegner> Alle { get; } =
    [
        Neu("ilse", "Ilse", 600, Stufe.Anfaenger, Persoenlichkeit.Gemuetlich, "Keine Eile. Wir spielen in aller Ruhe, ja?"),
        Neu("finn", "Finn", 800, Stufe.Anfaenger, Persoenlichkeit.Aggressiv, "Ich greif einfach immer an!"),
        Neu("oscar", "Oscar", 1000, Stufe.Hobbyspieler, Persoenlichkeit.Lehrerhaft, "Ich erklär dir hinterher gern, warum ich so gezogen hab."),
        Neu("nele", "Nele", 1200, Stufe.Hobbyspieler, Persoenlichkeit.Verspielt, "Mal gucken, was passiert, wenn ich das probiere."),
        Neu("theo", "Theo", 1400, Stufe.Vereinsspieler, Persoenlichkeit.Vorsichtig, "Sicher ist sicher. Ich decke erst mal alles ab."),
        Neu("sementa", "Sementa", 1600, Stufe.Vereinsspieler, Persoenlichkeit.Eitel, "Elegant, oder? So sind meine Züge immer."),
        Neu("konrad", "Konrad", 1900, Stufe.Turnierspieler, Persoenlichkeit.Lehrerhaft, "Erst die Grundsätze, dann der Angriff."),
        Neu("maren", "Maren", 2200, Stufe.Meister, Persoenlichkeit.Aggressiv, "Ich opfere gern eine Figur für den Angriff."),
    ];

    /// <summary>Gegner zum Key oder <c>null</c>.</summary>
    public static Gegner? Finde(string key) => Alle.FirstOrDefault(g => string.Equals(g.Key, key, StringComparison.Ordinal));

    private static Gegner Neu(string key, string name, int elo, Stufe stufe, Persoenlichkeit persoenlichkeit, string vorstellung) =>
        new(key, name, elo, stufe, persoenlichkeit, vorstellung, Stilgewichte.Fuer(persoenlichkeit));
}

/// <summary>Gewichtsvektor über die Zugmerkmale (Spec 4.2, 1,0 = neutral).</summary>
public sealed record Stilgewichte(
    double Schlagzug,
    double Schach,
    double Koenigszone,
    double Riskant,
    double Tausch,
    double Rueckzug,
    double Entwicklung,
    double Ungewoehnlich)
{
    /// <summary>Alle Gewichte 1,0 (Eichung a, Stil-A/B-Gegenseite).</summary>
    public static Stilgewichte Neutral { get; } = new(1, 1, 1, 1, 1, 1, 1, 1);

    /// <summary>Startgewichte der Persönlichkeit (Tabelle Spec 4.2).</summary>
    /// <param name="unabhaengigkeitGeprueft">Erst wenn Eichung 5.4 b für Verspielt besteht, gilt
    /// Ungewöhnlich = 1,5; vorher 1,0.</param>
    public static Stilgewichte Fuer(Persoenlichkeit persoenlichkeit, bool unabhaengigkeitGeprueft = false) => persoenlichkeit switch
    {
        Persoenlichkeit.Aggressiv => new(1.4, 1.5, 1.5, 1.3, 0.7, 0.6, 1.0, 1.0),
        Persoenlichkeit.Vorsichtig => new(1.0, 0.9, 0.7, 0.5, 1.0, 1.2, 1.2, 0.7),
        Persoenlichkeit.Lehrerhaft => new(1.0, 1.0, 1.0, 0.8, 1.0, 1.0, 1.5, 0.5),
        Persoenlichkeit.Verspielt => new(1.1, 1.2, 1.0, 1.4, 0.8, 1.0, 0.9, unabhaengigkeitGeprueft ? 1.5 : 1.0),
        Persoenlichkeit.Eitel => new(1.1, 1.2, 1.2, 1.1, 0.5, 1.0, 1.0, 1.2),
        Persoenlichkeit.Gemuetlich => new(0.9, 0.9, 0.8, 0.6, 1.5, 1.0, 1.0, 0.8),
        _ => throw new ArgumentOutOfRangeException(nameof(persoenlichkeit)),
    };
}

/// <summary>Welche Engine den Gegnerzug rechnet (Spiegel von <c>EngineArt</c> in Application).</summary>
public enum GegnerEngine
{
    Maia,
    Fairy,
}

/// <summary>Engine-Einstellung je Gegner (Konfiguration <c>Engine:Eichung</c>, Spec 5.4). Die Eichung
/// stellt nur die Engine ein, nie die Elo des Gegners.</summary>
public sealed record Eichung(int SelfElo, double Temperatur, GegnerEngine Engine)
{
    /// <summary>Platzhalter bis zur Eichung: <c>selfElo = elo</c>, <c>temperatur = 1.0</c>, Maia.</summary>
    public static Eichung Identitaet(Gegner gegner)
    {
        ArgumentNullException.ThrowIfNull(gegner);
        return new(gegner.Elo, 1.0, GegnerEngine.Maia);
    }
}
