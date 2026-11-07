using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Schach.Application.Abstractions;
using Schach.Infrastructure.Email;

namespace Schach.Infrastructure.Identity;

public static class KontoServiceCollectionExtensions
{
    /// <summary>
    /// Session S2a: Mail (Sender nach <see cref="EmailOptionen.WirksamerProvider"/>, Kontingent) und
    /// die Dienste fuer Magic Link, Konto und Sweep-Block D.
    /// </summary>
    public static IServiceCollection AddSchachKonto(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<EmailOptionen>(configuration.GetSection(EmailOptionen.Abschnitt));
        services.Configure<MailLimits>(configuration.GetSection(MailLimits.Abschnitt));
        services.Configure<AnmeldungOptionen>(configuration.GetSection("Limits"));
        services.Configure<AnmeldungOptionen>(configuration.GetSection("Anmeldung"));

        var email = configuration.GetSection(EmailOptionen.Abschnitt).Get<EmailOptionen>() ?? new EmailOptionen();
        switch (email.WirksamerProvider)
        {
            case "Lettermint":
                services.AddHttpClient<IEmailSender, LettermintEmailSender>(c => c.Timeout = TimeSpan.FromSeconds(15));
                break;
            case "File":
                services.AddSingleton<IEmailSender, FileEmailSender>();
                break;
            case "Console":
                services.AddSingleton<IEmailSender, ConsoleEmailSender>();
                break;
            default:
                throw new InvalidOperationException($"Email:Provider '{email.Provider}' ist unbekannt (Lettermint, Console, File).");
        }

        services.AddSingleton<MailKontingent>();
        services.AddScoped<AnmeldeDienst>();
        services.AddScoped<KontoDienst>();
        services.AddScoped<AnmeldungenAufraeumen>();
        return services;
    }
}
