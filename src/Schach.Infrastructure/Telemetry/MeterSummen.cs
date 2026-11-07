using System.Diagnostics.Metrics;

namespace Schach.Infrastructure.Telemetry;

/// <summary>
/// Summiert die Instrumente des Meters <c>Schach</c> aus derselben <see cref="IMeterFactory"/> im
/// Prozess, ohne Tags, fuer die Tageszeile. Laeuft unabhaengig vom OTLP-Export. Singleton.
/// </summary>
public sealed class MeterSummen : IDisposable
{
    private static readonly string[] Instrumente =
    [
        SchachMetrics.LimitsAbgelehnt,
        SchachMetrics.LlmAufrufe,
        SchachMetrics.LlmKostenMicrocent,
        SchachMetrics.EngineFehler,
        SchachMetrics.MailGesendet,
    ];

    private readonly MeterListener _listener = new();
    private readonly Lock _sperre = new();
    private readonly Dictionary<string, long> _summen = Instrumente.ToDictionary(n => n, _ => 0L);

    public MeterSummen(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == SchachMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, meterFactory))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, wert, _, _) =>
        {
            lock (_sperre)
            {
                _summen[instrument.Name] = _summen.GetValueOrDefault(instrument.Name) + wert;
            }
        });
        _listener.Start();
    }

    /// <summary>Summen je Instrumentname seit dem letzten Abruf; setzt auf null zurueck.</summary>
    public IReadOnlyDictionary<string, long> Abholen()
    {
        lock (_sperre)
        {
            var kopie = new Dictionary<string, long>(_summen);
            foreach (var name in kopie.Keys)
            {
                _summen[name] = 0;
            }

            return kopie;
        }
    }

    public void Dispose() => _listener.Dispose();
}
