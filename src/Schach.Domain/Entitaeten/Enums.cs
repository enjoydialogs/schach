namespace Schach.Domain.Entitaeten;

// Domaenen-Enums der gespeicherten Spalten (SCHACH-SPEC 8.4a, 8.12). In Postgres als Text, darum
// sind die Namen Teil des Schemas: nicht umbenennen. Die Member gleichen den Wire-Enums in
// Schach.Api.Contracts, damit die Abbildung ueber den Namen geht.

/// <summary>Bedenkzeit einer Partie (SCHACH-SPEC 3.2).</summary>
public enum Bedenkzeit
{
    OhneUhr,
    Fischer15Plus10,
    Fischer10Plus5,
    Fischer5Plus3,
    JeZug90s,
}

/// <summary>Seite auf dem Brett: <c>Partie.SpielerFarbe</c> und <c>Zug.Seite</c>.</summary>
public enum Farbe
{
    Weiss,
    Schwarz,
}

/// <summary>Zustand einer Partie (SCHACH-SPEC 8.2).</summary>
public enum PartieStatus
{
    Offen,
    Beendet,
}

/// <summary>Ergebnis einer beendeten Partie (SCHACH-SPEC 3.6).</summary>
public enum Ergebnis
{
    WeissGewinnt,
    SchwarzGewinnt,
    Remis,
}

/// <summary>Grund des Partieendes (SCHACH-SPEC 3.6).</summary>
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

/// <summary>Anlass eines Spruchs (SCHACH-SPEC 6.1).</summary>
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

/// <summary>Herkunft eines Spruchs (SCHACH-SPEC 6.7, 8.12).</summary>
public enum SpruchQuelle
{
    Mistral,
    Ersatz,
}

/// <summary>Engine, die einen Gegnerzug berechnet hat (SCHACH-SPEC 8.10). Leer bei Spielerzuegen.</summary>
public enum ZugEngine
{
    Maia,
    Fairy,
}
