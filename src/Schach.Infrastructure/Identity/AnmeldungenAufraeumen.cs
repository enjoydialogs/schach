using Microsoft.EntityFrameworkCore;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Persistence;

namespace Schach.Infrastructure.Identity;

/// <summary>
/// Sweep-Block D (SCHACH-SPEC 8.7, 9.2, 12.5): loescht <c>Anmeldung</c>-Zeilen aelter als 1 h und
/// <c>AnmeldungVersuch</c>-Zeilen aelter als 24 h.
/// </summary>
public sealed class AnmeldungenAufraeumen(SchachDbContext db, TimeProvider zeit)
{
    public static readonly TimeSpan AnmeldungHoechstens = TimeSpan.FromHours(1);

    public static readonly TimeSpan VersuchHoechstens = TimeSpan.FromHours(24);

    /// <returns>Zahl der geloeschten Zeilen (Anmeldung, AnmeldungVersuch).</returns>
    public async Task<(int Anmeldungen, int Versuche)> AusfuehrenAsync(CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var grenzeAnmeldung = jetzt - AnmeldungHoechstens;
        var grenzeVersuch = jetzt - VersuchHoechstens;
        var anmeldungen = await db.Set<Anmeldung>().Where(a => a.ErstelltAm < grenzeAnmeldung).ExecuteDeleteAsync(ct);
        var versuche = await db.Set<AnmeldungVersuch>().Where(v => v.ErstelltAm < grenzeVersuch).ExecuteDeleteAsync(ct);
        return (anmeldungen, versuche);
    }
}
