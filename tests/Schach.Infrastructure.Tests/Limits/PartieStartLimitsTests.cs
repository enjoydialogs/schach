using Schach.Application.Limits;
using Schach.Infrastructure.Limits;

namespace Schach.Infrastructure.Tests.Limits;

/// <summary>Entscheidung vor <c>POST /partien</c> nach SCHACH-SPEC 11.2 (ohne Datenbank).</summary>
public sealed class PartieStartLimitsTests
{
    private static readonly LimitsOptions Vorgaben = new();

    [Fact]
    public void Vorgaben_entsprechen_der_Spec()
    {
        Vorgaben.PartienJeSpielerUndTag.Should().Be(50);
        Vorgaben.GastPartienJeIpUndStunde.Should().Be(60);
        Vorgaben.OffeneGastPartienGesamt.Should().Be(300);
        Vorgaben.ZuegeJePartieUndMinute.Should().Be(40);
        Vorgaben.TippsJePartieUndMinute.Should().Be(10);
        Vorgaben.AktionenJePartieUndMinute.Should().Be(20);
        Vorgaben.EngineZuegeGleichzeitig.Should().Be(24);
        Vorgaben.EngineZuegeWarteschlange.Should().Be(48);
        Vorgaben.StockfishTippsGleichzeitig.Should().Be(2);
        Vorgaben.StockfishTippsWarteschlange.Should().Be(4);
        Vorgaben.AnmeldungenJeIpUnd10Minuten.Should().Be(5);
    }

    [Theory]
    [InlineData(49, null)]
    [InlineData(50, PartieStartAblehnung.TageslimitErreicht)]
    public void Spieler_hat_ein_Tageslimit(int partienLetzte24h, PartieStartAblehnung? erwartet) =>
        PartieStartLimits.Entscheide(istGast: false, partienLetzte24h, offeneGastPartien: 0, engineUeberlastet: false, Vorgaben)
            .Should().Be(erwartet);

    [Fact]
    public void Spieler_starten_auch_bei_vielen_Gaesten_und_Ueberlast() =>
        PartieStartLimits.Entscheide(istGast: false, 0, offeneGastPartien: 300, engineUeberlastet: true, Vorgaben)
            .Should().BeNull();

    [Theory]
    [InlineData(299, false, null)]
    [InlineData(300, false, PartieStartAblehnung.GeradeVielLos)]
    [InlineData(0, true, PartieStartAblehnung.GeradeVielLos)]
    public void Gaeste_werden_bei_vielen_offenen_Gast_Partien_oder_Ueberlast_abgewiesen(int offen, bool ueberlast, PartieStartAblehnung? erwartet) =>
        PartieStartLimits.Entscheide(istGast: true, partienLetzte24h: 0, offen, ueberlast, Vorgaben)
            .Should().Be(erwartet);
}
