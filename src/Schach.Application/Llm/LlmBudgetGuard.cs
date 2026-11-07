using Schach.Application.Abstractions;

namespace Schach.Application.Llm;

/// <summary>Einstellungen des Budgetwaechters, Abschnitt <c>Llm</c> (SCHACH-SPEC 11.1).</summary>
public sealed class LlmBudgetOptions
{
    /// <summary>Weicher Deckel je Monat; <c>0</c> = Mistral aus.</summary>
    public int MonthlyBudgetEuroCents { get; set; } = 500;

    public int WarnThresholdPercent { get; set; } = 80;

    /// <summary>JSON-Zeichenkette; ein fehlendes Modell gilt als erschoepft.</summary>
    public string ModelPricesCentsPerMillion { get; set; } = """{"ministral-8b-2512":{"in":17,"out":17}}""";

    public int BudgetCacheSekunden { get; set; } = 60;

    /// <summary>Tokens, die fuer einen abgebrochenen Aufruf (Zeitlimit) gebucht werden.</summary>
    public int AbgebrochenerAufrufTokenSchaetzung { get; set; } = 600;

    public int PauseNach429Sekunden { get; set; } = 60;
}

/// <summary>Monatsstand in Mikrocent und die Stufe der Leiter.</summary>
public sealed record LlmBudgetStatus(long CostMicroCents, long BudgetMicroCents, LlmBudgetLevel Level);

/// <summary>Budgetwaechter (SCHACH-SPEC 6.6, 11.1). Singleton: haelt Cache und 429-Pause.</summary>
public interface ILlmBudgetGuard
{
    Task<LlmBudgetStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Wahr bis <c>Llm:PauseNach429Sekunden</c> nach dem letzten <see cref="Melde429"/>.</summary>
    bool IstPausiert { get; }

    void Melde429();
}

/// <summary>Uebernahme von Kronrat <c>LlmBudgetGuard</c>, gelesen aus <c>LlmVerbrauch</c> statt aus der Job-Queue.</summary>
public sealed class LlmBudgetGuard(
    ILlmVerbrauchSpeicher speicher,
    ILlmProviderResolver resolver,
    LlmBudgetOptions options,
    TimeProvider zeit) : ILlmBudgetGuard
{
    private readonly Lock _sperre = new();
    private (LlmBudgetStatus Status, DateTimeOffset GueltigBis, DateOnly Monat)? _cache;
    private DateTimeOffset _pauseBis = DateTimeOffset.MinValue;

    public bool IstPausiert
    {
        get
        {
            lock (_sperre)
            {
                return zeit.GetUtcNow() < _pauseBis;
            }
        }
    }

    public void Melde429()
    {
        lock (_sperre)
        {
            _pauseBis = zeit.GetUtcNow().AddSeconds(options.PauseNach429Sekunden);
        }
    }

    public async Task<LlmBudgetStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var jetzt = zeit.GetUtcNow();
        var monat = LlmMonat.Von(jetzt);
        lock (_sperre)
        {
            if (_cache is { } c && c.Monat == monat && jetzt < c.GueltigBis)
            {
                return c.Status;
            }
        }

        var status = await BerechneAsync(monat, cancellationToken).ConfigureAwait(false);
        lock (_sperre)
        {
            _cache = (status, jetzt.AddSeconds(options.BudgetCacheSekunden), monat);
        }

        return status;
    }

    private async Task<LlmBudgetStatus> BerechneAsync(DateOnly monat, CancellationToken cancellationToken)
    {
        var budget = options.MonthlyBudgetEuroCents * BudgetMath.MicroCentsPerCent;

        // Ohne Anbieter keine Kosten: keine Preissuche, die sonst auf „erschoepft“ fiele.
        if (resolver.IsOff)
        {
            return new LlmBudgetStatus(0, budget, BudgetMath.Level(0, budget, options.WarnThresholdPercent));
        }

        // Fail-closed: ein Modell ohne Preiseintrag gilt als erschoepft.
        var price = ModelPrices.For(ModelPrices.Parse(options.ModelPricesCentsPerMillion), resolver.ModelName);
        if (price is null)
        {
            return new LlmBudgetStatus(budget, budget, LlmBudgetLevel.Exhausted);
        }

        var stand = await speicher.LeseAsync(monat, cancellationToken).ConfigureAwait(false);
        var cost = BudgetMath.CostMicroCents(stand.PromptTokens, stand.CompletionTokens, price);
        return new LlmBudgetStatus(cost, budget, BudgetMath.Level(cost, budget, options.WarnThresholdPercent));
    }
}
