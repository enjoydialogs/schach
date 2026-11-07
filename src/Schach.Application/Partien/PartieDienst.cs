using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Schach.Application.Abstractions;
using Schach.Application.Limits;
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
}

/// <summary>Ergebnis eines Partie-Use-Cases: eine <see cref="Sicht"/>, ein <see cref="Fehler"/> oder beides
/// (409 mit Zustand).</summary>
public sealed record PartieErgebnis(PartieSicht? Sicht, PartieFehler? Fehler = null, Guid? OffenePartieId = null)
{
    public static PartieErgebnis Abgelehnt(PartieFehler fehler, PartieSicht? sicht = null) => new(sicht, fehler);
}

/// <summary>Ein Gegner mit der Bilanz des Spielers; <c>null</c> fuer Gaeste.</summary>
public sealed record GegnerMitBilanz(Gegner Gegner, Bilanz? Bilanz);

/// <summary>
/// Partie-Kern ueber die Datenbank (SCHACH-SPEC 3.1 bis 3.3, 8.8, 8.10, 8.11). Zwei Transaktionen je Zug:
/// T1 Spielerzug, dann Engine ausserhalb jeder Transaktion, T2 Gegnerzug. Faellt die Engine aus, bleibt
/// <c>gegnerZugAusstehend</c> und der Sweep (S1d) holt den Zug ueber <see cref="GegnerZugNachholenAsync"/> nach.
/// Ein Concurrency-Konflikt (<c>Partie.Version</c>, Unique <c>Zug(PartieId, Halbzug)</c>) ergibt
/// <see cref="PartieFehler.HalbzugVeraltet"/> mit frischem Zustand.
/// </summary>
/// <param name="spielerId">In allen Methoden: angemeldeter Spieler oder <c>null</c> fuer Gaeste. Eine
/// Spieler-Partie sieht nur ihr Besitzer; eine Gast-Partie jeder, der die Id kennt.</param>
public sealed class PartieDienst(IAppDbContext db, IEngineClient engine, IPartieStartLimits limits, TimeProvider zeit)
{
    /// <summary><c>POST /partien</c>. Spielt der Gegner Weiss, enthaelt die Sicht seinen ersten Zug.</summary>
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

        try
        {
            await db.RunInTransactionAsync(
                async t =>
                {
                    Leeren();
                    db.Set<Partie>().Add(partie);
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

        var sicht = new PartieSicht(partie, [], [], null);
        return new PartieErgebnis(partie.GegnerZugAusstehend() ? await GegnerAntwortAsync(sicht, ct) : sicht);
    }

    /// <summary><c>GET /partien/{id}</c>.</summary>
    public async Task<PartieErgebnis> LadenAsync(Guid id, Guid? spielerId, CancellationToken ct = default)
    {
        Leeren();
        return await SichtAsync(id, spielerId, ct) is { } sicht
            ? new PartieErgebnis(sicht)
            : PartieErgebnis.Abgelehnt(PartieFehler.NichtGefunden);
    }

    /// <summary><c>GET /partien/offen</c>: die offene Partie des Spielers oder <c>null</c>.</summary>
    public async Task<PartieSicht?> OffeneAsync(Guid spielerId, CancellationToken ct = default)
    {
        Leeren();
        return await OffenePartieIdAsync(spielerId, ct) is { } id ? await SichtAsync(id, spielerId, ct) : null;
    }

    /// <summary><c>POST /partien/{id}/zuege</c> (SCHACH-SPEC 8.1, 8.8).</summary>
    public async Task<PartieErgebnis> ZugAsync(Guid id, Guid? spielerId, int halbzug, string uci, CancellationToken ct = default)
    {
        PartieErgebnis t1;
        try
        {
            t1 = await db.RunInTransactionAsync(t => SpielerzugAsync(id, spielerId, halbzug, uci, t), ct);
        }
        catch (DbUpdateException)
        {
            return await VeraltetAsync(id, spielerId, ct);
        }

        if (t1.Fehler is not null || t1.Sicht is not { } sicht)
        {
            return t1;
        }

        return new PartieErgebnis(sicht.Partie.GegnerZugAusstehend() ? await GegnerAntwortAsync(sicht, ct) : sicht);
    }

    /// <summary><c>POST /partien/{id}/zuruecknehmen</c> (SCHACH-SPEC 3.3).</summary>
    public async Task<PartieErgebnis> ZuruecknehmenAsync(Guid id, Guid? spielerId, int halbzug, CancellationToken ct = default)
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
        }
        catch (DbUpdateException)
        {
            return await VeraltetAsync(id, spielerId, ct);
        }
    }

    /// <summary><c>POST /partien/{id}/aufgeben</c> (SCHACH-SPEC 3.5): der Gegner gewinnt, Grund Aufgabe.</summary>
    public async Task<PartieErgebnis> AufgebenAsync(Guid id, Guid? spielerId, int halbzug, CancellationToken ct = default)
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

                    if (Vorab(sicht, halbzug) is { } fehler)
                    {
                        return fehler;
                    }

