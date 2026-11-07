using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;

namespace Schach.Infrastructure.Betrieb;

/// <summary>
/// Sendet die Mails aus <see cref="OperatorPost"/> an <c>Email:OperatorEmail</c> (SCHACH-SPEC 14.2).
/// Den <see cref="IEmailSender"/> registriert S2a; fehlt er oder die Adresse, wird nur geloggt.
/// Nie die Adresse oder den Inhalt loggen (SCHACH-SPEC 14.3).
/// </summary>
public sealed partial class OperatorMailVersand(
    OperatorPost post,
    IServiceProvider services,
    IOptions<OperatorOptions> optionen,
    ILogger<OperatorMailVersand> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var mail in post.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await SendenAsync(mail, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Herunterfahren.
        }
    }

    private async Task SendenAsync(OperatorMail mail, CancellationToken ct)
    {
        var adresse = optionen.Value.Email;
        if (string.IsNullOrWhiteSpace(adresse))
        {
            LogKeinEmpfaenger(logger, mail.Betreff);
            return;
        }

        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var sender = scope.ServiceProvider.GetService<IEmailSender>();
            if (sender is null)
            {
                LogKeinSender(logger, mail.Betreff);
                return;
            }

            bool gesendet;
            try
            {
                gesendet = await sender.SendAsync(new OutboundEmail(adresse, mail.Betreff, mail.Text), ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Vertrag sagt "wirft nie"; ein Bruch darf den Versand nicht beenden.
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                LogFehler(logger, mail.Betreff, ex.GetType().Name);
                return;
            }

            if (!gesendet)
            {
                LogNichtGesendet(logger, mail.Betreff);
            }
        }
    }

    [LoggerMessage(EventId = 3911, Level = LogLevel.Information, Message = "Betreibermail \"{Betreff}\" nicht gesendet: Email:OperatorEmail ist leer.")]
    private static partial void LogKeinEmpfaenger(ILogger logger, string betreff);

    [LoggerMessage(EventId = 3912, Level = LogLevel.Information, Message = "Betreibermail \"{Betreff}\" nicht gesendet: kein Mail-Dienst registriert.")]
    private static partial void LogKeinSender(ILogger logger, string betreff);

    [LoggerMessage(EventId = 3913, Level = LogLevel.Warning, Message = "Betreibermail \"{Betreff}\" vom Mail-Dienst nicht angenommen.")]
    private static partial void LogNichtGesendet(ILogger logger, string betreff);

    [LoggerMessage(EventId = 3914, Level = LogLevel.Warning, Message = "Betreibermail \"{Betreff}\" gescheitert: {Fehlerart}.")]
    private static partial void LogFehler(ILogger logger, string betreff, string fehlerart);
}
