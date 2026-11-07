namespace Schach.Infrastructure.Llm;

/// <summary>
/// Abschnitt <c>Llm</c>, Anbieterwahl und Endpunkt (Uebernahme aus Kronrat, SCHACH-SPEC 6.3, 13.3).
/// Budget-Schluessel desselben Abschnitts stehen in <c>LlmBudgetOptions</c>.
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>„OpenAiCompatible“ oder „DeterministicFallback“. Jeder andere Wert gilt als aus.</summary>
    public string Provider { get; set; } = LlmProviderResolver.OpenAiCompatible;

    public string PromptsPath { get; set; } = "prompts";

    /// <summary>API-Basis (<c>…/v1</c>) oder voller Pfad (<c>…/v1/chat/completions</c>). EU-Endpunkt.</summary>
    public string Endpoint { get; set; } = "https://api.eu.mistral.ai/v1";

    /// <summary>Datierter Bezeichner, nie ein <c>-latest</c>-Alias (Preisliste wandert sonst nicht mit).</summary>
    public string Model { get; set; } = "ministral-8b-2512";

    /// <summary>Leer: Feld weglassen (ministral-8b-2512 hat keinen Denkmodus). Fuer mistral-small-2603: „none“.</summary>
    public string ReasoningEffort { get; set; } = "";

    /// <summary>Aus dem Dokploy-Panel (<c>MISTRAL_API_KEY</c> → <c>Llm__ApiKey</c>). Leer: nur Ersatzsprueche.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>HttpClient-Timeout; das Spruch-Zeitlimit (2500 ms) setzt der Aufrufer.</summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Die Basis mit abschliessendem Schraegstrich, an die <c>chat/completions</c> gehaengt wird.</summary>
    public string ChatCompletionsBase()
    {
        var trimmed = Endpoint.Trim().TrimEnd('/');
        if (trimmed.Length == 0)
        {
            return "";
        }

        const string suffix = "/chat/completions";
        if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^suffix.Length];
        }

        return trimmed + "/";
    }
}
