using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Schach.Application.Abstractions;
using Schach.Application.Limits;
using Schach.Application.Sprueche;
using Schach.Domain;
using Schach.Domain.Entitaeten;
using Schach.Domain.Stil;

namespace Schach.Application.Partien;

/// <summary>Alles, was der Zustand einer Partie braucht (SCHACH-SPEC 8.4). Bei Gast-Partien bleibt die
/// Sicht nach dem Loeschen der Zeilen gueltig: die letzte Antwort enthaelt alles fuer den Endbildschirm.</summary>
/// <param name="GegnerZug">Die Gegnerantwort dieser Anfrage, sonst <c>null</c>.</param>
public sealed record PartieSicht(Partie Partie, IReadOnlyList<Zug> Zugfolge, IReadOnlyList<Spruch> Sprueche, Zug? GegnerZug);

/// <summary>Fachliche Ablehnung eines Partie-Use-Cases (SCHACH-SPEC 8.5, 11.2).</summary>
public enum PartieFehler
{
    NichtGefunden,
    GegnerUnbekannt,
    ZugUnzulaessig,
    NichtAmZug,
    HalbzugVeraltet,
    PartieBeendet,
    UhrAbgelaufen,
    OffenePartie,
    ZuruecknehmenNichtErlaubt,
    TageslimitErreicht,
    GeradeVielLos,
    RemisAngebotGesperrt,
    TippNichtErlaubt,
    TippNichtVerfuegbar,
}

/// <summary>Ergebnis eines Partie-Use-Cases: eine <see cref="Sicht"/>, ein <see cref="Fehler"/> oder beides
/// (409 mit Zustand).</summary>
/// <param name="Tipp">Nur bei <c>POST /partien/{id}/tipp</c>.</param>
/// <param name="Remis">Nur bei <c>POST /partien/{id}/remis-angebot</c>: wie der Gegner geantwortet hat.</param>
public sealed record PartieErgebnis(
    PartieSicht? Sicht, PartieFehler? Fehler = null, Guid? OffenePartieId = null, Tippzug? Tipp = null, RemisAntwort? Remis = null)
{
    public static PartieErgebnis Abgelehnt(PartieFehler fehler, PartieSicht? sicht = null) => new(sicht, fehler);
}

/// <summary>Ein Gegner mit der Bilanz des Spielers; <c>null</c> fuer Gaeste.</summary>
public sealed record GegnerMitBilanz(Gegner Gegner, Bilanz? Bilanz);

/// <summary>Eine Seite der Partienliste (SCHACH-SPEC 8.3, 10.7), neueste zuerst.</summary>
/// <param name="Weitere">Es gibt aeltere beendete Partien.</param>
public sealed record PartienSeite(IReadOnlyList<Partie> Partien, bool Weitere);

