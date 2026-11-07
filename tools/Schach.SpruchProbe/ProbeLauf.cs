using System.Diagnostics;
using System.Globalization;
using System.Text;
using Schach.Application.Abstractions;
using Schach.Application.Sprueche;
using Schach.Domain.Entitaeten;

namespace Schach.SpruchProbe;

/// <summary>Ein Szenario der Spruch-Probe: Ereignis plus Daten in dritter Person.</summary>
public sealed record ProbeSzenario(string Name, SpruchEreignis Ereignis, SpruchDaten Daten, ErsatzWerte Werte);

/// <summary>Die 14 Szenarien (SCHACH-SPEC 6.8). Partiebeginn fehlt: das ist immer ein Ersatzspruch.</summary>
public static class ProbeSzenarien
{
    private static SpruchDaten D(
        SpruchEreignis e, int zug, string phase, string? spieler, string? gegner, bool schach = false,
        int aenderung = 0, int bilanz = 0, string? stellung = null, string[]? merkmale = null, string? grund = null) =>
        new(e.ToString(), zug, phase, "Weiss", "OhneUhr", spieler, gegner, schach, aenderung, bilanz, stellung, merkmale ?? [], grund, []);

    public static readonly IReadOnlyList<ProbeSzenario> Alle =
    [
        new("sieg-matt", SpruchEreignis.Sieg, D(SpruchEreignis.Sieg, 31, "Endspiel", "Kg1", "Dg2#", true, 0, 4, grund: "Matt"), new(null, 31)),
        new("niederlage-aufgabe", SpruchEreignis.Niederlage, D(SpruchEreignis.Niederlage, 27, "Mittelspiel", "Txe8+", null, bilanz: -6, grund: "Aufgabe"), new(null, 27)),
        new("remis-wiederholung", SpruchEreignis.Remis, D(SpruchEreignis.Remis, 35, "Endspiel", "Kf2", "Kg7", grund: "Wiederholung"), new(null, 35)),
        new("remis-abgelehnt", SpruchEreignis.RemisAbgelehnt, D(SpruchEreignis.RemisAbgelehnt, 12, "Mittelspiel", null, null, grund: "vor Zug 20"), new(null, 12)),
        new("zuruecknahme", SpruchEreignis.Zuruecknahme, D(SpruchEreignis.Zuruecknahme, 9, "Eröffnung", null, null), new(null, 9)),
        new("figur-gewonnen-dame", SpruchEreignis.FigurGewonnen, D(SpruchEreignis.FigurGewonnen, 18, "Mittelspiel", "Dd4", "Sxd4", aenderung: 9, bilanz: 9, merkmale: ["Schlagzug"]), new('q', 18)),
        new("figur-verloren-turm", SpruchEreignis.FigurVerloren, D(SpruchEreignis.FigurVerloren, 22, "Mittelspiel", "Lxa8", "Sc6", aenderung: -5, bilanz: -3), new('r', 22)),
        new("schach-gegeben", SpruchEreignis.SchachGegeben, D(SpruchEreignis.SchachGegeben, 15, "Mittelspiel", "h3", "Lb4+", true, merkmale: ["Schach", "Königszone"]), new(null, 15)),
        new("schach-erhalten", SpruchEreignis.SchachErhalten, D(SpruchEreignis.SchachErhalten, 16, "Mittelspiel", "Db5+", "c6"), new(null, 16)),
        new("stellung-besser", SpruchEreignis.StellungBesser, D(SpruchEreignis.StellungBesser, 20, "Mittelspiel", "g4", "Sf4", stellung: "Gegner steht klar besser", merkmale: ["Entwicklung"]), new(null, 20)),
        new("stellung-schlechter", SpruchEreignis.StellungSchlechter, D(SpruchEreignis.StellungSchlechter, 24, "Mittelspiel", "Sd5", "Kh8", stellung: "Spieler steht klar besser", merkmale: ["Rückzug"]), new(null, 24)),
        new("tausch", SpruchEreignis.Tausch, D(SpruchEreignis.Tausch, 11, "Eröffnung", "Lxf6", "Dxf6", merkmale: ["Schlagzug", "Tausch"]), new('b', 11)),
        new("zug-eroeffnung", SpruchEreignis.Zug, D(SpruchEreignis.Zug, 4, "Eröffnung", "Sc3", "Sf6", merkmale: ["Entwicklung"]), new(null, 4)),
        new("zug-endspiel", SpruchEreignis.Zug, D(SpruchEreignis.Zug, 41, "Endspiel", "Kd3", "a5", merkmale: ["Ungewöhnlich"]), new(null, 41)),
    ];
}

/// <summary>Eine Zeile der CSV.</summary>
public sealed record ProbeZeile(
    string Gegner, string Szenario, int Durchlauf, long Ms, string Quelle, string Roh, string Text,
    bool RohUeber100, bool Sperrliste, string Fehler);

/// <summary>
/// Fuehrt die Probe aus: 14 Szenarien × 8 Gegner × 2 Durchlaeufe, jeder Aufruf mit dem Zeitlimit
/// aus <see cref="SpruchOptions"/> und durch den <see cref="SpruchPruefer"/>. Ohne Budget und
/// ohne Datenbank: die Probe misst Modell und Prompt, nicht die Leiter.
/// </summary>
public sealed class ProbeLauf(ILlmProvider provider, ErsatzKatalog ersatz, IPersonaQuelle persona, SpruchOptions options, TimeProvider zeit)
{
    public const int Durchlaeufe = 2;

