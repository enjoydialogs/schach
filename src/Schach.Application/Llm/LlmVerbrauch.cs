namespace Schach.Application.Llm;

/// <summary>Tokenstand eines Monats aus <c>LlmVerbrauch</c>.</summary>
public sealed record LlmVerbrauchStand(long PromptTokens, long CompletionTokens);

/// <summary>Was ein Spruch-Aufruf zum Monats-Zaehlwerk beitraegt.</summary>
public sealed record LlmBuchung(long PromptTokens, long CompletionTokens, int Aufrufe, int Zeitueberschreitungen, int Fehler);

/// <summary>
/// Monats-Zaehlwerk <c>LlmVerbrauch</c> (SCHACH-SPEC 8.12, 11.1). <see cref="BucheAsync"/> zaehlt
/// atomar in der Datenbank hoch (<c>INSERT … ON CONFLICT DO UPDATE SET x = x + n</c>), nie lesen
/// und zurueckschreiben. Umgesetzt in <c>Schach.Infrastructure</c>.
/// </summary>
public interface ILlmVerbrauchSpeicher
{
    /// <param name="monat">Erster Tag des Monats (UTC).</param>
    Task<LlmVerbrauchStand> LeseAsync(DateOnly monat, CancellationToken cancellationToken = default);

    /// <param name="monat">Erster Tag des Monats (UTC).</param>
    Task BucheAsync(DateOnly monat, LlmBuchung buchung, CancellationToken cancellationToken = default);
}

public static class LlmMonat
{
    /// <summary>Schluessel des Zaehlwerks: erster Tag des Monats in UTC.</summary>
    public static DateOnly Von(DateTimeOffset zeitpunkt)
    {
        var utc = zeitpunkt.UtcDateTime;
        return new DateOnly(utc.Year, utc.Month, 1);
    }
}
