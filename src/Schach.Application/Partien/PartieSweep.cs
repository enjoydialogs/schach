using Microsoft.EntityFrameworkCore;
using Schach.Application.Abstractions;
using Schach.Domain.Entitaeten;

namespace Schach.Application.Partien;

/// <summary>
/// Sweep-Bloecke A bis C (SCHACH-SPEC 8.7, 8.9). Block D (Anmeldungen) liegt bei
/// <c>AnmeldungenAufraeumen</c>. Jeder Block liefert die Zahl der bearbeiteten Partien.
/// </summary>
public sealed class PartieSweep(IAppDbContext db, PartieDienst dienst, TimeProvider zeit)
{
    /// <summary>Block B: erst wenn der letzte Versuch so alt ist.</summary>
    public static readonly TimeSpan NachholenNach = TimeSpan.FromSeconds(10);

    /// <summary>Block C: offene Gast-Partien ohne Zug so lange.</summary>
    public static readonly TimeSpan GastPartieOhneZug = TimeSpan.FromHours(24);

    /// <summary>Block A: abgelaufene Uhren beenden (Grund Zeit oder ZeitOhneMattmaterial, Ersatzspruch).</summary>
    public async Task<int> UhrenAsync(CancellationToken ct = default)
    {
        var mitUhr = await db.Set<Partie>().AsNoTracking()
            .Where(p => p.Status == PartieStatus.Offen && p.Bedenkzeit != Bedenkzeit.OhneUhr)
            .ToListAsync(ct);
        var beendet = 0;
        foreach (var partie in mitUhr.Where(p => PartieDienst.UhrAbgelaufen(p, zeit)))
        {
            if (await dienst.ZeitablaufAsync(partie.Id, partie.SpielerId, ct) is { Partie.Status: PartieStatus.Beendet })
            {
                beendet++;
            }
        }

        return beendet;
    }

    /// <summary>Block B: ausstehende Gegnerzuege nachholen (Engine-Ausfall oder Absturz zwischen T1 und T2).</summary>
    public async Task<int> GegnerzuegeAsync(CancellationToken ct = default)
    {
        var grenze = zeit.GetUtcNow() - NachholenNach;
        var ids = await db.Set<Partie>().AsNoTracking()
            .Where(p => p.Status == PartieStatus.Offen && p.LetzterVersuchAm != null && p.LetzterVersuchAm < grenze)
            .Select(p => p.Id)
            .ToListAsync(ct);
        var gezogen = 0;
        foreach (var id in ids)
        {
            if (await dienst.GegnerZugNachholenAsync(id, ct) is { GegnerZug: not null })
            {
                gezogen++;
            }
        }

        return gezogen;
    }

    /// <summary>Block C: offene Gast-Partien ohne Zug seit 24 h loeschen (Zug und Spruch per Kaskade).</summary>
    public Task<int> GastPartienAsync(CancellationToken ct = default)
    {
        var grenze = zeit.GetUtcNow() - GastPartieOhneZug;
        return db.Set<Partie>()
            .Where(p => p.SpielerId == null && p.Status == PartieStatus.Offen && p.LetzterZugAm < grenze)
            .ExecuteDeleteAsync(ct);
    }
}
