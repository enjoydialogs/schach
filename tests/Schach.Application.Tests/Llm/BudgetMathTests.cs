using Schach.Application.Llm;

namespace Schach.Application.Tests.Llm;

/// <summary>Kronrat-Uebernahme <c>BudgetMath</c>/<c>ModelPrice</c> (SCHACH-SPEC 11.1).</summary>
public class BudgetMathTests
{
    [Theory]
    [InlineData(0, 1000, 80, LlmBudgetLevel.Ok)]
    [InlineData(799, 1000, 80, LlmBudgetLevel.Ok)]
    [InlineData(800, 1000, 80, LlmBudgetLevel.Warn)]
    [InlineData(999, 1000, 80, LlmBudgetLevel.Warn)]
    [InlineData(1000, 1000, 80, LlmBudgetLevel.Exhausted)]
    [InlineData(5000, 1000, 80, LlmBudgetLevel.Exhausted)]
    // Budget 0 heisst „Mistral aus“, nie „unbegrenzt“.
    [InlineData(0, 0, 80, LlmBudgetLevel.Exhausted)]
    [InlineData(0, -1, 80, LlmBudgetLevel.Exhausted)]
    public void Level_bildet_die_Leiter(long used, long budget, int warnPercent, LlmBudgetLevel expected) =>
        BudgetMath.Level(used, budget, warnPercent).Should().Be(expected);

    [Fact]
    public void Ein_einzelner_Spruch_kostet_nicht_null()
    {
        // 400 Token Eingabe, 30 Ausgabe bei 17/17 Cent je Million: 7 310 Mikrocent.
        BudgetMath.CostMicroCents(400, 30, new ModelPrice(17, 17)).Should().Be(7_310);
    }

    [Fact]
    public void Preisliste_liest_das_Format_aus_der_Spec()
    {
        var preise = ModelPrices.Parse("""{"ministral-8b-2512":{"in":17,"out":17}}""");

        ModelPrices.For(preise, "MINISTRAL-8b-2512").Should().Be(new ModelPrice(17, 17));
        ModelPrices.For(preise, "ministral-14b-2512").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein json")]
    public void Kaputte_Preisliste_ist_leer(string? json) => ModelPrices.Parse(json).Should().BeEmpty();
}
