using System.Net;
using Microsoft.Extensions.Logging;
using Schach.Application.Abstractions;
using Schach.Application.Llm;
using Schach.Domain;
using Schach.Domain.Entitaeten;

namespace Schach.Application.Sprueche;

/// <summary>Einstellungen, Abschnitt <c>Spruch</c> (SCHACH-SPEC 6.3, 6.2, 11.1).</summary>
public sealed class SpruchOptions
{
    public const string SectionName = "Spruch";

    public int ZeitlimitMs { get; set; } = 2500;

    public double Temperature { get; set; } = 0.8;

    public int MaxTokens { get; set; } = 80;

    public int MaxZeichen { get; set; } = SpruchPruefer.MaxZeichen;

    public int AbstandTierB { get; set; } = 2;

    public int AbstandTierC { get; set; } = 4;

    /// <summary>Hoechstens so viele Nicht-Pflicht-Sprueche je Partie (zaehlt die Orchestrierung, S1d).</summary>
    public int MaxJePartie { get; set; } = 20;

    public double BewertungssprungSchwelle { get; set; } = 0.25;

    public bool Bewertungssprung { get; set; } = true;

    /// <summary>Mistral-Aufrufe je Spieler-Partie, Partieende-Spruch ausgenommen.</summary>
    public int MaxMistralJePartie { get; set; } = 20;

    public int MaxMistralJeGastPartie { get; set; } = 12;

    /// <summary>Mit Bedenkzeit und weniger Gegner-Restzeit: Ersatzspruch statt Mistral (6.2).</summary>
    public long MinGegnerRestMs { get; set; } = 30_000;

    /// <summary>Kopie mit den Abstaenden des Gegners aus dem Gegner-Katalog, sonst unveraendert (6.2).
    /// Deckel wie <see cref="MaxJePartie"/> bleiben.</summary>
    public SpruchOptions FuerGegner(string gegnerKey)
    {
        var kopie = (SpruchOptions)MemberwiseClone();
        if (GegnerKatalog.Finde(gegnerKey) is { } gegner)
        {
            kopie.AbstandTierB = gegner.AbstandTierB ?? AbstandTierB;
            kopie.AbstandTierC = gegner.AbstandTierC ?? AbstandTierC;
        }

        return kopie;
    }
}

/// <summary>Prompt-Bausteine je Gegner: Notiz <c>prompts/gegner/{key}.de.md</c> fuer <c>{{persona}}</c>
/// und die Tonregel seiner Frechheit fuer <c>{{tonregel}}</c> (SCHACH-SPEC 4.2, 6.3).</summary>
public interface IPersonaQuelle
{
    string Persona(string gegnerKey);

    string Tonregel(string gegnerKey);
}

/// <param name="Partie">Die Partie; <see cref="Partie.MistralAufrufe"/> wird hier hochgezaehlt, der Aufrufer speichert.</param>
/// <param name="GegnerName">Anzeigename aus dem Gegner-Katalog.</param>
/// <param name="NachHalbzug">Halbzug, nach dem der Spruch erscheint.</param>
/// <param name="Daten">Daten fuer den Prompt (6.3, Abschnitt 4).</param>
/// <param name="Werte">Platzhalter fuer den Ersatzspruch.</param>
/// <param name="BisherigeTexte">Alle Spruchtexte dieser Partie, gegen Wiederholung.</param>
/// <param name="NurErsatz">Ohne Mistral, z. B. Partieende durch den Sweep (SCHACH-SPEC 6.1).</param>
public sealed record SpruchAnfrage(
    Partie Partie,
    string GegnerName,
    SpruchEreignis Ereignis,
    int NachHalbzug,
    SpruchDaten Daten,
    ErsatzWerte Werte,
    IReadOnlyCollection<string> BisherigeTexte,
    bool NurErsatz = false);