    public async Task<IReadOnlyList<ProbeZeile>> AusfuehrenAsync(CancellationToken cancellationToken = default)
    {
        var zeilen = new List<ProbeZeile>();
        foreach (var key in ErsatzKatalog.ErwarteteGegner)
        {
            var name = Infrastructure.Llm.ErsatzKatalogLader.Name(key);
            for (var i = 0; i < ProbeSzenarien.Alle.Count; i++)
            {
                for (var durchlauf = 1; durchlauf <= Durchlaeufe; durchlauf++)
                {
                    zeilen.Add(await EinAufrufAsync(key, name, ProbeSzenarien.Alle[i], i, durchlauf, cancellationToken).ConfigureAwait(false));
                }
            }
        }

        return zeilen;
    }

    private async Task<ProbeZeile> EinAufrufAsync(string key, string name, ProbeSzenario szenario, int index, int durchlauf, CancellationToken cancellationToken)
    {
        using var zeitlimit = new CancellationTokenSource(TimeSpan.FromMilliseconds(options.ZeitlimitMs), zeit);
        using var verbunden = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, zeitlimit.Token);
        var uhr = Stopwatch.StartNew();
        string roh = "", fehler = "";
        SpruchPruefung? pruefung = null;
        try
        {
            var antwort = await provider.CompleteAsync(
                new LlmRequest(
                    SpruchDienst.PromptDatei,
                    new Dictionary<string, string>
                    {
                        ["name"] = name,
                        ["persona"] = persona.Persona(key),
                        ["daten"] = szenario.Daten.AlsJson(),
                    },
                    options.MaxTokens,
                    options.Temperature),
                verbunden.Token).ConfigureAwait(false);
            roh = antwort.Text;
            pruefung = SpruchPruefer.Pruefe(roh, name);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            fehler = "Zeitlimit";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            fehler = ex is HttpRequestException { StatusCode: { } status } ? $"HTTP {(int)status}" : ex.GetType().Name;
        }

        uhr.Stop();
        var ok = pruefung is { Ok: true };
        var text = ok
            ? pruefung!.Text
            : ersatz.Waehle(key, szenario.Ereignis, durchlauf, index, [], szenario.Werte);
        return new ProbeZeile(
            key, szenario.Name, durchlauf, uhr.ElapsedMilliseconds, ok ? "Mistral" : "Ersatz", roh, text,
            pruefung?.RohLaengeUeber100 ?? false, pruefung?.Grund == SpruchAblehnung.Sperrliste, fehler);
    }
}

/// <summary>Automatische Abnahme (SCHACH-SPEC 6.8).</summary>
public sealed record ProbeAuswertung(int Anzahl, long P95Ms, double Ersatzquote, double RohUeber100Quote, double SperrlistenQuote)
{
    public const long MaxP95Ms = 2000;

    public bool Bestanden =>
        Anzahl > 0 && P95Ms <= MaxP95Ms && Ersatzquote <= 0.10 && RohUeber100Quote <= 0.10 && SperrlistenQuote <= 0.05;

    public static ProbeAuswertung Werte(IReadOnlyList<ProbeZeile> zeilen)
    {
        ArgumentNullException.ThrowIfNull(zeilen);
        if (zeilen.Count == 0)
        {
            return new ProbeAuswertung(0, 0, 0, 0, 0);
        }

        // p95 als „nearest rank“: der Wert an Rang ceil(0,95 · n).
        var sortiert = zeilen.Select(z => z.Ms).Order().ToList();
        var rang = (int)Math.Ceiling(0.95 * sortiert.Count) - 1;
        double n = zeilen.Count;
        return new ProbeAuswertung(
            zeilen.Count,
            sortiert[rang],
            zeilen.Count(z => z.Quelle == "Ersatz") / n,
            zeilen.Count(z => z.RohUeber100) / n,
            zeilen.Count(z => z.Sperrliste) / n);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"{Anzahl} Aufrufe, p95 {P95Ms} ms (≤ {MaxP95Ms}), Ersatz {Ersatzquote:P1} (≤ 10 %), roh > 100 Zeichen {RohUeber100Quote:P1} (≤ 10 %), Sperrliste {SperrlistenQuote:P1} (≤ 5 %): {(Bestanden ? "BESTANDEN" : "NICHT BESTANDEN")}");
}

/// <summary>CSV nach <c>.local/spruch-probe/&lt;datum&gt;.csv</c>, Semikolon, UTF-8.</summary>
public static class ProbeCsv
{
    public const string Kopf = "gegner;szenario;durchlauf;ms;quelle;roh;text;rohUeber100;sperrliste;fehler";

    public static string Pfad(string repoRoot, DateOnly datum) =>
        Path.Combine(repoRoot, ".local", "spruch-probe", datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".csv");

    public static string Schreibe(IEnumerable<ProbeZeile> zeilen)
    {
        ArgumentNullException.ThrowIfNull(zeilen);
        var sb = new StringBuilder().Append(Kopf).Append('\n');
        foreach (var z in zeilen)
        {
            sb.AppendJoin(';',
                Feld(z.Gegner), Feld(z.Szenario), z.Durchlauf.ToString(CultureInfo.InvariantCulture),
                z.Ms.ToString(CultureInfo.InvariantCulture), Feld(z.Quelle), Feld(z.Roh), Feld(z.Text),
                z.RohUeber100 ? "1" : "0", z.Sperrliste ? "1" : "0", Feld(z.Fehler)).Append('\n');
        }

        return sb.ToString();
    }

    private static string Feld(string wert)
    {
        var flach = wert.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ');
        return flach.IndexOfAny([';', '"']) >= 0 ? "\"" + flach.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : flach;
    }
}
