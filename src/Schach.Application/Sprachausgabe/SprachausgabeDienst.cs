using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Schach.Application.Betrieb;
using Schach.Application.Llm;

namespace Schach.Application.Sprachausgabe;

/// <summary>Ergebnis von <see cref="SprachausgabeDienst.AudioAsync"/>: 200, 204 oder 404.</summary>
public enum SprachAusgang
{
    /// <summary>MP3 liegt vor (200 <c>audio/mpeg</c>).</summary>
    Audio,

    /// <summary>Aus, Budget, Pause, Fehler oder Zeitlimit: Stille (204), nie eine Fehlermeldung.</summary>
    Stumm,

    /// <summary>Token nicht echt oder nicht lesbar (404).</summary>
    Ungueltig,
}

public sealed record SprachTon(SprachAusgang Ausgang, byte[]? Audio = null);

/// <summary>
/// Sprachausgabe der Sprueche (SCHACH-SPEC 6.9). Singleton. Nie im Zugpfad: <see cref="Token"/>
/// liest nur die Konfiguration; der Ton entsteht erst bei <c>GET /sprachausgabe/{token}</c>.
/// Das Token traegt Gegner, Text und Gast-Flag, damit es auch nach dem Loeschen einer Gast-Partie
/// gilt und der Client keinen beliebigen Text sprechen lassen kann.
/// Eigenes Budget mit Zeichenzaehlung; die Chat-Leiter (<c>LlmBudgetGuard</c>) bleibt unberuehrt.
/// </summary>
public sealed partial class SprachausgabeDienst(
    ISprachProvider provider,
    ISpruchStimmeSpeicher cache,
    ISprachVerbrauchSpeicher verbrauch,
    IStimmSiegel siegel,
    IBetriebsWarner warner,
    SprachausgabeOptions options,
    TimeProvider zeit,
    ILogger<SprachausgabeDienst> logger) : ISpruchStimmen
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Lock _sperre = new();
    private (LlmBudgetLevel Stufe, DateTimeOffset GueltigBis, DateOnly Monat)? _budget;
    private DateTimeOffset _pauseBis = DateTimeOffset.MinValue;

    private bool An => options.Aktiv && options.MonthlyBudgetEuroCents > 0 && provider.Verfuegbar;

    public string? Token(string gegnerKey, string text, bool gast)
    {
        if (!An || Stimme(gegnerKey) is null || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return siegel.Versiegeln(JsonSerializer.Serialize(new Inhalt(gegnerKey, text, gast), Json));
    }

    public async Task<SprachTon> AudioAsync(string token, CancellationToken cancellationToken = default)
    {
        if (Oeffne(token) is not { } inhalt)
        {
            return new SprachTon(SprachAusgang.Ungueltig);
        }

        if (!An || Stimme(inhalt.Gegner) is not { } stimme)
        {
            return Stumm;
        }

        var hash = Hash(options.Model, stimme.VoiceId, inhalt.Text);
        var jetzt = zeit.GetUtcNow();
        if (await cache.LeseAsync(hash, jetzt, cancellationToken).ConfigureAwait(false) is { } vorhanden)
        {
            return new SprachTon(SprachAusgang.Audio, vorhanden);
        }

        if (IstPausiert || !await BudgetErlaubtAsync(inhalt.Gast, cancellationToken).ConfigureAwait(false))
        {
            return Stumm;
        }

        var audio = await ErzeugeAsync(inhalt, stimme, cancellationToken).ConfigureAwait(false);
        if (audio is null)
        {
            return Stumm;
        }

        await cache.SpeichereAsync(hash, audio, inhalt.Text.Length, jetzt, cancellationToken).ConfigureAwait(false);
        return new SprachTon(SprachAusgang.Audio, audio);
    }

    /// <summary>Sweep-Block E: Cache-Eintraege nach <c>CacheTage</c> ohne Nutzung loeschen.</summary>
    public Task<int> AufraeumenAsync(CancellationToken cancellationToken = default) =>
        cache.AufraeumenAsync(zeit.GetUtcNow().AddDays(-options.CacheTage), cancellationToken);

    /// <summary>SHA-256 ueber Modell, Voice-Id und Text: derselbe Satz mit derselben Stimme entsteht nur einmal.</summary>
    public static byte[] Hash(string model, string voiceId, string text) =>
        SHA256.HashData(Encoding.UTF8.GetBytes($"{model}\n{voiceId}\n{text}"));

    private static SprachTon Stumm { get; } = new(SprachAusgang.Stumm);

    private bool IstPausiert
    {
        get
        {
            lock (_sperre)
            {
                return zeit.GetUtcNow() < _pauseBis;
            }
        }
    }

    private StimmeOptions? Stimme(string gegnerKey) =>
        options.Stimmen.TryGetValue(gegnerKey, out var s) && !string.IsNullOrWhiteSpace(s.VoiceId) ? s : null;

    private Inhalt? Oeffne(string token)
    {
        if (string.IsNullOrEmpty(token) || siegel.Oeffnen(token) is not { } roh)
        {
            return null;
        }

        try
        {
            var inhalt = JsonSerializer.Deserialize<Inhalt>(roh, Json);
            return inhalt is { Gegner.Length: > 0, Text.Length: > 0 } ? inhalt : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Leiter wie 11.1: Ok alle, Warn nur Spieler, Erschoepft niemand. Je Stufe eine Meldung im Monat.</summary>
    private async Task<bool> BudgetErlaubtAsync(bool gast, CancellationToken cancellationToken)
    {
        LlmBudgetLevel stufe;
        try
        {
            stufe = await StufeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Log.BudgetUnbekannt(logger, ex.GetType().Name);
            return false;
        }

        return stufe == LlmBudgetLevel.Ok || (stufe == LlmBudgetLevel.Warn && !gast);
    }

    private async Task<LlmBudgetLevel> StufeAsync(CancellationToken cancellationToken)
    {
        var jetzt = zeit.GetUtcNow();
        var monat = LlmMonat.Von(jetzt);
        lock (_sperre)
        {
            if (_budget is { } b && b.Monat == monat && jetzt < b.GueltigBis)
            {
                return b.Stufe;
            }
        }

        var zeichen = await verbrauch.LeseZeichenAsync(monat, cancellationToken).ConfigureAwait(false);
        var budget = options.MonthlyBudgetEuroCents * BudgetMath.MicroCentsPerCent;

        // Zeichen × Cent je Million ist bereits die Mikrocent-Zahl (wie BudgetMath.CostMicroCents).
        var kosten = zeichen * options.CentJeMillionZeichen;
        var stufe = BudgetMath.Level(kosten, budget, options.WarnThresholdPercent);
        lock (_sperre)
        {
            _budget = (stufe, jetzt.AddSeconds(options.BudgetCacheSekunden), monat);
        }

        var anteil = budget > 0 ? kosten * 100 / budget : 100;
        if (stufe == LlmBudgetLevel.Warn)
        {
            warner.Melden(BetriebsWarnung.SprachausgabeBudgetWarn, $"Sprachausgabe {anteil} % von {options.MonthlyBudgetEuroCents} Cent");
        }
        else if (stufe == LlmBudgetLevel.Exhausted)
        {
            warner.Melden(BetriebsWarnung.SprachausgabeBudgetErschoepft, $"Sprachausgabe {anteil} % von {options.MonthlyBudgetEuroCents} Cent");
        }

        return stufe;
    }

    /// <summary>Ein Versuch mit hartem Zeitlimit. <c>null</c> heisst Stille.</summary>
    private async Task<byte[]?> ErzeugeAsync(Inhalt inhalt, StimmeOptions stimme, CancellationToken cancellationToken)
    {
        using var zeitlimit = new CancellationTokenSource(TimeSpan.FromMilliseconds(options.ZeitlimitMs), zeit);
        using var verbunden = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, zeitlimit.Token);
        var zeichen = inhalt.Text.Length;
        try
        {
            var audio = await provider.SprichAsync(
                new SprachAnfrage(options.Model, stimme.VoiceId, stimme.Expressiveness, inhalt.Text), verbunden.Token).ConfigureAwait(false);
            await BucheAsync(new SprachBuchung(zeichen, 1, 0, 0)).ConfigureAwait(false);
            return audio.Length > 0 ? audio : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Mistral rechnet den Aufruf womoeglich trotzdem ab.
            Log.Zeitlimit(logger, inhalt.Gegner, options.ZeitlimitMs);
            await BucheAsync(new SprachBuchung(zeichen, 1, 1, 0)).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            var status = (ex as HttpRequestException)?.StatusCode;
            if (status == HttpStatusCode.TooManyRequests)
            {
                lock (_sperre)
                {
                    _pauseBis = zeit.GetUtcNow().AddSeconds(options.PauseNach429Sekunden);
                }
            }
            else if (status == HttpStatusCode.NotFound)
            {
                // Geklonte Stimmen laufen nach hoechstens 365 Tagen ab (retention_notice).
                Log.StimmeUnbekannt(logger, inhalt.Gegner);
                warner.Melden(BetriebsWarnung.StimmeUnbekannt, $"Stimme fuer {inhalt.Gegner} bei Mistral nicht gefunden (abgelaufen?)");
            }

            Log.Fehler(logger, inhalt.Gegner, ex.GetType().Name, (int?)status);
            await BucheAsync(new SprachBuchung(0, 1, 0, 1)).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Das Zaehlwerk darf den Ton nie verhindern.</summary>
    private async Task BucheAsync(SprachBuchung buchung)
    {
        try
        {
            await verbrauch.BucheAsync(LlmMonat.Von(zeit.GetUtcNow()), buchung, CancellationToken.None).ConfigureAwait(false);
            lock (_sperre)
            {
                // Naechste Budgetfrage liest frisch, sonst ueberzieht ein voller Cache-Takt.
                _budget = _budget is { } b && buchung.Zeichen > 0 ? b with { GueltigBis = DateTimeOffset.MinValue } : _budget;
            }
        }
        catch (Exception ex)
        {
            Log.BuchungFehlgeschlagen(logger, ex.GetType().Name);
        }
    }

    private sealed record Inhalt(
        [property: JsonPropertyName("g")] string Gegner,
        [property: JsonPropertyName("t")] string Text,
        [property: JsonPropertyName("a")] bool Gast);

    // Nie Spruchtext loggen.
    private static partial class Log
    {
        [LoggerMessage(EventId = 3401, Level = LogLevel.Information, Message = "Sprachausgabe fuer {Gegner}: Zeitlimit {ZeitlimitMs} ms, Stille.")]
        public static partial void Zeitlimit(ILogger logger, string gegner, int zeitlimitMs);

        [LoggerMessage(EventId = 3402, Level = LogLevel.Warning, Message = "Sprachausgabe fuer {Gegner}: {Fehler} (HTTP {Status}), Stille.")]
        public static partial void Fehler(ILogger logger, string gegner, string fehler, int? status);

        [LoggerMessage(EventId = 3403, Level = LogLevel.Warning, Message = "Sprachausgabe: Stimme fuer {Gegner} unbekannt oder abgelaufen.")]
        public static partial void StimmeUnbekannt(ILogger logger, string gegner);

        [LoggerMessage(EventId = 3404, Level = LogLevel.Warning, Message = "Sprachausgabe: Budgetstand nicht lesbar ({Fehler}), Stille.")]
        public static partial void BudgetUnbekannt(ILogger logger, string fehler);

        [LoggerMessage(EventId = 3405, Level = LogLevel.Error, Message = "SprachausgabeVerbrauch nicht gebucht ({Fehler}).")]
        public static partial void BuchungFehlgeschlagen(ILogger logger, string fehler);
    }
}