/// <summary>
/// Erzeugt genau einen Spruch (SCHACH-SPEC 6). Fragt Mistral nur, wenn Anbieter, 429-Pause,
/// Deckel je Partie, Gegner-Restzeit und Budgetleiter es erlauben; jeder Fehler, jedes Zeitlimit
/// und jede abgelehnte Antwort ergibt still einen Ersatzspruch (<see cref="SpruchQuelle.Ersatz"/>).
/// Welches Ereignis und ob ueberhaupt gesprochen wird, entscheidet die Orchestrierung (S1d).
/// Der Aufrufer startet die Aufgabe parallel zur Engine und fuegt den Spruch der Partie hinzu.
/// </summary>
public sealed partial class SpruchDienst(
    ILlmProviderResolver resolver,
    ILlmBudgetGuard guard,
    ILlmVerbrauchSpeicher verbrauch,
    ErsatzKatalog ersatz,
    IPersonaQuelle persona,
    SpruchOptions options,
    TimeProvider zeit,
    ILogger<SpruchDienst> logger)
{
    public const string PromptDatei = "spruch.de.md";

    /// <summary>Tokens eines abgebrochenen Aufrufs; gesetzt aus <c>Llm:AbgebrochenerAufrufTokenSchaetzung</c>.</summary>
    public int AbgebrochenerAufrufTokenSchaetzung { get; init; } = 600;

    public async Task<Spruch> ErzeugeAsync(SpruchAnfrage anfrage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        string? text = null;
        if (await MistralErlaubtAsync(anfrage, cancellationToken).ConfigureAwait(false))
        {
            anfrage.Partie.MistralAufrufe++;
            text = await FrageMistralAsync(anfrage, cancellationToken).ConfigureAwait(false);
        }

        var quelle = text is null ? SpruchQuelle.Ersatz : SpruchQuelle.Mistral;
        text ??= ersatz.Waehle(
            anfrage.Partie.GegnerId, anfrage.Ereignis, anfrage.Partie.Seed, anfrage.NachHalbzug, anfrage.BisherigeTexte, anfrage.Werte);

        return new Spruch
        {
            PartieId = anfrage.Partie.Id,
            NachHalbzug = anfrage.NachHalbzug,
            Ereignis = anfrage.Ereignis,
            Text = text,
            Quelle = quelle,
        };
    }

    private async Task<bool> MistralErlaubtAsync(SpruchAnfrage anfrage, CancellationToken cancellationToken)
    {
        var partie = anfrage.Partie;
        var gast = partie.SpielerId is null;

        if (anfrage.NurErsatz || anfrage.Ereignis == SpruchEreignis.Partiebeginn || resolver.IsOff || guard.IstPausiert)
        {
            return false;
        }

        var partieende = anfrage.Ereignis is SpruchEreignis.Sieg or SpruchEreignis.Niederlage or SpruchEreignis.Remis;
        var deckel = gast ? options.MaxMistralJeGastPartie : options.MaxMistralJePartie;
        if (partie.MistralAufrufe >= deckel && (gast || !partieende))
        {
            return false;
        }

        if (partie.Bedenkzeit != Bedenkzeit.OhneUhr && partie.GegnerRestMs < options.MinGegnerRestMs)
        {
            return false;
        }

        LlmBudgetLevel stufe;
        try
        {
            stufe = (await guard.GetStatusAsync(cancellationToken).ConfigureAwait(false)).Level;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Log.BudgetUnbekannt(logger, ex.GetType().Name);
            return false;
        }

        return stufe == LlmBudgetLevel.Ok || (stufe == LlmBudgetLevel.Warn && !gast);
    }

    /// <summary>Ein Versuch mit hartem Zeitlimit. <c>null</c> heisst Ersatzspruch.</summary>
    private async Task<string?> FrageMistralAsync(SpruchAnfrage anfrage, CancellationToken cancellationToken)
    {
        var key = anfrage.Partie.GegnerId;
        using var zeitlimit = new CancellationTokenSource(TimeSpan.FromMilliseconds(options.ZeitlimitMs), zeit);
        using var verbunden = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, zeitlimit.Token);

        try
        {
            var request = new LlmRequest(
                PromptDatei,
                new Dictionary<string, string>
                {
                    ["name"] = anfrage.GegnerName,
                    ["persona"] = persona.Persona(key),
                    ["tonregel"] = persona.Tonregel(key),
                    ["daten"] = anfrage.Daten.AlsJson(),
                },
                options.MaxTokens,
                options.Temperature);

            var antwort = await resolver.Current.CompleteAsync(request, verbunden.Token).ConfigureAwait(false);
            await BucheAsync(new LlmBuchung(antwort.PromptTokens, antwort.CompletionTokens, 1, 0, 0)).ConfigureAwait(false);

            var pruefung = SpruchPruefer.Pruefe(antwort.Text, anfrage.GegnerName);
            if (!pruefung.Ok)
            {
                Log.Abgelehnt(logger, key, pruefung.Grund!.Value);
                return null;
            }

            return pruefung.Text;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // Zeitlimit oder HttpClient-Timeout: die Antwort wird verworfen, die Kosten geschaetzt.
            Log.Zeitlimit(logger, key, options.ZeitlimitMs);
            await BucheAsync(new LlmBuchung(AbgebrochenerAufrufTokenSchaetzung, 0, 1, 1, 0)).ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            if (ex is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests })
            {
                guard.Melde429();
            }

            Log.Fehler(logger, key, ex.GetType().Name, (int?)(ex as HttpRequestException)?.StatusCode);
            await BucheAsync(new LlmBuchung(0, 0, 1, 0, 1)).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Das Zaehlwerk darf den Spruch nie verhindern.</summary>
    private async Task BucheAsync(LlmBuchung buchung)
    {
        try
        {
            await verbrauch.BucheAsync(LlmMonat.Von(zeit.GetUtcNow()), buchung, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.BuchungFehlgeschlagen(logger, ex.GetType().Name);
        }
    }

    // Nie Spruchtext, Prompt oder Spielerdaten loggen.
    private static partial class Log
    {
        [LoggerMessage(EventId = 3301, Level = LogLevel.Information, Message = "Spruch fuer {Gegner}: Zeitlimit {ZeitlimitMs} ms, Ersatzspruch.")]
        public static partial void Zeitlimit(ILogger logger, string gegner, int zeitlimitMs);

        [LoggerMessage(EventId = 3302, Level = LogLevel.Warning, Message = "Spruch fuer {Gegner}: {Fehler} (HTTP {Status}), Ersatzspruch.")]
        public static partial void Fehler(ILogger logger, string gegner, string fehler, int? status);

        [LoggerMessage(EventId = 3303, Level = LogLevel.Information, Message = "Spruch fuer {Gegner} abgelehnt: {Grund}, Ersatzspruch.")]
        public static partial void Abgelehnt(ILogger logger, string gegner, SpruchAblehnung grund);

        [LoggerMessage(EventId = 3304, Level = LogLevel.Warning, Message = "Budgetstand nicht lesbar ({Fehler}), Ersatzspruch.")]
        public static partial void BudgetUnbekannt(ILogger logger, string fehler);

        [LoggerMessage(EventId = 3305, Level = LogLevel.Error, Message = "LlmVerbrauch nicht gebucht ({Fehler}).")]
        public static partial void BuchungFehlgeschlagen(ILogger logger, string fehler);
    }
}
