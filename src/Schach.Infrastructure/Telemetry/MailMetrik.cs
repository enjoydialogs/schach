using System.Diagnostics.Metrics;

namespace Schach.Infrastructure.Telemetry;

/// <summary>Zaehler <c>schach.mail.gesendet</c> (SCHACH-SPEC 14.2), ohne Tags: nie Adresse oder Inhalt.</summary>
internal static class MailMetrik
{
    private static readonly Meter Meter = new(SchachMetrics.MeterName);
    private static readonly Counter<long> GesendetZaehler = Meter.CreateCounter<long>(SchachMetrics.MailGesendet);

    public static void Gesendet() => GesendetZaehler.Add(1);
}
