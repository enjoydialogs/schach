namespace Schach.Infrastructure.Telemetry;

/// <summary>
/// Namen des Meters und seiner Instrumente (SCHACH-SPEC 14.2). Die Instrumente selbst legt S2d in
/// <see cref="SchachMeter"/> an; S2b (Limits, Engine), S2c (LLM) und S2a (Mail) zaehlen ueber dessen
/// Methoden. Nie eine IP oder E-Mail-Adresse als Tag.
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
