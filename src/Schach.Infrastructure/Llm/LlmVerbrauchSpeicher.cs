using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schach.Application.Llm;
using Schach.Infrastructure.Persistence;

namespace Schach.Infrastructure.Llm;

/// <summary>
/// Monats-Zaehlwerk in Postgres (SCHACH-SPEC 11.1). Singleton mit eigenem Scope je Aufruf, weil
/// der Budgetwaechter ein Singleton ist und Sprueche auch aus dem Sweep kommen.
/// </summary>
public sealed class LlmVerbrauchSpeicher(IServiceScopeFactory scopes) : ILlmVerbrauchSpeicher
{
    public async Task<LlmVerbrauchStand> LeseAsync(DateOnly monat, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchachDbContext>();
        var stand = await db.Set<Domain.Entitaeten.LlmVerbrauch>()
            .AsNoTracking()
            .Where(v => v.Monat == monat)
            .Select(v => new LlmVerbrauchStand(v.PromptTokens, v.CompletionTokens))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return stand ?? new LlmVerbrauchStand(0, 0);
    }

    /// <summary>Ein Statement, atomar in Postgres: nie lesen und zurueckschreiben.</summary>
    public async Task BucheAsync(DateOnly monat, LlmBuchung buchung, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buchung);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchachDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "LlmVerbrauch" ("Monat", "PromptTokens", "CompletionTokens", "Aufrufe", "Zeitueberschreitungen", "Fehler")
            VALUES ({monat}, {buchung.PromptTokens}, {buchung.CompletionTokens}, {buchung.Aufrufe}, {buchung.Zeitueberschreitungen}, {buchung.Fehler})
            ON CONFLICT ("Monat") DO UPDATE SET
                "PromptTokens" = "LlmVerbrauch"."PromptTokens" + EXCLUDED."PromptTokens",
                "CompletionTokens" = "LlmVerbrauch"."CompletionTokens" + EXCLUDED."CompletionTokens",
                "Aufrufe" = "LlmVerbrauch"."Aufrufe" + EXCLUDED."Aufrufe",
                "Zeitueberschreitungen" = "LlmVerbrauch"."Zeitueberschreitungen" + EXCLUDED."Zeitueberschreitungen",
                "Fehler" = "LlmVerbrauch"."Fehler" + EXCLUDED."Fehler"
            """,
            cancellationToken).ConfigureAwait(false);
    }
}
