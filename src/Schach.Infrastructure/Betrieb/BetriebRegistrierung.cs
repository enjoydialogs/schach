using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Schach.Application.Betrieb;
using Schach.Infrastructure.Email;
using Schach.Infrastructure.Telemetry;

namespace Schach.Infrastructure.Betrieb;

public static class BetriebRegistrierung
{
    /// <summary>
    /// S2d (SCHACH-SPEC 14.2): Meter <c>Schach</c> mit allen Instrumenten, Betriebswarnungen,
    /// Betreibermails an <c>Email:OperatorEmail</c> und die Tageszeile. Gesendet wird ueber den
    /// <c>IEmailSender</c> aus S2a (<c>AddSchachKonto</c>); ohne ihn nur geloggt.
    /// </summary>
    public static IServiceCollection AddSchachBetrieb(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<BetriebOptions>().Bind(configuration.GetSection(BetriebOptions.Abschnitt));
        // Empfaenger aus Email:OperatorEmail (EmailOptionen, S2a). Doppeltes Binden derselben Sektion ist harmlos.
        services.AddOptions<EmailOptionen>().Bind(configuration.GetSection(EmailOptionen.Abschnitt));

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
