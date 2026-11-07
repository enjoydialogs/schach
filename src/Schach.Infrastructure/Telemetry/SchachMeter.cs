using System.Diagnostics.Metrics;
using System.Net;

namespace Schach.Infrastructure.Telemetry;

/// <summary>
/// Die fuenf Instrumente des Meters <c>Schach</c> (SCHACH-SPEC 14.2) an einer Stelle. Singleton.
/// Engine-Client und Limiter (S2b), LLM und Budget (S2c) und Mail (S2a) zaehlen nur hierueber und
/// legen keine eigenen Instrumente an.
///
/// Tags gibt es nur ueber die benannten Parameter. Sieht ein Wert wie eine IP oder E-Mail-Adresse
/// aus, wird er als <c>unzulaessig</c> ausgegeben (SCHACH-SPEC 14.3).
/// </summary>
public sealed class SchachMeter
{
    internal const string Unzulaessig = "unzulaessig";

    private readonly Counter<long> _limitsAbgelehnt;
    private readonly Counter<long> _llmAufrufe;
    private readonly Counter<long> _llmKosten;
    private readonly Counter<long> _engineFehler;
    private readonly Counter<long> _mailGesendet;

    public SchachMeter(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(SchachMetrics.MeterName);
        _limitsAbgelehnt = meter.CreateCounter<long>(SchachMetrics.LimitsAbgelehnt, description: "Vom Rate-Limiter abgelehnte Anfragen je Policy.");
        _llmAufrufe = meter.CreateCounter<long>(SchachMetrics.LlmAufrufe, description: "Spruch-Aufrufe je Quelle und Grund.");
        _llmKosten = meter.CreateCounter<long>(SchachMetrics.LlmKostenMicrocent, unit: "microcent", description: "Mistral-Kosten.");
        _engineFehler = meter.CreateCounter<long>(SchachMetrics.EngineFehler, description: "Engine-Fehler je Art.");
        _mailGesendet = meter.CreateCounter<long>(SchachMetrics.MailGesendet, description: "Vom Mail-Dienst angenommene Mails.");
    }

    /// <param name="policy">Name der Limiter-Policy, z. B. <c>zuege-je-partie</c>.</param>
    public void LimitAbgelehnt(string policy) =>
        _limitsAbgelehnt.Add(1, new KeyValuePair<string, object?>("policy", Tagwert(policy)));

    /// <param name="quelle">z. B. <c>mistral</c> oder <c>ersatz</c>.</param>
    /// <param name="grund">z. B. <c>ok</c>, <c>budget</c>, <c>zeitueberschreitung</c>.</param>
    public void LlmAufruf(string quelle, string grund) =>
        _llmAufrufe.Add(
            1,
            new KeyValuePair<string, object?>("quelle", Tagwert(quelle)),
            new KeyValuePair<string, object?>("grund", Tagwert(grund)));

    public void LlmKosten(long microcent) => _llmKosten.Add(microcent);

    /// <param name="art">z. B. <c>zeitueberschreitung</c>, <c>nicht-erreichbar</c>, <c>ueberlast</c>.</param>
    public void EngineFehler(string art) =>
        _engineFehler.Add(1, new KeyValuePair<string, object?>("art", Tagwert(art)));

    public void MailGesendet() => _mailGesendet.Add(1);

    internal static string Tagwert(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
        {
            return "leer";
        }

        if (wert.Contains('@', StringComparison.Ordinal)
            || ((wert.Contains('.', StringComparison.Ordinal) || wert.Contains(':', StringComparison.Ordinal))
                && IPAddress.TryParse(wert, out _)))
        {
            return Unzulaessig;
        }

        return wert;
    }
}
