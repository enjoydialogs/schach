using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Schach.Application.Abstractions;
using Schach.Application.Llm;
using Schach.Application.Sprueche;
using Schach.Domain;
using Schach.Infrastructure.Llm;

namespace Schach.Infrastructure;

/// <summary>Session S2c: Sprueche, Mistral, Budgetwaechter (SCHACH-SPEC 6, 11.1).</summary>
public static class SpruecheDependencyInjection
{
    public static IServiceCollection AddSprueche(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var llm = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
        var budget = configuration.GetSection(LlmOptions.SectionName).Get<LlmBudgetOptions>() ?? new LlmBudgetOptions();
        var spruch = configuration.GetSection(SpruchOptions.SectionName).Get<SpruchOptions>() ?? new SpruchOptions();
        var promptsDir = PromptVerzeichnis.Aufloesen(llm.PromptsPath);

        services.AddSingleton(llm);
        services.AddSingleton(budget);
        services.AddSingleton(spruch);
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IPromptCatalog>(new PromptCatalog(promptsDir));
        services.AddSingleton<IPersonaQuelle, PromptPersonaQuelle>();
        services.AddSingleton(_ => ErsatzKatalogLader.Lade(promptsDir));
        services.AddHostedService<ErsatzLadepruefung>();

        // Ein Versuch, kein Resilience-Handler (keine Wiederholung, SCHACH-SPEC 6.3).
        services.AddHttpClient<OpenAiCompatibleChatProvider>((_, client) => OpenAiCompatibleChatProvider.KonfiguriereClient(client, llm));
        services.AddSingleton<ILlmProviderResolver, LlmProviderResolver>();

        services.AddSingleton<ILlmVerbrauchSpeicher, LlmVerbrauchSpeicher>();
        services.AddSingleton<ILlmBudgetGuard, LlmBudgetGuard>();
        services.AddSingleton(sp => new SpruchDienst(
            sp.GetRequiredService<ILlmProviderResolver>(),
            sp.GetRequiredService<ILlmBudgetGuard>(),
            sp.GetRequiredService<ILlmVerbrauchSpeicher>(),
            sp.GetRequiredService<ErsatzKatalog>(),
            sp.GetRequiredService<IPersonaQuelle>(),
            spruch,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<SpruchDienst>>())
        {
            AbgebrochenerAufrufTokenSchaetzung = budget.AbgebrochenerAufrufTokenSchaetzung,
        });

        return services;
    }
}

/// <summary>Liest <c>prompts/gegner/{key}.de.md</c> und <c>prompts/tonregel/{frechheit}.de.md</c>.</summary>
internal sealed class PromptPersonaQuelle(IPromptCatalog prompts) : IPersonaQuelle
{
    private static readonly Dictionary<string, string> Leer = [];

    public string Persona(string gegnerKey) => prompts.Render($"gegner/{gegnerKey}.de.md", Leer).Trim();

    /// <summary>Die Zeilen tragen ihre Einrueckung selbst; darum nur am Ende kuerzen.</summary>
    public string Tonregel(string gegnerKey)
    {
        var frechheit = GegnerKatalog.Finde(gegnerKey)?.Frechheit ?? Frechheit.Freundlich;
        return prompts.Render($"tonregel/{frechheit.ToString().ToLowerInvariant()}.de.md", Leer).TrimEnd();
    }
}

/// <summary>
/// Ladepruefung beim Start (SCHACH-SPEC 6.5): Ersatzsprueche aller 8 Gegner, der Spruch-Prompt und
/// die Gegner-Notizen. Wirft, dann startet die App nicht.
/// </summary>
internal sealed class ErsatzLadepruefung(IServiceProvider services, IPromptCatalog prompts, IPersonaQuelle persona) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Wirft mit allen Befunden.
        var katalog = services.GetRequiredService<ErsatzKatalog>();
        foreach (var key in katalog.Gegner)
        {
            _ = persona.Persona(key);
            _ = persona.Tonregel(key);
        }

        _ = prompts.Render(SpruchDienst.PromptDatei, new Dictionary<string, string>
        {
            ["name"] = "",
            ["persona"] = "",
            ["tonregel"] = "",
            ["daten"] = "",
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
