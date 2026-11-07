using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Schach.Application.Betrieb;

namespace Schach.Infrastructure.Betrieb;

/// <summary>Umsetzung von <see cref="IBetriebsWarner"/>: Fenster im Speicher, Warnzeile, Betreibermail.</summary>
public sealed partial class BetriebsWarner(
    TimeProvider zeit,
    ILogger<BetriebsWarner> logger,
    IOptions<BetriebOptions> optionen,
    OperatorPost post) : IBetriebsWarner
{
    private readonly Lock _sperre = new();
    private readonly Dictionary<BetriebsWarnung, long> _letztesFenster = [];
    private readonly Dictionary<BetriebsWarnung, int> _zaehler = Enum.GetValues<BetriebsWarnung>().ToDictionary(a => a, _ => 0);

    public bool Melden(BetriebsWarnung art, string? zusatz = null)
    {
        var jetzt = zeit.GetUtcNow();
        var fenster = Fenster(art, jetzt);
        lock (_sperre)
        {
            _zaehler[art]++;
            if (_letztesFenster.TryGetValue(art, out var letztes) && letztes == fenster)
            {
                return false;
            }

            _letztesFenster[art] = fenster;
        }

        var text = string.IsNullOrWhiteSpace(zusatz) ? "-" : zusatz;
        Schreiben(art, text);
        post.Einreihen(new OperatorMail(
            $"Schach: {art}",
            string.Create(CultureInfo.InvariantCulture,
                $"{art} am {jetzt:yyyy-MM-dd HH:mm} UTC.\n\n{text}\n\nWeitere Meldungen dieser Art im selben Fenster gehen nur in die Tageszeile.\n")));
        return true;
    }

    /// <summary>Meldungen je Art seit dem letzten Abruf, auch die stillen; setzt auf null zurueck.
    /// Fuer die Tageszeile.</summary>
    public IReadOnlyDictionary<BetriebsWarnung, int> ZaehlerAbholen()
    {
        lock (_sperre)
        {
            var kopie = new Dictionary<BetriebsWarnung, int>(_zaehler);
            foreach (var art in kopie.Keys)
            {
                _zaehler[art] = 0;
            }

            return kopie;
        }
    }

    private long Fenster(BetriebsWarnung art, DateTimeOffset jetzt)
    {
        if (art is BetriebsWarnung.BudgetWarn or BetriebsWarnung.BudgetErschoepft)
        {
            return (jetzt.Year * 12L) + jetzt.Month;
        }

        var minuten = Math.Max(1, optionen.Value.WarnFensterMinuten);
        return jetzt.ToUnixTimeSeconds() / (minuten * 60L);
    }

    private void Schreiben(BetriebsWarnung art, string zusatz)
    {
        switch (art)
        {
            case BetriebsWarnung.BudgetWarn:
                LogBudgetWarn(logger, zusatz);
                break;
            case BetriebsWarnung.BudgetErschoepft:
                LogBudgetErschoepft(logger, zusatz);
                break;
            case BetriebsWarnung.EngineUeberlast:
                LogEngineUeberlast(logger, zusatz);
                break;
            case BetriebsWarnung.MailKontingent:
                LogMailKontingent(logger, zusatz);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(art), art, null);
        }
    }

    [LoggerMessage(EventId = 3901, EventName = "BudgetWarn", Level = LogLevel.Warning, Message = "BudgetWarn: {Zusatz}")]
    private static partial void LogBudgetWarn(ILogger logger, string zusatz);

    [LoggerMessage(EventId = 3902, EventName = "BudgetErschoepft", Level = LogLevel.Warning, Message = "BudgetErschoepft: {Zusatz}")]
    private static partial void LogBudgetErschoepft(ILogger logger, string zusatz);

    [LoggerMessage(EventId = 3903, EventName = "EngineUeberlast", Level = LogLevel.Warning, Message = "EngineUeberlast: {Zusatz}")]
    private static partial void LogEngineUeberlast(ILogger logger, string zusatz);

    [LoggerMessage(EventId = 3904, EventName = "MailKontingent", Level = LogLevel.Warning, Message = "MailKontingent: {Zusatz}")]
    private static partial void LogMailKontingent(ILogger logger, string zusatz);
}
