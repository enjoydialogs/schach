using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Schach.Application.Betrieb;
using Schach.Infrastructure.Betrieb;

namespace Schach.Infrastructure.Tests.Betrieb;

/// <summary>SCHACH-SPEC 14.2: Warn-Logzeilen einmal je Fenster, Warnmail an den Betreiber.</summary>
public sealed class BetriebsWarnerTests
{
    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 11, 7, 10, 15, 0, TimeSpan.Zero));
    private readonly ListLogger<BetriebsWarner> _log = new();
    private readonly OperatorPost _post = new();

    private BetriebsWarner Warner(int fensterMinuten = 60) =>
        new(_zeit, _log, Options.Create(new BetriebOptions { WarnFensterMinuten = fensterMinuten }), _post);

    [Theory]
    [InlineData(BetriebsWarnung.EngineUeberlast, "EngineUeberlast")]
    [InlineData(BetriebsWarnung.MailKontingent, "MailKontingent")]
    [InlineData(BetriebsWarnung.BudgetWarn, "BudgetWarn")]
    [InlineData(BetriebsWarnung.BudgetErschoepft, "BudgetErschoepft")]
    public void Erste_Meldung_im_Fenster_schreibt_eine_Warnzeile_mit_dem_Namen(BetriebsWarnung art, string name)
    {
        var warner = Warner();

        warner.Melden(art, "Detail").Should().BeTrue();

        _log.Zeilen.Should().ContainSingle();
        var zeile = _log.Zeilen[0];
        zeile.Level.Should().Be(LogLevel.Warning);
        zeile.EventName.Should().Be(name);
        zeile.Text.Should().StartWith(name).And.Contain("Detail");
    }

    [Fact]
    public void Weitere_Meldungen_im_selben_Fenster_bleiben_still_und_ein_neues_Fenster_meldet_wieder()
    {
        var warner = Warner(fensterMinuten: 60);

        warner.Melden(BetriebsWarnung.EngineUeberlast).Should().BeTrue();
        _zeit.Advance(TimeSpan.FromMinutes(30));     // 10:45, selbes Stundenfenster
        warner.Melden(BetriebsWarnung.EngineUeberlast).Should().BeFalse();
        warner.Melden(BetriebsWarnung.MailKontingent).Should().BeTrue();   // andere Art, eigenes Fenster
        _zeit.Advance(TimeSpan.FromMinutes(20));     // 11:05, neues Fenster
        warner.Melden(BetriebsWarnung.EngineUeberlast).Should().BeTrue();

        _log.Zeilen.Select(z => z.EventName).Should().Equal("EngineUeberlast", "MailKontingent", "EngineUeberlast");
    }

    [Fact]
    public void Budgetwarnungen_melden_einmal_je_Kalendermonat()
    {
        var warner = Warner(fensterMinuten: 60);

        warner.Melden(BetriebsWarnung.BudgetWarn).Should().BeTrue();
        _zeit.Advance(TimeSpan.FromDays(10));        // 17.11.
        warner.Melden(BetriebsWarnung.BudgetWarn).Should().BeFalse();
        warner.Melden(BetriebsWarnung.BudgetErschoepft).Should().BeTrue();
        _zeit.Advance(TimeSpan.FromDays(14));        // 01.12.
        warner.Melden(BetriebsWarnung.BudgetWarn).Should().BeTrue();
    }

    [Fact]
    public void Nur_die_erste_Meldung_im_Fenster_reiht_eine_Betreibermail_ein()
    {
        var warner = Warner();

        warner.Melden(BetriebsWarnung.BudgetWarn, "82 % von 5,00 EUR");
        warner.Melden(BetriebsWarnung.BudgetWarn, "85 % von 5,00 EUR");

        _post.Reader.TryRead(out var mail).Should().BeTrue();
        mail!.Betreff.Should().Contain("BudgetWarn");
        mail.Text.Should().Contain("82 % von 5,00 EUR");
        _post.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void Zaehler_nennt_alle_Meldungen_seit_dem_letzten_Abruf_auch_die_stillen()
    {
        var warner = Warner();

        warner.Melden(BetriebsWarnung.EngineUeberlast);
        warner.Melden(BetriebsWarnung.EngineUeberlast);
        warner.Melden(BetriebsWarnung.MailKontingent);

        var zaehler = warner.ZaehlerAbholen();
        zaehler[BetriebsWarnung.EngineUeberlast].Should().Be(2);
        zaehler[BetriebsWarnung.MailKontingent].Should().Be(1);
        zaehler[BetriebsWarnung.BudgetWarn].Should().Be(0);
        warner.ZaehlerAbholen()[BetriebsWarnung.EngineUeberlast].Should().Be(0);
    }
}
