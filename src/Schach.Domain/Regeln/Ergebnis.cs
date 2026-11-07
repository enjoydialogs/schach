namespace Schach.Domain;

/// <summary>Ergebnis einer beendeten Partie (SCHACH-SPEC 3.6). Namen wie das Wire-Enum.</summary>
public enum Ergebnis
{
    WeissGewinnt,
    SchwarzGewinnt,
    Remis,
}

/// <summary>Grund des Partieendes (SCHACH-SPEC 3.6). Namen wie das Wire-Enum und der PGN-Tag <c>Termination</c>.</summary>
public enum Grund
{
    Matt,
    Aufgabe,
    Zeit,
    Patt,
    Wiederholung,
    FuenfzigZuege,
    Material,
    ZeitOhneMattmaterial,
    Vereinbarung,
}

/// <summary>Ergebnis und Grund eines Partieendes.</summary>
public sealed record Partieende(Ergebnis Ergebnis, Grund Grund)
{
    /// <summary>Remis mit Grund.</summary>
    public static Partieende Remis(Grund grund) => new(Ergebnis.Remis, grund);

    /// <summary>Sieg der gegebenen Seite mit Grund.</summary>
    public static Partieende Sieg(Farbe sieger, Grund grund) =>
        new(sieger == Farbe.Weiss ? Ergebnis.WeissGewinnt : Ergebnis.SchwarzGewinnt, grund);
}
