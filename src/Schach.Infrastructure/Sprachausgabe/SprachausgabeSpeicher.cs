using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schach.Application.Sprachausgabe;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Persistence;

namespace Schach.Infrastructure.Sprachausgabe;

/// <summary>
/// Ton-Cache <c>SpruchStimme</c> in Postgres (SCHACH-SPEC 6.9). Singleton mit eigenem Scope je
/// Aufruf, wie <c>LlmVerbrauchSpeicher</c>.
/// </summary>
public sealed class SpruchStimmeSpeicher(IServiceScopeFactory scopes) : ISpruchStimmeSpeicher
{
    public async Task<byte[]?> LeseAsync(byte[] hash, DateTimeOffset jetzt, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchachDbContext>();
        var audio = await db.Set<SpruchStimme>().AsNoTracking()
            .Where(s => s.Hash == hash)
            .Select(s => s.Audio)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (audio is not null)
        {
            await db.Set<SpruchStimme>()
                .Where(s => s.Hash == hash)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.ZuletztGenutztAm, jetzt), cancellationToken)
                .ConfigureAwait(false);
        }

        return audio;
    }

    /// <summary>Zwei gleichzeitige Erzeugungen desselben Satzes: der erste Eintrag bleibt.</summary>
    public async Task SpeichereAsync(byte[] hash, byte[] audio, int zeichen, DateTimeOffset jetzt, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchachDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "SpruchStimme" ("Hash", "Audio", "Zeichen", "ErstelltAm", "ZuletztGenutztAm")
            VALUES ({hash}, {audio}, {zeichen}, {jetzt}, {jetzt})
            ON CONFLICT ("Hash") DO NOTHING
            """,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> AufraeumenAsync(DateTimeOffset grenze, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchachDbContext>();
        return await db.Set<SpruchStimme>()
            .Where(s => s.ZuletztGenutztAm < grenze)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>Monats-Zaehlwerk <c>SprachausgabeVerbrauch</c>, atomar in Postgres.</summary>
public sealed class SprachVerbrauchSpeicher(IServiceScopeFactory scopes) : ISprachVerbrauchSpeicher
{
    public async Task<long> LeseZeichenAsync(DateOnly monat, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchachDbContext>();
        return await db.Set<SprachausgabeVerbrauch>().AsNoTracking()
            .Where(v => v.Monat == monat)
            .Select(v => v.Zeichen)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task BucheAsync(DateOnly monat, SprachBuchung buchung, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buchung);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchachDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "SprachausgabeVerbrauch" ("Monat", "Zeichen", "Aufrufe", "Zeitueberschreitungen", "Fehler")
            VALUES ({monat}, {buchung.Zeichen}, {buchung.Aufrufe}, {buchung.Zeitueberschreitungen}, {buchung.Fehler})
            ON CONFLICT ("Monat") DO UPDATE SET
                "Zeichen" = "SprachausgabeVerbrauch"."Zeichen" + EXCLUDED."Zeichen",
                "Aufrufe" = "SprachausgabeVerbrauch"."Aufrufe" + EXCLUDED."Aufrufe",
                "Zeitueberschreitungen" = "SprachausgabeVerbrauch"."Zeitueberschreitungen" + EXCLUDED."Zeitueberschreitungen",
                "Fehler" = "SprachausgabeVerbrauch"."Fehler" + EXCLUDED."Fehler"
            """,
            cancellationToken).ConfigureAwait(false);
    }
}
