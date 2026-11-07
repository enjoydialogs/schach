namespace Schach.Domain.Entitaeten;

/// <summary>
/// Zaehlwerk der Mistral-Nutzung je Monat (SCHACH-SPEC 8.12, 11.1). Wird atomar per
/// <c>UPDATE … SET x = x + @n</c> hochgezaehlt, nie gelesen und zurueckgeschrieben.
/// </summary>
public class LlmVerbrauch
{
    /// <summary>Erster Tag des Monats (UTC).</summary>
    public DateOnly Monat { get; set; }

    public long PromptTokens { get; set; }

    public long CompletionTokens { get; set; }

    public int Aufrufe { get; set; }

    public int Zeitueberschreitungen { get; set; }

    public int Fehler { get; set; }

    public DateTimeOffset? WarnGemeldetAm { get; set; }

    public DateTimeOffset? ErschoepftGemeldetAm { get; set; }
}
