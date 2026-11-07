using System.Globalization;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Schach.Infrastructure.Telemetry;

namespace Schach.Infrastructure.Betrieb;

/// <summary>
/// Schreibt taeglich um <c>Betrieb:TageszeileUm</c> (06:00 Berliner Zeit) eine Logzeile mit den Zaehlern
/// seit der letzten Zeile (SCHACH-SPEC 14.2): die Summen der Schach-Instrumente und die Zahl der
/// Betriebswarnungen je Art, auch der stillen. Nur Log, keine Mail: eine taegliche Mail verbraucht
/// das Lettermint-Kontingent (SCHACH-SPEC 11.3).
///
/// Neue Zahlen kommen ueber <see cref="SchachMeter"/> von selbst in die Zeile. Die Zaehler liegen im
/// Speicher; nach einem Neustart zaehlt die naechste Zeile ab dem Start.
/// </summary>
public sealed partial class TageszeileDienst(
    TimeProvider zeit,
    IOptions<BetriebOptions> optionen,
    BetriebsWarner warner,
    MeterSummen summen,
    ILogger<TageszeileDienst> logger) : BackgroundService
{
    /// <summary>Der naechste Zeitpunkt (UTC) strikt nach <paramref name="jetztUtc"/>, an dem es in
    /// <paramref name="zone"/> <paramref name="um"/> Uhr ist.</summary>
    public static DateTimeOffset NaechsterLauf(DateTimeOffset jetztUtc, TimeZoneInfo zone, TimeOnly um)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var lokal = TimeZoneInfo.ConvertTime(jetztUtc, zone);
        var tag = DateOnly.FromDateTime(lokal.DateTime);
        for (var i = 0; i < 3; i++)
        {
            var kandidat = InUtc(tag.AddDays(i).ToDateTime(um), zone);
            if (kandidat > jetztUtc)
            {
                return kandidat;
            }
        }

        throw new InvalidOperationException("Kein naechster Lauf gefunden.");
    }

    private static DateTimeOffset InUtc(DateTime lokal, TimeZoneInfo zone)
    {
        // Eine Uhrzeit in der Umstellungsluecke gibt es nicht: eine Stunde spaeter nehmen.
        if (zone.IsInvalidTime(lokal))
        {
            lokal = lokal.AddHours(1);
        }

        // Doppelt vorhandene Uhrzeit: die erste (Sommerzeit-Versatz ist der groessere).
        var versatz = zone.IsAmbiguousTime(lokal) ? zone.GetAmbiguousTimeOffsets(lokal).Max() : zone.GetUtcOffset(lokal);
        return new DateTimeOffset(lokal, versatz).ToUniversalTime();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var zone = Zone();
        var um = optionen.Value.TageszeileUm;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var jetzt = zeit.GetUtcNow();
                var lauf = NaechsterLauf(jetzt, zone, um);
                await Task.Delay(lauf - jetzt, zeit, stoppingToken).ConfigureAwait(false);
                Schreiben(lauf, zone);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Herunterfahren.
        }
    }

    private void Schreiben(DateTimeOffset lauf, TimeZoneInfo zone)
    {
        var vortag = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(lauf, zone).DateTime).AddDays(-1);
        var text = new StringBuilder();
        foreach (var (name, wert) in summen.Abholen())
        {
            // schach.limits.abgelehnt -> limits_abgelehnt
            var kurz = name.StartsWith("schach.", StringComparison.Ordinal) ? name["schach.".Length..] : name;
            text.Append(CultureInfo.InvariantCulture, $"{kurz.Replace('.', '_')}={wert} ");
        }

        foreach (var (art, anzahl) in warner.ZaehlerAbholen())
        {
            text.Append(CultureInfo.InvariantCulture, $"{art}={anzahl} ");
        }

        var datum = vortag.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var werte = text.ToString().TrimEnd();
        LogTageszeile(logger, datum, werte);
    }

    private TimeZoneInfo Zone()
    {
        var id = optionen.Value.Zeitzone;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            LogZoneFehlt(logger, id);
            return TimeZoneInfo.Utc;
        }
    }

    [LoggerMessage(EventId = 3920, EventName = "Tageszeile", Level = LogLevel.Information, Message = "Tageszeile {Datum}: {Werte}")]
    private static partial void LogTageszeile(ILogger logger, string datum, string werte);

    [LoggerMessage(EventId = 3921, Level = LogLevel.Warning, Message = "Zeitzone {Zone} unbekannt; die Tageszeile laeuft nach UTC.")]
    private static partial void LogZoneFehlt(ILogger logger, string zone);
}
