namespace Schach.Api.Contracts;

// Konto und Sitzung (SCHACH-SPEC 9.3). Eingefroren ab Phase 0.

/// <summary>Body von <c>POST /auth/anmeldung</c>.</summary>
public sealed record AnmeldungAnfrage(string Email);

/// <summary>Immer 202 (keine Verzweigung nach Konto). <see cref="AnmeldungId"/> liegt im sessionStorage.</summary>
public sealed record AnmeldungAntwort(Guid AnmeldungId);

/// <summary>Body von <c>POST /auth/anmeldung/bestaetigen</c>: entweder <see cref="Token"/> (Link)
/// oder <see cref="AnmeldungId"/> plus <see cref="Code"/> (6 Ziffern).</summary>
public sealed record BestaetigenAnfrage(string? Token = null, Guid? AnmeldungId = null, string? Code = null);

/// <summary>200 plus Cookie <c>schach_sitzung</c>. <see cref="Neu"/>: Konto wurde gerade angelegt.</summary>
public sealed record BestaetigenAntwort(SpielerDto Spieler, bool Neu);

/// <summary>Der angemeldete Spieler.</summary>
public sealed record SpielerDto(Guid Id, string Email, int Elo);

/// <summary>Antwort von <c>GET /sitzung</c>; setzt das Antiforgery-Cookie.
/// <see cref="CsrfToken"/> geht als Header <c>X-CSRF-TOKEN</c> mit jedem Nicht-GET.</summary>
public sealed record SitzungAntwort(bool Angemeldet, SpielerDto? Spieler, string CsrfToken);

/// <summary>Body von <c>POST /auth/abmelden</c>. <see cref="Ueberall"/> erneuert den Sicherheitsstempel.</summary>
public sealed record AbmeldenAnfrage(bool? Ueberall = null);

/// <summary>Antwort von <c>GET /konto/export</c> (SCHACH-SPEC 9.7, 12.4).</summary>
public sealed record KontoExportDto(
    KontoExportKonto Konto,
    int Elo,
    IReadOnlyList<KontoExportBilanz> Bilanz,
    IReadOnlyList<KontoExportPartie> Partien);

public sealed record KontoExportKonto(Guid Id, string Email, DateTimeOffset ErstelltAm, DateTimeOffset? ZuletztAngemeldetAm);

public sealed record KontoExportBilanz(string GegnerKey, int Siege, int Remis, int Niederlagen, DateTimeOffset? BesiegtAm);

/// <param name="Pgn">Bei offener Partie <c>null</c>.</param>
public sealed record KontoExportPartie(
    Guid Id,
    string GegnerKey,
    Farbe SpielerFarbe,
    Bedenkzeit Bedenkzeit,
    PartieStatus Status,
    Ergebnis? Ergebnis,
    Grund? Grund,
    DateTimeOffset ErstelltAm,
    DateTimeOffset? BeendetAm,
    bool ZaehltFuerElo,
    int? EloVorher,
    int? EloNachher,
    string? Pgn,
    IReadOnlyList<SpruchDto> Sprueche);
