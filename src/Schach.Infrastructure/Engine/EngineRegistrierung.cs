using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;
using Schach.Application.Limits;
using Schach.Infrastructure.Limits;
using Schach.Infrastructure.Telemetry;

namespace Schach.Infrastructure.Engine;

public static class EngineRegistrierung
{
    /// <summary>
    /// S2b: Engine-Client hinter <see cref="IEngineClient"/>, Ueberlastwaechter,
    /// <see cref="IPartieStartLimits"/>, <see cref="LimitsOptions"/> und <see cref="SchachZaehler"/>.
    /// <c>Engine:BaseUrl</c> wird erst beim ersten Aufruf gebraucht; ohne Wert startet die App
    /// trotzdem (Tests, Entwicklung ohne Engine).
    /// </summary>
    public static IServiceCollection AddSchachEngine(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<EngineClientOptions>(configuration.GetSection(EngineClientOptions.Abschnitt));
        services.Configure<EngineUeberlastOptions>(configuration.GetSection(EngineClientOptions.Abschnitt));
        services.Configure<LimitsOptions>(configuration.GetSection(LimitsOptions.Abschnitt));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SchachZaehler>();
        services.TryAddSingleton<EngineUeberlastWaechter>();
        services.TryAddScoped<IPartieStartLimits, PartieStartLimits>();

        // Bewusst ohne AddStandardResilienceHandler: dessen Retries, Circuit Breaker und Fristen
        // widersprechen SCHACH-SPEC 5.2. Fristen und Retry stehen in EngineHttpClient.
        services.AddHttpClient<IEngineClient, EngineHttpClient>(EngineHttpClient.HttpClientName, (sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<EngineClientOptions>>().Value;
            http.BaseAddress = options.BaseUrl;
            http.Timeout = options.GesamtTimeout;
        });

        return services;
    }
}
