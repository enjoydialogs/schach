using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;

namespace Schach.Infrastructure.Email;

/// <summary>
/// Lokaler Sender ohne Versand (SCHACH-SPEC 13.4). Nur in der Umgebung <c>Development</c> schreibt er
/// Empfaenger und Text ins Log, damit Link und Code lokal ablesbar sind. In jeder anderen Umgebung
/// (etwa Prod ohne Lettermint-Token) loggt er nur, dass nichts gesendet wurde: nie Adresse oder Inhalt
/// (SCHACH-SPEC 14.3).
/// </summary>
public sealed partial class ConsoleEmailSender(IHostEnvironment umgebung, ILogger<ConsoleEmailSender> log) : IEmailSender
{
    public Task<bool> SendAsync(OutboundEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        if (umgebung.IsDevelopment())
        {
            Entwicklung(log, email.To, email.Subject, email.TextBody);
        }
        else
        {
            NichtGesendet(log);
        }

        return Task.FromResult(true);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "E-MAIL (Console, nur Development) an {To} | {Subject}\n{TextBody}")]
    private static partial void Entwicklung(ILogger log, string to, string subject, string textBody);

    [LoggerMessage(Level = LogLevel.Warning, Message = "mail.send outcome=console (kein Lettermint-Token, nichts gesendet)")]
    private static partial void NichtGesendet(ILogger log);
}

/// <summary>
/// Testsender (SCHACH-SPEC 15.2): legt jede Mail als JSON-Datei in <see cref="EmailOptionen.FileDirectory"/> ab.
/// Api.Tests lesen Link und Code daraus.
/// </summary>
public sealed class FileEmailSender(IOptions<EmailOptionen> optionen) : IEmailSender
{
    public async Task<bool> SendAsync(OutboundEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        var ordner = optionen.Value.FileDirectory;
        if (string.IsNullOrWhiteSpace(ordner))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(ordner);
            var datei = Path.Combine(ordner, $"{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(datei, JsonSerializer.Serialize(email), cancellationToken);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