/// <summary>
/// Partie-Kern ueber die Datenbank (SCHACH-SPEC 3.1 bis 3.3, 8.8, 8.10, 8.11). Zwei Transaktionen je Zug:
/// T1 Spielerzug, dann Engine ausserhalb jeder Transaktion, T2 Gegnerzug. Faellt die Engine aus, bleibt
/// <c>gegnerZugAusstehend</c> und der Sweep (S1d) holt den Zug ueber <see cref="GegnerZugNachholenAsync"/> nach.
/// Ein Concurrency-Konflikt (<c>Partie.Version</c>, Unique <c>Zug(PartieId, Halbzug)</c>) ergibt
/// <see cref="PartieFehler.HalbzugVeraltet"/> mit frischem Zustand.
/// </summary>
/// <param name="spielerId">In allen Methoden: angemeldeter Spieler oder <c>null</c> fuer Gaeste. Eine
/// Spieler-Partie sieht nur ihr Besitzer; eine Gast-Partie jeder, der die Id kennt.</param>
public sealed class PartieDienst(
    IAppDbContext db,
    IEngineClient engine,
    IPartieStartLimits limits,
    SpruchDienst sprueche,
    SpruchOptions spruchOptionen,
    TippCache tippCache,
    TippOptions tippOptionen,
    TimeProvider zeit)
{
    /// <summary>Hoechstzahl je Seite der Partienliste.</summary>
    public const int ListeMax = 50;

    /// <summary>Uhr der Spielerseite abgelaufen (SCHACH-SPEC 8.7). Die Gegnerseite laeuft nie ab: bei
    /// Engine-Ausfall wird ihr nur D belastet, und D ist auf 10 % ihrer Restzeit gedeckelt.</summary>
    public static bool UhrAbgelaufen(Partie partie, TimeProvider zeit)
    {
        ArgumentNullException.ThrowIfNull(partie);
        return partie.SpielerAmZug() && partie.Uhr() is { } uhr && uhr.IstAbgelaufen(zeit);
    }

    /// <summary><c>POST /partien</c>. Spielt der Gegner Weiss, enthaelt die Sicht seinen ersten Zug.
    /// Der <c>Partiebeginn</c>-Spruch (nur Ersatz, SCHACH-SPEC 6.1) kommt in derselben Transaktion.</summary>
    /// <param name="farbe"><c>null</c> heisst Zufall.</param>
    public async Task<PartieErgebnis> StartenAsync(
        Guid? spielerId, string gegnerId, Farbe? farbe, Bedenkzeit bedenkzeit, CancellationToken ct = default)
    {
        if (GegnerKatalog.Finde(gegnerId) is not { } gegner)
        {
            return PartieErgebnis.Abgelehnt(PartieFehler.GegnerUnbekannt);
        }

        if (spielerId is { } sid && await OffenePartieIdAsync(sid, ct) is { } offen)
        {
            return new PartieErgebnis(null, PartieFehler.OffenePartie, offen);
        }

        switch (await limits.PruefeAsync(spielerId, ct))
        {
            case PartieStartAblehnung.TageslimitErreicht:
                return PartieErgebnis.Abgelehnt(PartieFehler.TageslimitErreicht);
            case PartieStartAblehnung.GeradeVielLos:
                return PartieErgebnis.Abgelehnt(PartieFehler.GeradeVielLos);
        }

        var partie = Partiefuehrung.Starte(
            Guid.NewGuid(),
            spielerId,
            gegner,
            farbe ?? (RandomNumberGenerator.GetInt32(2) == 0 ? Farbe.Weiss : Farbe.Schwarz),
            bedenkzeit,
            BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8)),
            zeit);
        if (partie.GegnerZugAusstehend())
        {
            partie.LetzterVersuchAm = zeit.GetUtcNow();
        }

        var beginn = await sprueche.ErzeugeAsync(
            Spruchbau.Anfrage(partie, [], [], SpruchEreignis.Partiebeginn, 0, null, nurErsatz: true), ct);

        try
        {
            await db.RunInTransactionAsync(
                async t =>
                {
                    Leeren();
                    db.Set<Partie>().Add(partie);
                    db.Set<Spruch>().Add(Kopie(beginn));
                    await db.SaveChangesAsync(t);
                    return 0;
                },
                ct);
        }
        catch (DbUpdateException) when (spielerId is { } besitzer)
        {
            // Zwei Starts zugleich: der gefilterte Unique-Index laesst nur eine offene Partie zu.
            Leeren();
            return await OffenePartieIdAsync(besitzer, ct) is { } schonOffen
                ? new PartieErgebnis(null, PartieFehler.OffenePartie, schonOffen)
                : throw new InvalidOperationException("Partie konnte nicht angelegt werden.");
        }

        var sicht = new PartieSicht(partie, [], [beginn], null);
        return new PartieErgebnis(partie.GegnerZugAusstehend() ? await GegnerAntwortAsync(sicht, nachAusfall: false, ct) : sicht);
    }

    /// <summary><c>GET /partien/{id}</c>. Prueft zuerst den Zeitablauf (SCHACH-SPEC 8.7).</summary>
    public async Task<PartieErgebnis> LadenAsync(Guid id, Guid? spielerId, CancellationToken ct = default)
    {
        Leeren();
        return await SichtMitZeitablaufAsync(id, spielerId, ct) is { } sicht
            ? new PartieErgebnis(sicht)
            : PartieErgebnis.Abgelehnt(PartieFehler.NichtGefunden);
    }

    /// <summary><c>GET /partien/offen</c>: die offene Partie des Spielers oder <c>null</c>. Ist ihre Uhr
    /// abgelaufen, kommt sie beendet zurueck.</summary>
    public async Task<PartieSicht?> OffeneAsync(Guid spielerId, CancellationToken ct = default)
    {
        Leeren();
        return await OffenePartieIdAsync(spielerId, ct) is { } id ? await SichtMitZeitablaufAsync(id, spielerId, ct) : null;
    }

    /// <summary><c>GET /partien?limit&amp;vorId</c>: beendete Partien des Spielers, neueste zuerst (Keyset
    /// ueber <c>(BeendetAm, Id)</c>). <c>null</c>, wenn <paramref name="vorId"/> keine beendete Partie des
    /// Spielers ist.</summary>
    public async Task<PartienSeite?> ListeAsync(Guid spielerId, int limit, Guid? vorId, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, ListeMax);
        var basis = db.Set<Partie>().AsNoTracking()
            .Where(p => p.SpielerId == spielerId && p.Status == PartieStatus.Beendet && p.BeendetAm != null);
        var abfrage = basis;
        var gleichzeitig = 0;
        if (vorId is { } vor)
        {
            var grenze = await basis.Where(p => p.Id == vor).Select(p => p.BeendetAm).FirstOrDefaultAsync(ct);
            if (grenze is null)
            {
                return null;
            }

            // Gleiche BeendetAm stehen in der SQL-Ordnung zuerst; bis einschliesslich vorId ueberspringen.
            // So bleibt die Uuid-Ordnung ganz in Postgres.
            gleichzeitig = await basis.CountAsync(p => p.BeendetAm == grenze, ct);
            abfrage = basis.Where(p => p.BeendetAm <= grenze);
        }

        var zeilen = await abfrage
            .OrderByDescending(p => p.BeendetAm)
            .ThenByDescending(p => p.Id)
            .Take(limit + 1 + gleichzeitig)
            .ToListAsync(ct);
        if (vorId is { } v)
        {
            zeilen = [.. zeilen.Skip(zeilen.FindIndex(p => p.Id == v) + 1)];
        }

        return new PartienSeite([.. zeilen.Take(limit)], zeilen.Count > limit);
    }

    /// <summary><c>POST /partien/{id}/zuege</c> (SCHACH-SPEC 8.1, 8.8). Ein Zug nach Ablauf der Uhr ergibt
    /// <see cref="PartieFehler.UhrAbgelaufen"/>.</summary>
    public async Task<PartieErgebnis> ZugAsync(Guid id, Guid? spielerId, int halbzug, string uci, CancellationToken ct = default)
    {
        PartieErgebnis t1;
        var gespielt = false;
        try
        {
            t1 = await db.RunInTransactionAsync(
                async t =>
                {
                    var (ergebnis, neu) = await SpielerzugAsync(id, spielerId, halbzug, uci, t);
                    gespielt = neu;
                    return ergebnis;
                },
                ct);
        }
        catch (DbUpdateException)
        {
            return await VeraltetAsync(id, spielerId, ct);
        }

        if (t1.Fehler is not null || t1.Sicht is not { } sicht)
        {
            return t1;
        }

        if (gespielt && sicht.Partie is { Status: PartieStatus.Beendet, Ergebnis: { } erg, Grund: { } grund })
        {
            // Partieende durch den Spielerzug: die einzige Ausnahme von „ein Spruch je Gegnerzug“.
            return new PartieErgebnis(await SpruchNachtragenAsync(
                sicht, SpruchAuswahl.BeiEnde(erg, sicht.Partie.SpielerFarbe), sicht.Partie.Halbzuege, grund.ToString(), ct));
        }

        return new PartieErgebnis(sicht.Partie.GegnerZugAusstehend() && sicht.GegnerZug is null
            ? await GegnerAntwortAsync(sicht, nachAusfall: false, ct)
            : sicht);
    }

    /// <summary><c>POST /partien/{id}/zuruecknehmen</c> (SCHACH-SPEC 3.3). Der Gegner darf es kommentieren.</summary>
    public async Task<PartieErgebnis> ZuruecknehmenAsync(Guid id, Guid? spielerId, int halbzug, CancellationToken ct = default)
    {
        var schonZurueckgenommen = false;
        var ergebnis = await InTransaktionAsync(
            id,
            spielerId,
            async (sicht, t) =>
            {
                if (Vorab(sicht, halbzug) is { } fehler)
                {
                    return fehler;
                }

                int neu;
                try
                {
                    neu = sicht.Partie.NimmZurueck(sicht.Zugfolge);
                }
                catch (ZuruecknehmenNichtErlaubtException)
                {
                    return PartieErgebnis.Abgelehnt(PartieFehler.ZuruecknehmenNichtErlaubt, sicht);
                }

                schonZurueckgenommen = sicht.Sprueche.Any(s => s.Ereignis == SpruchEreignis.Zuruecknahme);
                sicht.Partie.LetzterVersuchAm = null;
                db.Set<Zug>().RemoveRange(sicht.Zugfolge.Where(z => z.Halbzug > neu));
                db.Set<Spruch>().RemoveRange(sicht.Sprueche.Where(s => s.NachHalbzug > neu));
                await db.SaveChangesAsync(t);
                return new PartieErgebnis(sicht with
                {
                    Zugfolge = [.. sicht.Zugfolge.Where(z => z.Halbzug <= neu)],
                    Sprueche = [.. sicht.Sprueche.Where(s => s.NachHalbzug <= neu)],
                });
            },
            ct);

        if (ergebnis is not { Fehler: null, Sicht: { } danach })
        {
            return ergebnis;
        }

        var ereignis = SpruchAuswahl.NachZuruecknahme(
            danach.Sprueche, danach.Zugfolge, danach.Partie.SpielerFarbe, schonZurueckgenommen, spruchOptionen);
        return ereignis is { } e
            ? new PartieErgebnis(await SpruchNachtragenAsync(danach, e, danach.Partie.Halbzuege, null, ct))
            : ergebnis;
    }

    /// <summary><c>POST /partien/{id}/aufgeben</c> (SCHACH-SPEC 3.5): der Gegner gewinnt, Grund Aufgabe.</summary>
    public async Task<PartieErgebnis> AufgebenAsync(Guid id, Guid? spielerId, int halbzug, CancellationToken ct = default)
    {
        var ergebnis = await InTransaktionAsync(
            id,
            spielerId,
            async (sicht, t) =>
            {
                if (Vorab(sicht, halbzug) is { } fehler)
                {
                    return fehler;
                }

                await BeendenAsync(sicht.Partie, Partieende.Sieg(Brettlage.Andere(sicht.Partie.SpielerFarbe), Grund.Aufgabe), t);
                return new PartieErgebnis(sicht);
            },
            ct);

        return ergebnis is { Fehler: null, Sicht: { } s }
            ? new PartieErgebnis(await SpruchNachtragenAsync(s, SpruchEreignis.Sieg, s.Partie.Halbzuege, nameof(Grund.Aufgabe), ct))
            : ergebnis;
    }

    /// <summary>
    /// <c>POST /partien/{id}/remis-angebot</c> (SCHACH-SPEC 3.5, 11.2). Sperren ohne Engine; ab Zug 20
    /// bewertet Stockfish (<c>/analyse</c>) aus Sicht des Gegners, bei Ausfall die Materialbilanz.
    /// Annahme beendet mit Grund Vereinbarung; Ablehnung speichert die Zugnummer und bringt einen
    /// <c>RemisAbgelehnt</c>-Spruch.
    /// </summary>
    public async Task<PartieErgebnis> RemisAngebotAsync(Guid id, Guid? spielerId, int halbzug, CancellationToken ct = default)
    {
        var vorab = await InTransaktionAsync(
            id,
            spielerId,
            async (sicht, t) =>
            {
                if (Vorab(sicht, halbzug) is { } fehler)
                {
                    return fehler;
                }

                var ohneEngine = Remisangebot.VorEngine(Brettlage.Zugnummer(sicht.Partie.Fen), sicht.Partie.LetztesRemisAngebotBeiZug);
                return ohneEngine is { } antwort ? await RemisAntwortenAsync(sicht, antwort, t) : new PartieErgebnis(sicht);
            },
            ct);
        if (vorab.Fehler is not null || vorab.Remis is not null || vorab.Sicht is not { } sicht)
        {
            return await RemisSpruchAsync(vorab, ct);
        }

        var partie = sicht.Partie;
        EngineAnalyseZeile? zeile;
        try
        {
            var analyse = await engine.AnalyseAsync(
                new EngineAnalyseRequest(partie.Fen, [], tippOptionen.MovetimeMs, 1), ct);
            zeile = analyse.Lines.Count > 0 ? analyse.Lines[0] : null;
        }
        catch (Exception e) when (e is EngineNichtVerfuegbarException or EngineFehlerException)
        {
            zeile = null;
        }

        var gegnerFarbe = Brettlage.Andere(partie.SpielerFarbe);
        var nachEngine = Remisangebot.NachEngine(zeile, partie.AmZug() == gegnerFarbe, Brettlage.Material(partie.Fen, gegnerFarbe));
        var ergebnis = await InTransaktionAsync(
            id,
            spielerId,
            async (s, t) =>
            {
                if (Vorab(s, halbzug) is { } fehler)
                {
                    return fehler;
                }

                // Ein paralleles Angebot kann inzwischen abgelehnt worden sein.
                var gesperrt = Remisangebot.VorEngine(Brettlage.Zugnummer(s.Partie.Fen), s.Partie.LetztesRemisAngebotBeiZug);
                return await RemisAntwortenAsync(s, gesperrt ?? nachEngine, t);
            },
            ct);
        return await RemisSpruchAsync(ergebnis, ct);
    }

    /// <summary>
    /// <c>POST /partien/{id}/tipp</c> (SCHACH-SPEC 7.3, 8.3): der beste Zug laut Stockfish, je FEN
    /// gecacht. Der erste Tipp setzt <c>ZaehltFuerElo = false</c>. Nur ohne Bedenkzeit und am Zug.
    /// </summary>
    public async Task<PartieErgebnis> TippAsync(Guid id, Guid? spielerId, int halbzug, CancellationToken ct = default)
    {
        var vorab = await InTransaktionAsync(id, spielerId, (sicht, _) => Task.FromResult(TippVorab(sicht, halbzug) ?? new PartieErgebnis(sicht)), ct);
        if (vorab.Fehler is not null || vorab.Sicht is not { } sicht)
        {
            return vorab;
        }

        var fen = sicht.Partie.Fen;
        if (!tippCache.Versuche(fen, out var uci))
        {
            try
            {
                uci = (await engine.AnalyseAsync(new EngineAnalyseRequest(fen, [], tippOptionen.MovetimeMs, 1), ct)).Bestmove;
            }
            catch (Exception e) when (e is EngineNichtVerfuegbarException or EngineFehlerException)
            {
                return PartieErgebnis.Abgelehnt(PartieFehler.TippNichtVerfuegbar, sicht);
            }

            if (uci is null || !Stellung.AusFen(fen).ErlaubteZuege().Contains(uci, StringComparer.Ordinal))
            {
                return PartieErgebnis.Abgelehnt(PartieFehler.TippNichtVerfuegbar, sicht);
            }

            tippCache.Merke(fen, uci);
        }

        var tipp = Tippzug.Aus(fen, uci);
        if (!sicht.Partie.ZaehltFuerElo)
        {
            return new PartieErgebnis(sicht, Tipp: tipp);
        }

        var ergebnis = await InTransaktionAsync(
            id,
            spielerId,
            async (s, t) =>
            {
                if (TippVorab(s, halbzug) is { } fehler)
                {
                    return fehler;
                }

                // Wie eine Zuruecknahme: die Partie zaehlt nicht mehr fuer die Elo (SCHACH-SPEC 3.7).
                s.Partie.ZaehltFuerElo = false;
                s.Partie.Version++;
                await db.SaveChangesAsync(t);
                return new PartieErgebnis(s);
            },
            ct);
        return ergebnis.Fehler is null ? ergebnis with { Tipp = tipp } : ergebnis;
    }

    /// <summary>Sweep-Block A und Lazy-Pfad: beendet die Partie, wenn die Uhr des Spielers abgelaufen ist
    /// (Grund Zeit oder ZeitOhneMattmaterial, Ersatzspruch). <c>null</c>, wenn es sie nicht gibt.</summary>
    public async Task<PartieSicht?> ZeitablaufAsync(Guid id, Guid? spielerId, CancellationToken ct = default)
    {
        try
        {
            return await db.RunInTransactionAsync(
                async t =>
                {
                    Leeren();
                    return await SichtAsync(id, spielerId, t) is { } sicht
                        ? await ZeitablaufImTxAsync(sicht, t) ?? sicht
                        : null;
                },
                ct);
        }
        catch (DbUpdateException)
        {
            // Parallel gezogen oder beendet: der frische Stand gilt.
            Leeren();
            return await SichtAsync(id, spielerId, ct);
        }
    }

    /// <summary>Holt einen ausstehenden Gegnerzug nach (Sweep-Block B). Belastet wird dem Gegner nur
    /// seine Denkzeit D (SCHACH-SPEC 8.6). Ohne ausstehenden Zug liefert er nur die Sicht.</summary>
    public async Task<PartieSicht?> GegnerZugNachholenAsync(Guid id, CancellationToken ct = default)
    {
        Leeren();
        var partie = await db.Set<Partie>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (partie is null || await SichtAsync(id, partie.SpielerId, ct) is not { } sicht)
        {
            return null;
        }

        if (!sicht.Partie.GegnerZugAusstehend())
        {
            await db.Set<Partie>().Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.LetzterVersuchAm, (DateTimeOffset?)null), ct);
            return sicht;
        }

        return await GegnerAntwortAsync(sicht, nachAusfall: true, ct);
    }

    /// <summary><c>GET /gegner</c>: alle Gegner Elo aufsteigend, mit Bilanz nur fuer Spieler.</summary>
    public async Task<IReadOnlyList<GegnerMitBilanz>> GegnerAsync(Guid? spielerId, CancellationToken ct = default)
    {
        if (spielerId is not { } sid)
        {
            return [.. GegnerKatalog.Alle.Select(g => new GegnerMitBilanz(g, null))];
        }

        var bilanzen = await db.Set<Bilanz>().AsNoTracking().Where(b => b.SpielerId == sid).ToListAsync(ct);
        return
        [
            .. GegnerKatalog.Alle.Select(g => new GegnerMitBilanz(
                g,
                bilanzen.FirstOrDefault(b => b.GegnerId == g.Key) ?? new Bilanz { SpielerId = sid, GegnerId = g.Key })),
        ];
    }

    /// <returns>Das Ergebnis und ob ein neuer Spielerzug gespeichert wurde.</returns>
    private async Task<(PartieErgebnis Ergebnis, bool Neu)> SpielerzugAsync(Guid id, Guid? spielerId, int halbzug, string uci, CancellationToken t)
    {
        Leeren();
        if (await SichtAsync(id, spielerId, t) is not { } sicht)
        {
            return (PartieErgebnis.Abgelehnt(PartieFehler.NichtGefunden), false);
        }

        if (await ZeitablaufImTxAsync(sicht, t) is { } abgelaufen)
        {
            // Lazy (SCHACH-SPEC 8.7): wer ueberzieht, verliert; der Zug kommt zu spaet.
            return (PartieErgebnis.Abgelehnt(PartieFehler.UhrAbgelaufen, abgelaufen), false);
        }

        var partie = sicht.Partie;
        switch (partie.OrdneEin(sicht.Zugfolge, halbzug, uci))
        {
            case Zugeinordnung.Wiederholung:
                // Gleicher Zug an gespielter Position: aktueller Zustand samt schon gespielter Antwort.
                return (new PartieErgebnis(sicht with { GegnerZug = sicht.Zugfolge.FirstOrDefault(z => z.Halbzug == halbzug + 2) }), false);
            case Zugeinordnung.PartieBeendet:
                // Schon vom Sweep oder einem GET wegen Zeit beendet: fuer den Zug ist es dasselbe.
                return (PartieErgebnis.Abgelehnt(
                    partie.Grund is Grund.Zeit or Grund.ZeitOhneMattmaterial && halbzug == partie.Halbzuege
                        ? PartieFehler.UhrAbgelaufen
                        : PartieFehler.PartieBeendet,
                    sicht), false);
            case Zugeinordnung.HalbzugVeraltet:
                return (PartieErgebnis.Abgelehnt(PartieFehler.HalbzugVeraltet, sicht), false);
            case Zugeinordnung.NichtAmZug:
                return (PartieErgebnis.Abgelehnt(PartieFehler.NichtAmZug, sicht), false);
        }

        Halbzugergebnis ergebnis;
        try
        {
            ergebnis = partie.SpieleSpielerzug(sicht.Zugfolge, uci, zeit);
        }
        catch (ZugUnzulaessigException)
        {
            return (PartieErgebnis.Abgelehnt(PartieFehler.ZugUnzulaessig), false);
        }
        catch (UhrAbgelaufenException)
        {
            var stellung = Partiefuehrung.Verlauf(sicht.Zugfolge)[^1];
            return (PartieErgebnis.Abgelehnt(
                PartieFehler.UhrAbgelaufen,
                await EndeMitErsatzAsync(sicht, Remisregeln.Zeitueberschreitung(stellung, partie.SpielerFarbe), t)), false);
        }

        var zug = ergebnis.Zug!;
        db.Set<Zug>().Add(zug);
        sicht = sicht with { Zugfolge = [.. sicht.Zugfolge, zug] };
        if (ergebnis.Ende is { } ende)
        {
            await BeendenAsync(partie, ende, t);
        }
        else
        {
            partie.LetzterVersuchAm = zeit.GetUtcNow();
            await db.SaveChangesAsync(t);
        }

        return (new PartieErgebnis(sicht), true);
    }

    /// <summary>Was T2 bewirkt hat.</summary>
    private enum Gegnerantwort
    {
        Nichts,
        Zug,
        ZugMitEnde,
        Zeitende,
    }

    /// <summary>Engine ausserhalb jeder Transaktion, dann T2, dann der Spruch (SpruchDienst wartet bis
    /// 2,5 s und gehoert in keine Transaktion). Faellt die Engine aus, bleibt der Zug ausstehend.</summary>
    /// <param name="nachAusfall">Sweep-Block B: dem Gegner nur D belasten (SCHACH-SPEC 8.6).</param>
    private async Task<PartieSicht> GegnerAntwortAsync(PartieSicht vorher, bool nachAusfall, CancellationToken ct)
    {
        var partie = vorher.Partie;
        var gegner = GegnerKatalog.Finde(partie.GegnerId)
            ?? throw new InvalidOperationException($"Unbekannter Gegner '{partie.GegnerId}'.");
        var eichung = Eichung.Identitaet(gegner);
        var spielerElo = partie.SpielerId is { } sid
            ? await db.Set<Spieler>().AsNoTracking().Where(s => s.Id == sid).Select(s => (int?)s.Elo).FirstOrDefaultAsync(ct) ?? Elo.Start
            : Elo.Start;
        var verlauf = Partiefuehrung.Verlauf(vorher.Zugfolge);
        var halbzug = partie.Halbzuege;

        Gegnerwahl? wahl;
        try
        {
            var antwort = await engine.MoveAsync(
                new EngineMoveRequest(
                    SchachDomain.GrundstellungFen,
                    [.. vorher.Zugfolge.OrderBy(z => z.Halbzug).Select(z => z.Uci)],
                    eichung.SelfElo,
                    spielerElo,
                    eichung.Engine == ZugEngine.Fairy ? EngineArt.Fairy : EngineArt.Maia,
                    eichung.Engine == ZugEngine.Fairy ? eichung.SelfElo : null),
                ct);
            wahl = Waehle(antwort, verlauf, gegner, eichung, partie.Seed, halbzug + 1);
        }
        catch (Exception e) when (e is EngineNichtVerfuegbarException or EngineFehlerException)
        {
            wahl = null;
        }

        if (wahl is null)
        {
            // Der Sweep versucht es erst wieder, wenn dieser Versuch 10 s alt ist.
            var jetzt = zeit.GetUtcNow();
            await db.Set<Partie>()
                .Where(p => p.Id == partie.Id && p.Halbzuege == halbzug && p.Status == PartieStatus.Offen)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.LetzterVersuchAm, (DateTimeOffset?)jetzt), ct);
            return vorher with { GegnerZug = null };
        }

        var art = Gegnerantwort.Nichts;
        PartieSicht sicht;
        try
        {
            sicht = await db.RunInTransactionAsync(
                async t =>
                {
                    art = Gegnerantwort.Nichts;
                    Leeren();
                    var s = await SichtAsync(partie.Id, partie.SpielerId, t);
                    if (s is null || s.Partie.Halbzuege != halbzug || !s.Partie.GegnerZugAusstehend())
                    {
                        // Inzwischen geaendert (Zuruecknehmen, Aufgeben, Sweep): nichts mehr zu tun.
                        return s ?? vorher with { GegnerZug = null };
                    }

                    if (nachAusfall)
                    {
                        // Die Ausfallzeit zaehlt nicht: gespieltAm = jetzt + D, belastet wird nur D.
                        var jetzt = zeit.GetUtcNow();
                        s.Partie.LetzterZugAm = jetzt;
                        if (s.Partie.Uhr() is not null)
                        {
                            s.Partie.AmZugSeit = jetzt;
                        }
                    }

                    Halbzugergebnis ergebnis;
                    try
                    {
                        ergebnis = s.Partie.SpieleGegnerzug(s.Zugfolge, wahl, zeit);
                    }
                    catch (ZugUnzulaessigException)
                    {
                        return s;
                    }

                    if (ergebnis.Zug is not { } zug)
                    {
                        art = Gegnerantwort.Zeitende;
                        return await EndeMitErsatzAsync(s, ergebnis.Ende!, t);
                    }

                    db.Set<Zug>().Add(zug);
                    s = s with { Zugfolge = [.. s.Zugfolge, zug], GegnerZug = zug };
                    s.Partie.LetzterVersuchAm = null;
                    if (ergebnis.Ende is { } ende)
                    {
                        art = Gegnerantwort.ZugMitEnde;
                        await BeendenAsync(s.Partie, ende, t);
                    }
                    else
                    {
                        art = Gegnerantwort.Zug;
                        await db.SaveChangesAsync(t);
                    }

                    return s;
                },
                ct);
        }
        catch (DbUpdateException)
        {
            Leeren();
            return await SichtAsync(partie.Id, partie.SpielerId, ct) ?? vorher with { GegnerZug = null };
        }

        switch (art)
        {
            case Gegnerantwort.ZugMitEnde when sicht.Partie is { Ergebnis: { } erg, Grund: { } grund }:
                return await SpruchNachtragenAsync(sicht, SpruchAuswahl.BeiEnde(erg, sicht.Partie.SpielerFarbe), sicht.Partie.Halbzuege, grund.ToString(), ct);
            case Gegnerantwort.Zug:
                var farbe = sicht.Partie.SpielerFarbe;
                var ereignis = SpruchAuswahl.NachGegnerzug(
                    Spruchbau.Lage(sicht.Zugfolge, farbe),
                    SpruchAuswahl.GegnerzuegeSeitSpruch(sicht.Zugfolge, sicht.Sprueche, farbe),
                    SpruchAuswahl.NichtPflicht(sicht.Sprueche),
                    spruchOptionen);
                return ereignis is { } e ? await SpruchNachtragenAsync(sicht, e, sicht.Partie.Halbzuege, null, ct) : sicht;
            default:
                return sicht;
        }
    }

    /// <summary>Maia: Stilwahl ueber die Policy der erlaubten Zuege. Fairy: <c>bestmove</c>.
    /// <c>null</c>, wenn die Antwort keinen erlaubten Zug enthaelt.</summary>
    private static Gegnerwahl? Waehle(EngineMoveResponse antwort, List<Stellung> verlauf, Gegner gegner, Eichung eichung, long seed, int halbzug)
    {
        var erlaubt = verlauf[^1].ErlaubteZuege().ToHashSet(StringComparer.Ordinal);
        if (antwort.Engine == EngineArt.Fairy)
        {
            return antwort.Bestmove is { } best && erlaubt.Contains(best)
                ? new Gegnerwahl(best, ZugEngine.Fairy, null, null, null, null)
                : null;
        }

        var policy = (antwort.Policy ?? []).Where(e => erlaubt.Contains(e.Uci)).Select(e => new Policyzug(e.Uci, e.P)).ToList();
        if (policy.Count == 0)
        {
            return null;
        }

        var stil = Partiefuehrung.WaehleGegnerzug(verlauf, policy, Stilparameter.Fuer(gegner, eichung), seed, halbzug);
        var wdl = antwort.Wdl is { } w ? new Wdl { W = w.W, D = w.D, L = w.L } : null;
        return new Gegnerwahl(stil.Uci, ZugEngine.Maia, stil.MaiaRang, stil.PAnteil, stil.W, wdl);
    }

    /// <summary>Gemeinsame Pruefung fuer Aktionen mit <c>{halbzug}</c>.</summary>
    private static PartieErgebnis? Vorab(PartieSicht sicht, int halbzug)
    {
        if (sicht.Partie.Status == PartieStatus.Beendet)
        {
            return PartieErgebnis.Abgelehnt(PartieFehler.PartieBeendet, sicht);
        }

        return halbzug != sicht.Partie.Halbzuege ? PartieErgebnis.Abgelehnt(PartieFehler.HalbzugVeraltet, sicht) : null;
    }

    /// <summary>Tipp nur ohne Bedenkzeit und am Zug (SCHACH-SPEC 3.2, 7.3, 8.3).</summary>
    private static PartieErgebnis? TippVorab(PartieSicht sicht, int halbzug)
    {
        if (Vorab(sicht, halbzug) is { } fehler)
        {
            return fehler;
        }

        if (sicht.Partie.Bedenkzeit != Bedenkzeit.OhneUhr)
        {
            return PartieErgebnis.Abgelehnt(PartieFehler.TippNichtErlaubt, sicht);
        }

        return sicht.Partie.SpielerAmZug() ? null : PartieErgebnis.Abgelehnt(PartieFehler.NichtAmZug, sicht);
    }

    /// <summary>Annahme beendet, Ablehnung speichert die Zugnummer (Sperre fuer 5 Zuege).</summary>
    private async Task<PartieErgebnis> RemisAntwortenAsync(PartieSicht sicht, RemisAntwort antwort, CancellationToken t)
    {
        switch (antwort)
        {
            case RemisAntwort.Gesperrt:
                return PartieErgebnis.Abgelehnt(PartieFehler.RemisAngebotGesperrt, sicht);
            case RemisAntwort.Angenommen:
                await BeendenAsync(sicht.Partie, Partieende.Remis(Grund.Vereinbarung), t);
                break;
            default:
                sicht.Partie.LetztesRemisAngebotBeiZug = Brettlage.Zugnummer(sicht.Partie.Fen);
                sicht.Partie.Version++;
                await db.SaveChangesAsync(t);
                break;
        }

        return new PartieErgebnis(sicht, Remis: antwort);
    }

    private async Task<PartieErgebnis> RemisSpruchAsync(PartieErgebnis ergebnis, CancellationToken ct)
    {
        if (ergebnis is not { Fehler: null, Sicht: { } sicht, Remis: { } antwort })
        {
            return ergebnis;
        }

        var (ereignis, grund) = antwort switch
        {
            RemisAntwort.Angenommen => (SpruchEreignis.Remis, nameof(Grund.Vereinbarung)),
            RemisAntwort.AbgelehntVorZug20 => (SpruchEreignis.RemisAbgelehnt, Remisangebot.GrundVorZug20),
            _ => (SpruchEreignis.RemisAbgelehnt, Remisangebot.GrundKlarVorn),
        };
        return ergebnis with { Sicht = await SpruchNachtragenAsync(sicht, ereignis, sicht.Partie.Halbzuege, grund, ct) };
    }

    /// <summary>Laedt die Partie in einer Transaktion, prueft zuerst den Zeitablauf (SCHACH-SPEC 8.7) und
    /// fuehrt dann <paramref name="arbeit"/> aus. Concurrency-Konflikt: <see cref="PartieFehler.HalbzugVeraltet"/>.</summary>
    private async Task<PartieErgebnis> InTransaktionAsync(
        Guid id, Guid? spielerId, Func<PartieSicht, CancellationToken, Task<PartieErgebnis>> arbeit, CancellationToken ct)
    {
        try
        {
            return await db.RunInTransactionAsync(
                async t =>
                {
                    Leeren();
                    if (await SichtAsync(id, spielerId, t) is not { } sicht)
                    {
                        return PartieErgebnis.Abgelehnt(PartieFehler.NichtGefunden);
                    }

                    return await arbeit(await ZeitablaufImTxAsync(sicht, t) ?? sicht, t);
                },
                ct);
        }
        catch (DbUpdateException)
        {
            return await VeraltetAsync(id, spielerId, ct);
        }
    }

    /// <summary>Laden mit Lazy-Zeitablauf: nur wenn die Uhr abgelaufen ist, kostet es eine Transaktion.</summary>
    private async Task<PartieSicht?> SichtMitZeitablaufAsync(Guid id, Guid? spielerId, CancellationToken ct)
    {
        var sicht = await SichtAsync(id, spielerId, ct);
        return sicht is not null && UhrAbgelaufen(sicht.Partie, zeit) ? await ZeitablaufAsync(id, spielerId, ct) : sicht;
    }

    /// <summary>In der laufenden Transaktion: ist die Uhr abgelaufen, endet die Partie mit Zeit und
    /// Ersatzspruch. <c>null</c>, wenn nicht.</summary>
    private async Task<PartieSicht?> ZeitablaufImTxAsync(PartieSicht sicht, CancellationToken t)
    {
        var partie = sicht.Partie;
        if (partie.Status != PartieStatus.Offen || !UhrAbgelaufen(partie, zeit))
        {
            return null;
        }

        return await EndeMitErsatzAsync(sicht, Remisregeln.Zeitueberschreitung(Stellung.AusFen(partie.Fen), partie.SpielerFarbe), t);
    }

    /// <summary>Beendet in der laufenden Transaktion mit Ersatzspruch (Ende ohne Request-Bezug,
    /// SCHACH-SPEC 6.1). Der Ersatzspruch braucht kein Netz und darf darum in die Transaktion.</summary>
    private async Task<PartieSicht> EndeMitErsatzAsync(PartieSicht sicht, Partieende ende, CancellationToken t)
    {
        var p = sicht.Partie;
        var spruch = await sprueche.ErzeugeAsync(
            Spruchbau.Anfrage(p, sicht.Zugfolge, sicht.Sprueche, SpruchAuswahl.BeiEnde(ende.Ergebnis, p.SpielerFarbe), p.Halbzuege, ende.Grund.ToString(), nurErsatz: true),
            t);
        db.Set<Spruch>().Add(spruch);
        await BeendenAsync(p, ende, t);
        return sicht with { Sprueche = [.. sicht.Sprueche, spruch] };
    }

    /// <summary>
    /// Erzeugt einen Spruch ausserhalb jeder Transaktion (Mistral bis 2,5 s, sonst Ersatz) und speichert
    /// ihn danach. Gast-Partien sind nach dem Ende schon geloescht: dann steht er nur in der Antwort.
    /// </summary>
    private async Task<PartieSicht> SpruchNachtragenAsync(PartieSicht sicht, SpruchEreignis ereignis, int nachHalbzug, string? grund, CancellationToken ct)
    {
        var p = sicht.Partie;
        var vorher = p.MistralAufrufe;
        var spruch = await sprueche.ErzeugeAsync(Spruchbau.Anfrage(p, sicht.Zugfolge, sicht.Sprueche, ereignis, nachHalbzug, grund, nurErsatz: false), ct);
        var aufrufe = p.MistralAufrufe - vorher;
        var neu = sicht with { Sprueche = [.. sicht.Sprueche, spruch] };
        if (p.SpielerId is null && p.Status == PartieStatus.Beendet)
        {
            return neu;
        }

        try
        {
            await db.RunInTransactionAsync(
                async t =>
                {
                    Leeren();
                    if (!await db.Set<Partie>().AnyAsync(x => x.Id == p.Id, t))
                    {
                        return 0;
                    }

                    db.Set<Spruch>().Add(Kopie(spruch));
                    await db.SaveChangesAsync(t);
                    if (aufrufe > 0)
                    {
                        // Monoton, ohne Version: kein Konflikt mit dem naechsten Zug.
                        await db.Set<Partie>().Where(x => x.Id == p.Id)
                            .ExecuteUpdateAsync(s => s.SetProperty(x => x.MistralAufrufe, x => x.MistralAufrufe + aufrufe), t);
                    }

                    return 0;
                },
                ct);
        }
        catch (DbUpdateException)
        {
            // Die Partie ist inzwischen weg (Gast, Sweep C): der Spruch steht nur in der Antwort.
        }

        return neu;
    }

    private static Spruch Kopie(Spruch s) => new()
    {
        PartieId = s.PartieId,
        NachHalbzug = s.NachHalbzug,
        Ereignis = s.Ereignis,
        Text = s.Text,
        Quelle = s.Quelle,
    };

    /// <summary>Beenden in der laufenden Transaktion (SCHACH-SPEC 8.11). Gast-Partien werden geloescht;
    /// die Sicht im Speicher bleibt fuer die Antwort.</summary>
    private async Task BeendenAsync(Partie partie, Partieende ende, CancellationToken t)
    {
        Spieler? spieler = null;
        Bilanz? bilanz = null;
        if (partie.SpielerId is { } sid)
        {
            spieler = await db.Set<Spieler>().FirstAsync(s => s.Id == sid, t);
            bilanz = await db.Set<Bilanz>().FirstOrDefaultAsync(b => b.SpielerId == sid && b.GegnerId == partie.GegnerId, t);
            if (bilanz is null)
            {
                bilanz = new Bilanz { SpielerId = sid, GegnerId = partie.GegnerId };
                db.Set<Bilanz>().Add(bilanz);
            }
        }

        var folge = partie.Beenden(ende.Ergebnis, ende.Grund, zeit, spieler, bilanz);
        partie.Version++;
        partie.LetzterVersuchAm = null;
        await db.SaveChangesAsync(t);
        if (folge.Loeschen)
        {
            // Zug und Spruch folgen per Kaskade.
            await db.Set<Partie>().Where(p => p.Id == partie.Id).ExecuteDeleteAsync(t);
        }
    }

    private async Task<PartieSicht?> SichtAsync(Guid id, Guid? spielerId, CancellationToken ct)
    {
        var partie = await db.Set<Partie>().FirstOrDefaultAsync(p => p.Id == id && (p.SpielerId == null || p.SpielerId == spielerId), ct);
        if (partie is null)
        {
            return null;
        }

        // Nur bis zum geladenen Stand: unter READ COMMITTED kann ein paralleler Zug zwischen den Abfragen
        // committen. Ohne Filter passten Partie und Zugfolge nicht zusammen (gleichzeitige Zuege ergaben
        // dann 400 ZugUnzulaessig statt 409); so scheitert der zweite Zug am Concurrency-Token.
        var halbzuege = partie.Halbzuege;
        var zuege = await db.Set<Zug>().Where(z => z.PartieId == id && z.Halbzug <= halbzuege).OrderBy(z => z.Halbzug).ToListAsync(ct);
        var sprueche = await db.Set<Spruch>().Where(s => s.PartieId == id && s.NachHalbzug <= halbzuege)
            .OrderBy(s => s.NachHalbzug).ThenBy(s => s.Id).ToListAsync(ct);
        return new PartieSicht(partie, zuege, sprueche, null);
    }

    private async Task<PartieErgebnis> VeraltetAsync(Guid id, Guid? spielerId, CancellationToken ct)
    {
        Leeren();
        return await SichtAsync(id, spielerId, ct) is { } sicht
            ? PartieErgebnis.Abgelehnt(PartieFehler.HalbzugVeraltet, sicht)
            : PartieErgebnis.Abgelehnt(PartieFehler.NichtGefunden);
    }

    private Task<Guid?> OffenePartieIdAsync(Guid spielerId, CancellationToken ct) =>
        db.Set<Partie>().AsNoTracking()
            .Where(p => p.SpielerId == spielerId && p.Status == PartieStatus.Offen)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>Jeder Transaktionsversuch beginnt ohne verfolgte Objekte: die Ausfuehrungsstrategie darf
    /// ihn wiederholen, und T2 laedt frisch nach der Engine.</summary>
    private void Leeren()
    {
        if (db is DbContext context)
        {
            context.ChangeTracker.Clear();
        }
    }
}
