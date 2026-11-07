namespace Schach.Api.Telemetry;

/// <summary>Logzeilen beim Start (Quellgenerator, CA1848). Nie IP, E-Mail oder Mailinhalt.</summary>
internal static partial class StartupLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Telemetrie ist an: {Key} ist gesetzt.")]
    public static partial void TelemetrieAn(ILogger logger, string key);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Telemetrie ist aus: {Key} ist nicht gesetzt.")]
    public static partial void TelemetrieAus(ILogger logger, string key);
}
