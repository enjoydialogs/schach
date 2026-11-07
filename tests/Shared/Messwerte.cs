using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;

namespace Schach.Tests.Shared;

/// <summary>
/// Eigene <see cref="IMeterFactory"/> je Test plus Listener, der nur deren Instrumente hoert. So
/// stoeren parallel laufende Tests einander nicht. Liefert je Instrument die gemessenen Tags.
/// </summary>
internal sealed class Messwerte : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly MeterListener _listener = new();
    private readonly ConcurrentQueue<(string Instrument, Dictionary<string, string?> Tags, long Wert)> _werte = new();

    public Messwerte()
    {
        _provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        Factory = _provider.GetRequiredService<IMeterFactory>();
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter.Scope, Factory))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, wert, tags, _) =>
        {
            var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                dict[tag.Key] = tag.Value?.ToString();
            }

            _werte.Enqueue((instrument.Name, dict, wert));
        });
        _listener.Start();
    }

    public IMeterFactory Factory { get; }

    /// <summary>Alle Messungen eines Instruments als Tag-Woerterbuecher.</summary>
    public IReadOnlyList<Dictionary<string, string?>> Von(string instrument) =>
        _werte.Where(w => w.Instrument == instrument).Select(w => w.Tags).ToList();

    public void Dispose()
    {
        _listener.Dispose();
        _provider.Dispose();
    }
}
