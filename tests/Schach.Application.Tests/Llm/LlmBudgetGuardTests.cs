using Microsoft.Extensions.Time.Testing;
using Schach.Application.Llm;

namespace Schach.Application.Tests.Llm;

/// <summary>SCHACH-SPEC 11.1: Budgetleiter Ok, Warn, Exhausted aus dem Monats-Zaehlwerk.</summary>
public class LlmBudgetGuardTests
{
    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeVerbrauchSpeicher _speicher = new();
    private readonly FakeResolver _resolver = new();
    private readonly LlmBudgetOptions _optionen = new();

    private LlmBudgetGuard Guard() => new(_speicher, _resolver, _optionen, _zeit);

    private void Verbrauch(long prompt, long completion) =>
        _speicher.Staende[new DateOnly(2026, 11, 1)] = new LlmVerbrauchStand(prompt, completion);

    [Theory]
    // 5 € = 500 Cent = 5e8 Mikrocent; 17 Cent je Million Token, Warnstufe ab 4e8.
    [InlineData(0, 0, LlmBudgetLevel.Ok)]
    [InlineData(20_000_000, 3_000_000, LlmBudgetLevel.Ok)]
    [InlineData(20_000_000, 3_529_412, LlmBudgetLevel.Warn)]
    [InlineData(25_000_000, 4_411_765, LlmBudgetLevel.Exhausted)]
    public async Task Leiter_aus_dem_Verbrauch_des_laufenden_Monats(long prompt, long completion, LlmBudgetLevel erwartet)
    {
        Verbrauch(prompt, completion);

        (await Guard().GetStatusAsync(TestContext.Current.CancellationToken)).Level.Should().Be(erwartet);
    }

    [Fact]
    public async Task Vormonat_zaehlt_nicht()
    {
        _speicher.Staende[new DateOnly(2026, 10, 1)] = new LlmVerbrauchStand(100_000_000, 0);

        (await Guard().GetStatusAsync(TestContext.Current.CancellationToken)).Level.Should().Be(LlmBudgetLevel.Ok);
    }

    [Fact]
    public async Task Monatsgrenze_in_UTC()
    {
        _zeit.SetUtcNow(new DateTimeOffset(2026, 11, 30, 23, 30, 0, TimeSpan.Zero));
        Verbrauch(100_000_000, 0);
        var guard = Guard();
        (await guard.GetStatusAsync(TestContext.Current.CancellationToken)).Level.Should().Be(LlmBudgetLevel.Exhausted);

        _zeit.Advance(TimeSpan.FromHours(1));

        (await guard.GetStatusAsync(TestContext.Current.CancellationToken)).Level.Should().Be(LlmBudgetLevel.Ok);
    }

    [Fact]
    public async Task Modell_ohne_Preis_gilt_als_erschoepft()
    {
        _resolver.ModelName = "ministral-14b-2512";

        (await Guard().GetStatusAsync(TestContext.Current.CancellationToken)).Level.Should().Be(LlmBudgetLevel.Exhausted);
    }

    [Fact]
    public async Task Kaputte_Preisliste_gilt_als_erschoepft()
    {
        _optionen.ModelPricesCentsPerMillion = "{kaputt";

        (await Guard().GetStatusAsync(TestContext.Current.CancellationToken)).Level.Should().Be(LlmBudgetLevel.Exhausted);
    }

    [Fact]
    public async Task Budget_null_schaltet_Mistral_aus()
    {
        _optionen.MonthlyBudgetEuroCents = 0;

        (await Guard().GetStatusAsync(TestContext.Current.CancellationToken)).Level.Should().Be(LlmBudgetLevel.Exhausted);
    }

    [Fact]
    public async Task Stand_wird_60_Sekunden_gecacht()
    {
        var guard = Guard();
        (await guard.GetStatusAsync(TestContext.Current.CancellationToken)).Level.Should().Be(LlmBudgetLevel.Ok);

        Verbrauch(100_000_000, 0);
        _zeit.Advance(TimeSpan.FromSeconds(59));
        (await guard.GetStatusAsync(TestContext.Current.CancellationToken)).Level.Should().Be(LlmBudgetLevel.Ok);

        _zeit.Advance(TimeSpan.FromSeconds(2));
        (await guard.GetStatusAsync(TestContext.Current.CancellationToken)).Level.Should().Be(LlmBudgetLevel.Exhausted);
        _speicher.Lesungen.Should().Be(2);
    }

    [Fact]
    public void Pause_nach_429_haelt_60_Sekunden()
    {
        var guard = Guard();
        guard.IstPausiert.Should().BeFalse();

        guard.Melde429();
        _zeit.Advance(TimeSpan.FromSeconds(59));
        guard.IstPausiert.Should().BeTrue();

        _zeit.Advance(TimeSpan.FromSeconds(2));
        guard.IstPausiert.Should().BeFalse();
    }
}
