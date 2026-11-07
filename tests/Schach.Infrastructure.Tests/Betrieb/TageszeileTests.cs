using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Schach.Application.Betrieb;
using Schach.Infrastructure.Betrieb;
using Schach.Infrastructure.Telemetry;

namespace Schach.Infrastructure.Tests.Betrieb;

/// <summary>Tageszeile um 06:00 Berliner Zeit (SCHACH-SPEC 14.2).</summary>
public sealed class TageszeileTests : IDisposable
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly TimeOnly Sechs = new(6, 0);

    private readonly ServiceProvider _services = new ServiceCollection().AddMetrics().BuildServiceProvider();

    [Theory]
    // Winterzeit (UTC+1): 06:00 Berlin = 05:00 UTC.
    [InlineData("2026-11-07T10:15:00Z", "2026-11-08T05:00:00Z")]
    [InlineData("2026-11-07T04:30:00Z", "2026-11-07T05:00:00Z")]
    // Genau um 06:00 ist der Lauf schon vorbei: der naechste ist morgen.
    [InlineData("2026-11-07T05:00:00Z", "2026-11-08T05:00:00Z")]
    // Sommerzeit (UTC+2): 06:00 Berlin = 04:00 UTC.
    [InlineData("2027-06-01T12:00:00Z", "2027-06-02T04:00:00Z")]
    // Umstellung auf Sommerzeit am 28.03.2027: am Vortag noch +1, am Tag selbst schon +2.
    [InlineData("2027-03-27T10:00:00Z", "2027-03-28T04:00:00Z")]
    // Umstellung auf Winterzeit am 25.10.2026: am Vortag +2, am Tag selbst +1.
    [InlineData("2026-10-24T10:00:00Z", "2026-10-25T05:00:00Z")]
    public void Naechster_Lauf_ist_die_naechste_Uhrzeit_in_der_Zone(string jetzt, string erwartet)
    {
        var naechster = TageszeileDienst.NaechsterLauf(DateTimeOffset.Parse(jetzt, System.Globalization.CultureInfo.InvariantCulture), Berlin, Sechs);

        naechster.Should().Be(DateTimeOffset.Parse(erwartet, System.Globalization.CultureInfo.InvariantCulture));
        naechster.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Um_sechs_steht_eine_Zeile_mit_den_Zaehlern_des_Vortags()
    {
        var zeit = new FakeTimeProvider(new DateTimeOffset(2026, 11, 7, 10, 15, 0, TimeSpan.Zero));
        var optionen = Options.Create(new BetriebOptions());
        var warner = new BetriebsWarner(zeit, new ListLogger<BetriebsWarner>(), optionen, new OperatorPost());
        var fabrik = _services.GetRequiredService<IMeterFactory>();
        using var summen = new MeterSummen(fabrik);
        var meter = new SchachMeter(fabrik);
        var log = new ListLogger<TageszeileDienst>();
        using var dienst = new TageszeileDienst(zeit, optionen, warner, summen, log);

        meter.LimitAbgelehnt("zuege-je-partie");
        meter.LimitAbgelehnt("tipp-je-partie");
        meter.LlmKosten(1500);
        warner.Melden(BetriebsWarnung.EngineUeberlast);
        warner.Melden(BetriebsWarnung.EngineUeberlast);

        await dienst.StartAsync(TestContext.Current.CancellationToken);
        for (var i = 0; i < 200 && !log.Zeilen.Any(z => z.EventName == "Tageszeile"); i++)
        {
            zeit.Advance(TimeSpan.FromMinutes(10));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        await dienst.StopAsync(TestContext.Current.CancellationToken);

        var zeile = log.Zeilen.Should().ContainSingle(z => z.EventName == "Tageszeile").Subject;
        zeile.Level.Should().Be(LogLevel.Information);
        zeile.Text.Should().StartWith("Tageszeile 2026-11-07:")
            .And.Contain("limits_abgelehnt=2")
            .And.Contain("llm_aufrufe=0")
            .And.Contain("llm_kosten_microcent=1500")
            .And.Contain("engine_fehler=0")
            .And.Contain("mail_gesendet=0")
            .And.Contain("EngineUeberlast=2")
            .And.Contain("BudgetWarn=0");
        zeit.GetUtcNow().Should().BeOnOrAfter(new DateTimeOffset(2026, 11, 8, 5, 0, 0, TimeSpan.Zero));
    }

    public void Dispose() => _services.Dispose();
}
