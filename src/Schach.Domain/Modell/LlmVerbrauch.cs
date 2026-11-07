namespace Schach.Domain.Modell;

/// <summary>Mistral-Zaehlwerk je Monat (SCHACH-SPEC 11.1). Wird atomar per
/// <c>UPDATE … SET x = x + @n</c> hochgezaehlt.</summary>
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
