namespace Schach.Api.Contracts;

// Partie-Vertrag (SCHACH-SPEC 8.3, 8.4). Eingefroren ab Phase 0. Felder in camelCase (ApiJson),
// Zeitpunkte ISO-8601 in UTC mit `Z`.

/// <summary>Body von <c>POST /partien</c>.</summary>
/// <param name="GegnerId">Gegner-Key, z. B. <c>oscar</c>.</param>
public sealed record NeuePartieAnfrage(string GegnerId, FarbeWahl Farbe, Bedenkzeit Bedenkzeit);

/// <summary>Body von <c>POST /partien/{id}/zuege</c>.</summary>
/// <param name="Halbzug">Optimistisches Token: der <c>halbzug</c> des Zustands, auf dem der Zug beruht.</param>
/// <param name="Zug">UCI inklusive Umwandlung, z. B. <c>e7e8q</c>.</param>
public sealed record ZugAnfrage(int Halbzug, string Zug);

/// <summary>Body von <c>zuruecknehmen</c>, <c>aufgeben</c>, <c>remis-angebot</c> und <c>tipp</c>.</summary>
public sealed record HalbzugAnfrage(int Halbzug);

/// <summary>
/// Der vollstaendige Zustand einer Partie (SCHACH-SPEC 8.4). Immer vollstaendig; der Client
/// uebernimmt ihn ohne Zusammenfuehren.
/// </summary>
/// <param name="Id">128-Bit-Zufalls-Id; fuer Gaeste zugleich der Zugriffsschluessel.</param>
/// <param name="Halbzug">Anzahl gespielter Halbzuege, zugleich das Konflikttoken.</param>
/// <param name="ErlaubteZuege">UCI-Liste, nur wenn der Spieler am Zug ist, sonst leer.</param>
/// <param name="GegnerZug">Die Gegnerantwort dieser Anfrage, sonst <c>null</c>.</param>
/// <param name="Uhr"><c>null</c> bei <see cref="Contracts.Bedenkzeit.OhneUhr"/>.</param>
/// <param name="RemisAngebotMoeglichAbZug">Zugnummer (FEN-Feld 6), ab der ein Remis-Angebot erlaubt ist.</param>
/// <param name="Pgn">Nur bei <see cref="PartieStatus.Beendet"/>.</param>
public sealed record ZustandDto(
    Guid Id,
    PartieStatus Status,
    Farbe SpielerFarbe,
    GegnerDto Gegner,
    Bedenkzeit Bedenkzeit,
    int Halbzug,
    string Fen,
    IReadOnlyList<ZugDto> Zugfolge,
    IReadOnlyList<string> ErlaubteZuege,
    bool AmZug,
    bool GegnerZugAusstehend,
    GegnerZugDto? GegnerZug,
    UhrDto? Uhr,
    bool ZaehltFuerElo,
    int RemisAngebotMoeglichAbZug,
    IReadOnlyList<SpruchDto> Sprueche,
    Ergebnis? Ergebnis,
    Grund? Grund,
    int? EloVorher,
    int? EloNachher,
    string? Pgn);

/// <summary>Ein Eintrag der Zugfolge.</summary>
/// <param name="Halbzug">1-basiert: der erste Zug von Weiss ist Halbzug 1.</param>
public sealed record ZugDto(int Halbzug, Farbe Seite, string Uci, string San, DateTimeOffset GespieltAm);

/// <summary>Die Gegnerantwort. Der Client zeigt sie erst ab <see cref="SichtbarAb"/> (SCHACH-SPEC 8.6).</summary>
public sealed record GegnerZugDto(
    int Halbzug,
    Farbe Seite,
    string Uci,
    string San,
    DateTimeOffset GespieltAm,
    DateTimeOffset SichtbarAb,
    SpruchDto? Spruch);

/// <summary>Uhrstand zum Zeitpunkt <see cref="ServerZeit"/> (SCHACH-SPEC 8.6).</summary>
/// <param name="LaeuftFuer"><c>null</c>, wenn keine Uhr laeuft (Partie beendet).</param>
public sealed record UhrDto(
    long SpielerRestMs,
    long GegnerRestMs,
    UhrSeite? LaeuftFuer,
    DateTimeOffset? AmZugSeit,
    DateTimeOffset ServerZeit);

/// <summary>Ein Spruch an seinem Halbzug (SCHACH-SPEC 6.7).</summary>
/// <param name="Stimme">Signiertes Token fuer <c>GET /sprachausgabe/{token}</c> (SCHACH-SPEC 6.9);
/// <c>null</c>, wenn die Sprachausgabe aus ist oder der Gegner keine Stimme hat.</param>
public sealed record SpruchDto(int NachHalbzug, SpruchEreignis Ereignis, string Text, string? Stimme = null);

/// <summary>Ein Gegner, wie <c>GET /gegner</c> ihn liefert und wie er im Zustand steht.</summary>
/// <param name="Key">Stabile Id, z. B. <c>oscar</c>.</param>
/// <param name="Bilanz">Nur fuer Spieler in <c>GET /gegner</c>; im Zustand und fuer Gaeste <c>null</c>.</param>
public sealed record GegnerDto(
    string Key,
    string Name,
    int Elo,
    Stufe Stufe,
    Persoenlichkeit Persoenlichkeit,
    string Vorstellung,
    BilanzDto? Bilanz);

/// <summary>Bilanz eines Spielers gegen einen Gegner (SCHACH-SPEC 3.8).</summary>
/// <param name="BesiegtAm">Erster Sieg in einer Partie, die fuer die Elo zaehlt; sonst <c>null</c>.</param>
public sealed record BilanzDto(int Siege, int Remis, int Niederlagen, DateTimeOffset? BesiegtAm);

/// <summary>Antwort von <c>POST /partien/{id}/tipp</c> (SCHACH-SPEC 7.3, 8.3).</summary>
public sealed record TippAntwort(TippDto Tipp, ZustandDto Zustand);

/// <summary>Der beste Zug laut Stockfish.</summary>
/// <param name="Uci">z. B. <c>c3e5</c>.</param>
/// <param name="Von">Startfeld, z. B. <c>c3</c>.</param>
/// <param name="Nach">Zielfeld, z. B. <c>e5</c>.</param>
public sealed record TippDto(string Uci, string Von, string Nach, Figur Figur);

/// <summary>Antwort von <c>GET /partien?limit=50&amp;vorId=</c>, neueste zuerst.</summary>
/// <param name="Weitere">Wahr, wenn es aeltere Partien gibt; die naechste Seite fragt mit <c>vorId</c> = letzte Id.</param>
public sealed record PartienListeDto(IReadOnlyList<PartieZeileDto> Partien, bool Weitere);

/// <summary>Eine beendete Partie in der Partienliste.</summary>
public sealed record PartieZeileDto(
    Guid Id,
    string GegnerKey,
    Farbe SpielerFarbe,
    Bedenkzeit Bedenkzeit,
    Ergebnis Ergebnis,
    Grund Grund,
    DateTimeOffset BeendetAm,
    bool ZaehltFuerElo,
    int? EloVorher,
    int? EloNachher);
