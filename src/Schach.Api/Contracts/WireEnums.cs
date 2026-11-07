namespace Schach.Api.Contracts;

// Wire-Enums nach SCHACH-SPEC 8.4a. Auf dem Draht als PascalCase-Strings (JsonStringEnumConverter
// ohne Naming Policy, siehe ApiJson), nie als Zahlen. Die Namen SIND der Vertrag: nicht umbenennen.
// Anzeigetexte kommen aus src/i18n/de.ts, nie aus diesen Werten.

/// <summary><c>bedenkzeit</c> (SCHACH-SPEC 3.2).</summary>
public enum Bedenkzeit
{
    OhneUhr,
    Fischer15Plus10,
    Fischer10Plus5,
    Fischer5Plus3,
    JeZug90s,
}

/// <summary><c>farbe</c> in der Anfrage von <c>POST /partien</c>.</summary>
public enum FarbeWahl
{
    Weiss,
    Schwarz,
    Zufall,
}

/// <summary><c>spielerFarbe</c> und <c>seite</c>.</summary>
public enum Farbe
{
    Weiss,
    Schwarz,
}

/// <summary><c>uhr.laeuftFuer</c>; <c>null</c>, wenn keine Uhr laeuft.</summary>
public enum UhrSeite
{
    Spieler,
    Gegner,
}

/// <summary><c>status</c> (SCHACH-SPEC 8.2).</summary>
public enum PartieStatus
{
    Offen,
    Beendet,
}

/// <summary><c>ergebnis</c> (SCHACH-SPEC 3.6).</summary>
public enum Ergebnis
{
    WeissGewinnt,
    SchwarzGewinnt,
    Remis,
}

/// <summary><c>grund</c> (SCHACH-SPEC 3.6).</summary>
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

/// <summary><c>ereignis</c> (SCHACH-SPEC 6.1). Zugleich die Ueberschriften in <c>prompts/ersatz/*.de.md</c>.</summary>
public enum SpruchEreignis
{
    Partiebeginn,
    Sieg,
    Niederlage,
    Remis,
    RemisAbgelehnt,
    Zuruecknahme,
    FigurGewonnen,
    FigurVerloren,
    SchachGegeben,
    SchachErhalten,
    StellungBesser,
    StellungSchlechter,
    Tausch,
    Zug,
}

/// <summary><c>stufe</c> (Spielstaerke, SCHACH-SPEC 4.1).</summary>
public enum Stufe
{
    Anfaenger,
    Hobbyspieler,
    Vereinsspieler,
    Turnierspieler,
    Meister,
}

/// <summary><c>persoenlichkeit</c> (SCHACH-SPEC 4.2).</summary>
public enum Persoenlichkeit
{
    Aggressiv,
    Vorsichtig,
    Lehrerhaft,
    Verspielt,
    Eitel,
    Gemuetlich,
}

/// <summary><c>tipp.figur</c>.</summary>
public enum Figur
{
    Koenig,
    Dame,
    Turm,
    Laeufer,
    Springer,
    Bauer,
}

/// <summary>
/// <c>fehler</c> in jeder Fehlerantwort (SCHACH-SPEC 8.3, 8.5, 9.3, 11). Der HTTP-Status je Code
/// steht im Kommentar; der Client entscheidet ueber den Code, nie ueber den Status allein.
/// </summary>
public enum FehlerCode
{
    /// <summary>409, mit Zustand.</summary>
    NichtAmZug,

    /// <summary>409, mit Zustand.</summary>
    HalbzugVeraltet,

    /// <summary>409, mit Zustand.</summary>
    PartieBeendet,

    /// <summary>409, mit Zustand.</summary>
    UhrAbgelaufen,

    /// <summary>409, mit <c>offenePartieId</c>.</summary>
    OffenePartie,

    /// <summary>409, mit Zustand.</summary>
    ZuruecknehmenNichtErlaubt,

    /// <summary>409, mit Zustand.</summary>
    RemisAngebotGesperrt,

    /// <summary>409, mit Zustand (Partie mit Bedenkzeit).</summary>
    TippNichtErlaubt,

    /// <summary>503; Client zeigt „gerade nicht verfuegbar“.</summary>
    TippNichtVerfuegbar,

    /// <summary>400.</summary>
    ZugUnzulaessig,

    /// <summary>429.</summary>
    TageslimitErreicht,

    /// <summary>503, nur Gaeste.</summary>
    GeradeVielLos,

    /// <summary>400.</summary>
    EmailUngueltig,

    /// <summary>503.</summary>
    MailNichtGesendet,

    /// <summary>503.</summary>
    MailKontingentErschoepft,

    /// <summary>422.</summary>
    AnmeldungUngueltig,
}
