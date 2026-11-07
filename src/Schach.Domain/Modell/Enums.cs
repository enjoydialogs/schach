namespace Schach.Domain.Modell;

// Fach-Enums der Entitaeten (SCHACH-SPEC 8.4a). In Postgres als Text gespeichert: Die Membernamen
// SIND die gespeicherten Werte und gleichen den Wire-Enums in Schach.Api/Contracts/WireEnums.cs.
// Nicht umbenennen, ohne die Daten zu migrieren.

/// <summary><c>Partie.Bedenkzeit</c> (SCHACH-SPEC 3.2).</summary>
public enum Bedenkzeit
{
    OhneUhr,
    Fischer15Plus10,
    Fischer10Plus5,
    Fischer5Plus3,
    JeZug90s,
}

/// <summary><c>Partie.SpielerFarbe</c> und <c>Zug.Seite</c>.</summary>
public enum Farbe
{
    Weiss,
    Schwarz,
}

/// <summary><c>Partie.Status</c> (SCHACH-SPEC 8.2).</summary>
public enum PartieStatus
{
    Offen,
    Beendet,
}

/// <summary><c>Partie.Ergebnis</c> (SCHACH-SPEC 3.6).</summary>
public enum Ergebnis
{
    WeissGewinnt,
    SchwarzGewinnt,
    Remis,
}

/// <summary><c>Partie.Grund</c> (SCHACH-SPEC 3.6).</summary>
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

/// <summary><c>Spruch.Ereignis</c> (SCHACH-SPEC 6.1).</summary>
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

/// <summary><c>Spruch.Quelle</c> (SCHACH-SPEC 8.12).</summary>
public enum SpruchQuelle
{
    Mistral,
    Ersatz,
}

/// <summary><c>Zug.Engine</c>: welche Engine einen Gegnerzug gerechnet hat (SCHACH-SPEC 8.10).
/// Spiegelt <c>EngineArt</c> aus <c>Schach.Application.Abstractions</c>.</summary>
public enum ZugEngine
{
    Maia,
    Fairy,
}
