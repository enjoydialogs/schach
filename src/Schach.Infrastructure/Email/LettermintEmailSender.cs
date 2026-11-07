using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;
using Schach.Infrastructure.Telemetry;

namespace Schach.Infrastructure.Email;

/// <summary>
/// Versand ueber die Lettermint-HTTP-API (<c>POST /v1/send</c>, Header <c>x-lettermint-token</c>).
/// Oeffnungs- und Klick-Tracking sind je Nachricht aus (SCHACH-SPEC 9.6).
/// Vertrag von <see cref="IEmailSender"/>: wirft nie, loggt weder Adresse noch Inhalt; die einzige
/// Logzeile ist <c>mail.send outcome=… status=… duration_ms=…</c>.
/// </summary>
public sealed partial class LettermintEmailSender(
    HttpClient http, IOptions<EmailOptionen> optionen, ILogger<LettermintEmailSender> log) : IEmailSender
{
    public async Task<bool> SendAsync(OutboundEmail email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        var o = optionen.Value;
        if (string.IsNullOrWhiteSpace(o.Lettermint.ApiToken) || string.IsNullOrWhiteSpace(o.FromAddress))
        {
            Ergebnis(log, "nicht_konfiguriert", 0, 0);
            return false;
        }

        var nachricht = new LettermintNachricht(
            o.FromAddress,
            [email.To],
            email.Subject,
            email.TextBody,
            email.HtmlBody,
            email.ReplyTo is null ? null : [email.ReplyTo],
            new LettermintSettings(TrackOpens: false, TrackClicks: false));

        var uhr = Stopwatch.StartNew();
        try
        {
            using var anfrage = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(o.Lettermint.BaseUrl), "send"))
            {
                Content = JsonContent.Create(nachricht),
            };
            anfrage.Headers.Add("x-lettermint-token", o.Lettermint.ApiToken);

            using var antwort = await http.SendAsync(anfrage, cancellationToken);
            var status = (int)antwort.StatusCode;
            if (antwort.IsSuccessStatusCode)
            {
                Ergebnis(log, "accepted", status, uhr.ElapsedMilliseconds);
                MailMetrik.Gesendet();
                return true;
            }

            Ergebnis(log, "rejected", status, uhr.ElapsedMilliseconds);
            return false;
        }
#pragma warning disable CA1031 // Vertrag: wirft nie. Jeder Fehler wird false.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            Ergebnis(log, "failed", 0, uhr.ElapsedMilliseconds);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "mail.send outcome={Outcome} status={Status} duration_ms={DurationMs}")]
    private static partial void Ergebnis(ILogger log, string outcome, int status, long durationMs);

    private sealed record LettermintNachricht(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("html"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Html,
        [property: JsonPropertyName("reply_to"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? ReplyTo,
        [property: JsonPropertyName("settings")] LettermintSettings Settings);

    private sealed record LettermintSettings(
        [property: JsonPropertyName("track_opens")] bool TrackOpens,
        [property: JsonPropertyName("track_clicks")] bool TrackClicks);
}