                    var gegnerFarbe = sicht.Partie.SpielerFarbe == Farbe.Weiss ? Farbe.Schwarz : Farbe.Weiss;
                    await BeendenAsync(sicht.Partie, Partieende.Sieg(gegnerFarbe, Grund.Aufgabe), t);
                    return new PartieErgebnis(sicht);
                },
                ct);
        }
        catch (DbUpdateException)
        {
            return await VeraltetAsync(id, spielerId, ct);
        }
    }

    /// <summary>Holt einen ausstehenden Gegnerzug nach (Sweep-Block B, S1d). Ohne ausstehenden Zug
    /// liefert er nur die Sicht.</summary>
    public async Task<PartieSicht?> GegnerZugNachholenAsync(Guid id, CancellationToken ct = default)
    {
        Leeren();
        var partie = await db.Set<Partie>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (partie is null || await SichtAsync(id, partie.SpielerId, ct) is not { } sicht)
        {
            return null;
        }

        return sicht.Partie.GegnerZugAusstehend() ? await GegnerAntwortAsync(sicht, ct) : sicht;
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

    private async Task<PartieErgebnis> SpielerzugAsync(Guid id, Guid? spielerId, int halbzug, string uci, CancellationToken t)
    {
        Leeren();
        if (await SichtAsync(id, spielerId, t) is not { } sicht)
        {
            return PartieErgebnis.Abgelehnt(PartieFehler.NichtGefunden);
        }

        var partie = sicht.Partie;
        switch (partie.OrdneEin(sicht.Zugfolge, halbzug, uci))
        {
            case Zugeinordnung.Wiederholung:
                // Gleicher Zug an gespielter Position: aktueller Zustand samt schon gespielter Antwort.
                return new PartieErgebnis(sicht with { GegnerZug = sicht.Zugfolge.FirstOrDefault(z => z.Halbzug == halbzug + 2) });
            case Zugeinordnung.PartieBeendet:
                return PartieErgebnis.Abgelehnt(PartieFehler.PartieBeendet, sicht);
            case Zugeinordnung.HalbzugVeraltet:
                return PartieErgebnis.Abgelehnt(PartieFehler.HalbzugVeraltet, sicht);
            case Zugeinordnung.NichtAmZug:
                return PartieErgebnis.Abgelehnt(PartieFehler.NichtAmZug, sicht);
        }

        Halbzugergebnis ergebnis;
        try
        {
            ergebnis = partie.SpieleSpielerzug(sicht.Zugfolge, uci, zeit);
        }
        catch (ZugUnzulaessigException)
        {
            return PartieErgebnis.Abgelehnt(PartieFehler.ZugUnzulaessig);
        }
        catch (UhrAbgelaufenException)
        {
            // Lazy fuer den Zug-Pfad (SCHACH-SPEC 8.7): wer ueberzieht, verliert.
            var stellung = Partiefuehrung.Verlauf(sicht.Zugfolge)[^1];
            await BeendenAsync(partie, Remisregeln.Zeitueberschreitung(stellung, partie.SpielerFarbe), t);
            return PartieErgebnis.Abgelehnt(PartieFehler.UhrAbgelaufen, sicht);
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

        return new PartieErgebnis(sicht);
    }

    /// <summary>Engine ausserhalb jeder Transaktion, dann T2. Faellt die Engine aus, bleibt der Zug
    /// ausstehend.</summary>
    private async Task<PartieSicht> GegnerAntwortAsync(PartieSicht vorher, CancellationToken ct)
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
            return vorher with { GegnerZug = null };
        }

        try
        {
            return await db.RunInTransactionAsync(
                async t =>
                {
                    Leeren();
                    var sicht = await SichtAsync(partie.Id, partie.SpielerId, t);
                    if (sicht is null || sicht.Partie.Halbzuege != halbzug || !sicht.Partie.GegnerZugAusstehend())
                    {
                        // Inzwischen geaendert (Zuruecknehmen, Aufgeben, Sweep): nichts mehr zu tun.
                        return sicht ?? vorher with { GegnerZug = null };
                    }

                    Halbzugergebnis ergebnis;
                    try
                    {
                        ergebnis = sicht.Partie.SpieleGegnerzug(sicht.Zugfolge, wahl, zeit);
                    }
                    catch (ZugUnzulaessigException)
                    {
                        return sicht;
                    }

                    if (ergebnis.Zug is { } zug)
                    {
                        db.Set<Zug>().Add(zug);
                        sicht = sicht with { Zugfolge = [.. sicht.Zugfolge, zug], GegnerZug = zug };
                    }

                    sicht.Partie.LetzterVersuchAm = null;
                    if (ergebnis.Ende is { } ende)
                    {
                        await BeendenAsync(sicht.Partie, ende, t);
                    }
                    else
                    {
                        await db.SaveChangesAsync(t);
                    }

                    return sicht;
                },
                ct);
        }
        catch (DbUpdateException)
        {
            Leeren();
            return await SichtAsync(partie.Id, partie.SpielerId, ct) ?? vorher with { GegnerZug = null };
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

        var zuege = await db.Set<Zug>().Where(z => z.PartieId == id).OrderBy(z => z.Halbzug).ToListAsync(ct);
        var sprueche = await db.Set<Spruch>().Where(s => s.PartieId == id).OrderBy(s => s.NachHalbzug).ThenBy(s => s.Id).ToListAsync(ct);
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
