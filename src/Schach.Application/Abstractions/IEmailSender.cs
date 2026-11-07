namespace Schach.Application.Abstractions;

/// <summary>
/// Ausgehende Mail (Magic Link, Budget- und Warnmails). Umsetzungen: <c>LettermintEmailSender</c>
/// (Prod), <c>ConsoleEmailSender</c> (Entwicklung), <c>FileEmailSender</c> (Api.Tests).
///
/// Vertrag (SCHACH-SPEC 13.3): wirft nie, loggt keinen Inhalt und keine Adresse. Ein
/// Versandfehler kommt als <c>false</c> zurueck; der Aufrufer antwortet dann 503
/// <c>MailNichtGesendet</c> (SCHACH-SPEC 9.3).
/// </summary>
public interface IEmailSender
{
    /// <returns><c>true</c>, wenn der Dienst die Mail angenommen hat.</returns>
    Task<bool> SendAsync(OutboundEmail email, CancellationToken cancellationToken = default);
}

/// <summary>
/// Mail mit vollstaendigem Textteil und optionalem HTML-Teil aus denselben Bausteinen
/// (<c>SystemMailLayout</c>). <see cref="TextBody"/> ist immer fuer sich vollstaendig.
/// </summary>
public sealed record OutboundEmail(
    string To,
    string Subject,
    string TextBody,
    string? HtmlBody = null,
    string? ReplyTo = null);
