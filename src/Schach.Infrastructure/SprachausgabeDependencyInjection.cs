using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Schach.Application.Sprachausgabe;
using Schach.Infrastructure.Llm;
using Schach.Infrastructure.Sprachausgabe;

namespace Schach.Infrastructure;

/// <summary>Sprachausgabe der Sprueche (SCHACH-SPEC 6.9, Issue #77). Braucht <c>AddSprueche</c> (LlmOptions).</summary>
public static class SprachausgabeDependencyInjection
{
    public static IServiceCollection AddSprachausgabe(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var optionen = configuration.GetSection(SprachausgabeOptions.SectionName).Get<SprachausgabeOptions>() ?? new SprachausgabeOptions();
        var llm = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
        services.AddSingleton(optionen);
        services.TryAddSingleton(llm);
        services.TryAddSingleton(TimeProvider.System);

        // Ein Versuch, kein Resilience-Handler.
        services.AddHttpClient(MistralSprachProvider.ClientName, client =>
            MistralSprachProvider.KonfiguriereClient(client, llm, optionen));
        services.AddSingleton<ISprachProvider, MistralSprachProvider>();
        services.AddSingleton<ISpruchStimmeSpeicher, SpruchStimmeSpeicher>();
        services.AddSingleton<ISprachVerbrauchSpeicher, SprachVerbrauchSpeicher>();
        services.AddSingleton<IStimmSiegel, StimmSiegel>();

        // Singleton: haelt 429-Pause und Budget-Cache.
        services.AddSingleton<SprachausgabeDienst>();
        services.AddSingleton<ISpruchStimmen>(sp => sp.GetRequiredService<SprachausgabeDienst>());

        return services;
    }
}
