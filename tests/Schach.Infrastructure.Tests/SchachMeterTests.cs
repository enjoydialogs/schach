using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Schach.Infrastructure.Telemetry;

namespace Schach.Infrastructure.Tests;

/// <summary>SCHACH-SPEC 14.2: alle fuenf Instrumente des Meters <c>Schach</c>, nur erlaubte Tags,
/// nie IP oder E-Mail.</summary>
public sealed class SchachMeterTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();
    private readonly List<(string Name, long Wert, Dictionary<string, object?> Tags)> _messungen = [];
    private readonly MeterListener _listener = new();
    private readonly IMeterFactory _fabrik;

    public SchachMeterTests()
    {
        _fabrik = _services.GetRequiredService<IMeterFactory>();
        _listener.InstrumentPublished = (instrument, l) =>
        {
            // Nur Instrumente aus der eigenen Fabrik: andere Testklassen zaehlen parallel.
            if (instrument.Meter.Name == SchachMetrics.MeterName && ReferenceEquals(instrument.Meter.Scope, _fabrik))
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, wert, tags, _) =>
        {
            var d = new Dictionary<string, object?>();
            foreach (var t in tags)
            {
                d[t.Key] = t.Value;
            }

            lock (_messungen)
            {
                _messungen.Add((instrument.Name, wert, d));
            }
        });
        _listener.Start();
    }

    private SchachMeter Meter() => new(_fabrik);

    [Fact]
    public void Alle_fuenf_Instrumente_aus_Spec_14_2_zaehlen_mit_ihren_Tags()
    {
        var meter = Meter();

        meter.LimitAbgelehnt("zuege-je-partie");
        meter.LlmAufruf("mistral", "ok");
        meter.LlmKosten(1234);
        meter.EngineFehler("zeitueberschreitung");
        meter.MailGesendet();

        _messungen.Should().BeEquivalentTo(new[]
        {
            ("schach.limits.abgelehnt", 1L, new Dictionary<string, object?> { ["policy"] = "zuege-je-partie" }),
            ("schach.llm.aufrufe", 1L, new Dictionary<string, object?> { ["quelle"] = "mistral", ["grund"] = "ok" }),
            ("schach.llm.kosten_microcent", 1234L, new Dictionary<string, object?>()),
            ("schach.engine.fehler", 1L, new Dictionary<string, object?> { ["art"] = "zeitueberschreitung" }),
            ("schach.mail.gesendet", 1L, new Dictionary<string, object?>()),
        });
    }

    [Theory]
    [InlineData("203.0.113.7")]
    [InlineData("2001:db8::1")]
    [InlineData("spieler@example.org")]
    public void Ein_Tagwert_wie_IP_oder_Adresse_wird_nie_ausgegeben(string wert)
    {
        var meter = Meter();

        meter.LimitAbgelehnt(wert);
        meter.EngineFehler(wert);
        meter.LlmAufruf(wert, wert);

        _messungen.SelectMany(m => m.Tags.Values).Should().AllBeEquivalentTo("unzulaessig");
    }

    public void Dispose()
    {
        _listener.Dispose();
        _services.Dispose();
    }
}
