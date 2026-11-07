using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Schach.Infrastructure.Engine;

namespace Schach.Infrastructure.Tests.Engine;

/// <summary>Ueberlastsperre nach SCHACH-SPEC 11.2: <c>Engine:UeberlastFehlerIn30s</c> Ausfaelle in
/// 30 s sperren neue Gast-Partien fuer <c>Engine:UeberlastSperreSekunden</c>.</summary>
public sealed class EngineUeberlastWaechterTests
{
    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));

    private EngineUeberlastWaechter Waechter(int fehler = 10, int sperreSekunden = 60) =>
        new(Options.Create(new EngineUeberlastOptions { UeberlastFehlerIn30s = fehler, UeberlastSperreSekunden = sperreSekunden }),
            _zeit,
            NullLogger<EngineUeberlastWaechter>.Instance);

    [Fact]
    public void Ohne_Ausfaelle_ist_nichts_gesperrt() => Waechter().IstGesperrt.Should().BeFalse();

    [Fact]
    public void Zehn_Ausfaelle_in_30_Sekunden_sperren_fuer_60_Sekunden()
    {
        var waechter = Waechter();
        for (var i = 0; i < 9; i++)
        {
            waechter.AusfallMelden();
            _zeit.Advance(TimeSpan.FromSeconds(3));
        }

        waechter.IstGesperrt.Should().BeFalse();
        waechter.AusfallMelden();
        waechter.IstGesperrt.Should().BeTrue();

        _zeit.Advance(TimeSpan.FromSeconds(59));
        waechter.IstGesperrt.Should().BeTrue();
        _zeit.Advance(TimeSpan.FromSeconds(1));
        waechter.IstGesperrt.Should().BeFalse();
    }

    [Fact]
    public void Ausfaelle_aelter_als_30_Sekunden_zaehlen_nicht()
    {
        var waechter = Waechter(fehler: 3);
        waechter.AusfallMelden();
        waechter.AusfallMelden();
        _zeit.Advance(TimeSpan.FromSeconds(31));

        waechter.AusfallMelden();

        waechter.IstGesperrt.Should().BeFalse();
    }
}
