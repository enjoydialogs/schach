using Microsoft.EntityFrameworkCore;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Persistence;

namespace Schach.Infrastructure.Identity;

/// <summary>Eine Partie fuer den Export: Kopf, Zugfolge und Sprueche.</summary>
public sealed record ExportPartie(Partie Partie, IReadOnlyList<Zug> Zuege, IReadOnlyList<Spruch> Sprueche);

/// <summary>Alles, was zum Konto gespeichert ist (SCHACH-SPEC 9.3, 12.4).</summary>
public sealed record KontoDaten(Spieler Spieler, IReadOnlyList<Bilanz> Bilanz, IReadOnlyList<ExportPartie> Partien);

/// <summary>
/// Konto loeschen, exportieren und Sitzungen entwerten (SCHACH-SPEC 9.4, 9.7). Auch als Admin-Befehl
/// gedacht: nichts hier haengt am HTTP-Kontext.
/// </summary>
public sealed class KontoDienst(SchachDbContext db)
{
    /// <summary>
    /// Loescht hart in einer Transaktion: <c>Spieler</c> (die Datenbank kaskadiert auf Partie, Zug,
    /// Spruch und Bilanz), dazu offene Anmeldungen der Adresse und die Limit-Zaehler ihres Hashs.
    /// </summary>
    /// <returns><c>false</c>, wenn es den Spieler nicht (mehr) gibt.</returns>
    public Task<bool> LoeschenAsync(Guid spielerId, CancellationToken ct) =>
        db.RunInTransactionAsync(
            async t =>
            {
                var email = await db.Set<Spieler>().Where(s => s.Id == spielerId).Select(s => s.Email).FirstOrDefaultAsync(t);
                if (email is null)
                {
                    return false;
                }

                var hash = EmailAdresse.ZaehlHash(email);
                await db.Set<Spieler>().Where(s => s.Id == spielerId).ExecuteDeleteAsync(t);
                await db.Set<Anmeldung>().Where(a => a.Email == email).ExecuteDeleteAsync(t);
                await db.Set<AnmeldungVersuch>().Where(v => v.EmailHash == hash).ExecuteDeleteAsync(t);
                return true;
            },
            ct);

    /// <summary>Neuer Sicherheitsstempel: alle anderen Sitzungen werden ungueltig.</summary>
    public async Task StempelErneuernAsync(Guid spielerId, CancellationToken ct) =>
        await db.Set<Spieler>().Where(s => s.Id == spielerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Sicherheitsstempel, Guid.NewGuid()), ct);

    /// <summary>Aktueller Stempel per PK; <c>null</c>, wenn der Spieler fehlt.</summary>
    public Task<Guid?> StempelAsync(Guid spielerId, CancellationToken ct) =>
        db.Set<Spieler>().AsNoTracking().Where(s => s.Id == spielerId)
            .Select(s => (Guid?)s.Sicherheitsstempel).FirstOrDefaultAsync(ct);

    public Task<Spieler?> SpielerAsync(Guid spielerId, CancellationToken ct) =>
        db.Set<Spieler>().AsNoTracking().FirstOrDefaultAsync(s => s.Id == spielerId, ct);

    public async Task<KontoDaten?> ExportAsync(Guid spielerId, CancellationToken ct)
    {
        var spieler = await SpielerAsync(spielerId, ct);
        if (spieler is null)
        {
            return null;
        }

        var bilanz = await db.Set<Bilanz>().AsNoTracking().Where(b => b.SpielerId == spielerId)
            .OrderBy(b => b.GegnerId).ToListAsync(ct);
        var partien = await db.Set<Partie>().AsNoTracking().Where(p => p.SpielerId == spielerId)
            .OrderBy(p => p.ErstelltAm).ToListAsync(ct);
        var ids = partien.Select(p => p.Id).ToList();
        var zuege = (await db.Set<Zug>().AsNoTracking().Where(z => ids.Contains(z.PartieId)).ToListAsync(ct))
            .ToLookup(z => z.PartieId);
        var sprueche = (await db.Set<Spruch>().AsNoTracking().Where(s => ids.Contains(s.PartieId)).ToListAsync(ct))
            .ToLookup(s => s.PartieId);

        return new KontoDaten(
            spieler,
            bilanz,
            [.. partien.Select(p => new ExportPartie(
                p,
                [.. zuege[p.Id].OrderBy(z => z.Halbzug)],
                [.. sprueche[p.Id].OrderBy(s => s.NachHalbzug).ThenBy(s => s.Id)]))]);
    }
}
