namespace Schach.Domain.Entitaeten;

// Domaenen-Enums der gespeicherten Spalten (SCHACH-SPEC 8.4a, 8.12). In Postgres als Text, darum
// sind die Namen Teil des Schemas: nicht umbenennen. Die Member gleichen den Wire-Enums in
// Schach.Api.Contracts, damit die Abbildung ueber den Namen geht. Farbe, Ergebnis und Grund
// liegen in Schach.Domain (Regeln/), eine Menge fuer Regeln und Entitaeten.

/// <summary>Bedenkzeit einer Partie (SCHACH-SPEC 3.2).</summary>
public enum Bedenkzeit
{
    OhneUhr,
    Fischer15Plus10,
    Fischer10Plus5,
    Fischer5Plus3,
    JeZug90s,
}

/// <summary>Zustand einer Partie (SCHACH-SPEC 8.2).</summary>
public enum PartieStatus
{
    Offen,
    Beendet,
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
