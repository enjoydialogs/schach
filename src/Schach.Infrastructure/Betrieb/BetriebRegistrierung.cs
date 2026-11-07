using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Schach.Application.Betrieb;
using Schach.Infrastructure.Telemetry;

namespace Schach.Infrastructure.Betrieb;

public static class BetriebRegistrierung
{
    /// <summary>
    /// S2d (SCHACH-SPEC 14.2): Meter <c>Schach</c> mit allen Instrumenten, Betriebswarnungen,
    /// Betreibermails an <c>Email:OperatorEmail</c> und die Tageszeile. Den <c>IEmailSender</c>
    /// registriert S2a; bis dahin werden Betreibermails nur geloggt.
    /// </summary>
    public static IServiceCollection AddSchachBetrieb(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<BetriebOptions>().Bind(configuration.GetSection(BetriebOptions.Abschnitt));
        services.AddOptions<OperatorOptions>().Configure(o => o.Email = configuration[OperatorOptions.Schluessel] ?? "");

        services.AddMetrics();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SchachMeter>();
        services.TryAddSingleton<MeterSummen>();

        services.TryAddSingleton<OperatorPost>();
        services.TryAddSingleton<BetriebsWarner>();
        services.TryAddSingleton<IBetriebsWarner>(sp => sp.GetRequiredService<BetriebsWarner>());

        services.AddHostedService<OperatorMailVersand>();
        services.AddHostedService<TageszeileDienst>();

        return services;
    }
}
