namespace Schach.Application.Llm;

/// <summary>Die Budgetleiter (SCHACH-SPEC 6.6, 11.1).</summary>
public enum LlmBudgetLevel
{
    /// <summary>Unter der Warnschwelle: alle bekommen live Sprueche.</summary>
    Ok,

    /// <summary>Ab Warnschwelle (80 %): Gaeste bekommen Ersatzsprueche.</summary>
    Warn,

    /// <summary>Budget aufgebraucht: alle bekommen Ersatzsprueche.</summary>
    Exhausted,
}

/// <summary>Uebernahme aus Kronrat (SCHACH-SPEC 13.3). Ganzzahlig in Mikrocent (10⁻⁶ Cent).</summary>
public static class BudgetMath
{
    public const long MicroCentsPerCent = 1_000_000L;

    /// <summary>
    /// Reine Funktion, einheitenfrei. Ein Budget von 0 (oder negativ) gilt als aufgebraucht, nicht
    /// als unbegrenzt: <c>Llm:MonthlyBudgetEuroCents=0</c> schaltet Mistral aus.
    /// </summary>
    public static LlmBudgetLevel Level(long used, long budget, int warnThresholdPercent)
    {
        if (budget <= 0 || used >= budget)
        {
            return LlmBudgetLevel.Exhausted;
        }

        // Multipliziert statt dividiert: kein Rundungsrand an genau 80 %.
        return used * 100 >= budget * (long)warnThresholdPercent ? LlmBudgetLevel.Warn : LlmBudgetLevel.Ok;
    }

    /// <summary>
    /// Kosten in Mikrocent. <c>Token × Cent je Million</c> ist bereits die Mikrocent-Zahl; es wird
    /// nirgends dividiert, damit ein einzelner Spruch nicht auf 0 Cent faellt.
    /// </summary>
    public static long CostMicroCents(long promptTokens, long completionTokens, ModelPrice price)
    {
        ArgumentNullException.ThrowIfNull(price);
        return (promptTokens * price.InputCentsPerMillion) + (completionTokens * price.OutputCentsPerMillion);
    }
}
