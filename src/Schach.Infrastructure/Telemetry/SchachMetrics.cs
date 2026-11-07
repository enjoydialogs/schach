namespace Schach.Infrastructure.Telemetry;

/// <summary>
/// Namen des Meters und seiner Instrumente (SCHACH-SPEC 14.2). Phase 0a legt nur die Namen fest;
/// die Instrumente erzeugt die Session, die sie zuerst zaehlt (S2b Limits und Engine, S2c LLM,
/// S2a Mail). Nie eine IP oder E-Mail-Adresse als Tag.
/// </summary>
public static class SchachMetrics
{
    public const string MeterName = "Schach";

    /// <summary>Tag <c>policy</c>: Name der Limiter-Policy.</summary>
    public const string LimitsAbgelehnt = "schach.limits.abgelehnt";

    /// <summary>Tags <c>quelle</c> (Mistral, Ersatz) und <c>grund</c>.</summary>
    public const string LlmAufrufe = "schach.llm.aufrufe";

    public const string LlmKostenMicrocent = "schach.llm.kosten_microcent";

    /// <summary>Tag <c>art</c>.</summary>
    public const string EngineFehler = "schach.engine.fehler";

    public const string MailGesendet = "schach.mail.gesendet";
}
