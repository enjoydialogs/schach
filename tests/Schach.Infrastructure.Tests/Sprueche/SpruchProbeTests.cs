using System.Text.Json;
using Schach.Application.Abstractions;
using Schach.Application.Sprueche;
using Schach.Domain;
using Schach.Infrastructure.Llm;
using Schach.SpruchProbe;

namespace Schach.Infrastructure.Tests.Sprueche;

/// <summary>SCHACH-SPEC 6.8: Spruch-Probe ohne echten Mistral-Aufruf.</summary>
public sealed class SpruchProbeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ProbeLauf Lauf(ILlmProvider provider) =>
        new(provider, ErsatzKatalogLader.Lade(PromptVerzeichnis.Aufloesen("prompts")), new FestePersona(), new SpruchOptions(), TimeProvider.System);

    [Fact]
    public void Vierzehn_Szenarien_ergeben_224_Aufrufe()
    {
        ProbeSzenarien.Alle.Should().HaveCount(14);
        ProbeSzenarien.Alle.Select(s => s.Name).Should().OnlyHaveUniqueItems();
        (ProbeSzenarien.Alle.Count * ErsatzKatalog.ErwarteteGegner.Count * ProbeLauf.Durchlaeufe).Should().Be(224);
    }

    [Fact]
    public async Task Lauf_schreibt_eine_Zeile_je_Aufruf()
    {
        var provider = new FakeProvider(_ => "Attacke, mein Springer!");

        var zeilen = await Lauf(provider).AusfuehrenAsync(Ct);

        zeilen.Should().HaveCount(224);
        provider.Aufrufe.Should().Be(224);
        zeilen.Should().OnlyContain(z => z.Quelle == "Mistral" && z.Text == "Attacke, mein Springer!");
        zeilen.Select(z => z.Gegner).Distinct().Should().BeEquivalentTo(ErsatzKatalog.ErwarteteGegner);
    }

    [Fact]
    public async Task Jeder_Aufruf_traegt_Tonregel_und_Laune_seines_Gegners()
    {
        var anfragen = new List<LlmRequest>();
        var provider = new FakeProvider(a =>
        {
            anfragen.Add(a);
            return "Attacke, mein Springer!";
        });

        var zeilen = await Lauf(provider).AusfuehrenAsync(Ct);

        anfragen.Should().HaveCount(zeilen.Count);
        for (var i = 0; i < zeilen.Count; i++)
        {
            var zeile = zeilen[i];
            anfragen[i].Variables["tonregel"].Should().Be("Tonregel " + zeile.Gegner);
            using var daten = JsonDocument.Parse(anfragen[i].Variables["daten"]);
            daten.RootElement.GetProperty("laune").GetString().Should().Be(zeile.Laune);
        }

        // Stufe-0-Gegner bleiben unter vorlaut, Stufe-2-Gegner zeigen auch genervt.
        zeilen.Where(z => GegnerKatalog.Finde(z.Gegner)!.Frechheit == Frechheit.Freundlich)
            .Should().OnlyContain(z => z.Laune == "trocken" || z.Laune == "lustig");
        zeilen.Select(z => z.Laune).Distinct().Should().HaveCountGreaterThan(2);
    }

    [Fact]
    public async Task Abgelehnte_und_fehlerhafte_Antworten_werden_Ersatz()
    {
        var n = 0;
        var provider = new FakeProvider(_ => ++n % 2 == 0 ? "Du solltest rochieren." : throw new HttpRequestException("x"));

        var zeilen = await Lauf(provider).AusfuehrenAsync(Ct);

        zeilen.Should().OnlyContain(z => z.Quelle == "Ersatz" && z.Text.Length > 0);
        zeilen.Count(z => z.Sperrliste).Should().Be(112);
        zeilen.Count(z => z.Fehler.Length > 0).Should().Be(112);
    }

    [Fact]
    public void Auswertung_besteht_innerhalb_der_Grenzen()
    {
        var zeilen = Enumerable.Range(0, 100).Select(i => Zeile(ms: i < 95 ? 500 : 2500)).ToList();

        var auswertung = ProbeAuswertung.Werte(zeilen);

        auswertung.P95Ms.Should().Be(500);
        auswertung.Bestanden.Should().BeTrue();
    }

    [Theory]
    [InlineData(94, 0, 0, 0, false)] // p95 > 2000
    [InlineData(100, 11, 0, 0, false)] // Ersatzquote > 10 %
    [InlineData(100, 10, 0, 0, true)]
    [InlineData(100, 0, 11, 0, false)] // roh > 100 Zeichen > 10 %
    [InlineData(100, 0, 0, 6, false)] // Sperrliste > 5 %
    [InlineData(100, 0, 0, 5, true)]
    public void Auswertung_prueft_die_vier_Grenzen(int schnelle, int ersatz, int lang, int sperr, bool bestanden)
    {
        var zeilen = Enumerable.Range(0, 100).Select(i => Zeile(
            ms: i < schnelle ? 800 : 3000,
            quelle: i < ersatz ? "Ersatz" : "Mistral",
            lang: i < lang,
            sperr: i < sperr)).ToList();

        ProbeAuswertung.Werte(zeilen).Bestanden.Should().Be(bestanden);
    }

    [Fact]
    public void Csv_hat_Kopfzeile_und_maskiert_Felder()
    {
        var csv = ProbeCsv.Schreibe([Zeile(roh: "Ach; \"na\" gut\nzwei")]);

        var zeilen = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        zeilen[0].Should().Be("gegner;szenario;durchlauf;laune;ms;quelle;roh;text;rohUeber100;sperrliste;fehler");
        zeilen[1].Should().StartWith("finn;zug;1;vorlaut;500;");
        csv.Should().Contain("\"Ach; \"\"na\"\" gut zwei\"");
    }

    [Fact]
    public void Ausgabepfad_liegt_unter_local_spruch_probe()
    {
        var pfad = ProbeCsv.Pfad("/repo", new DateOnly(2026, 11, 7));

        pfad.Replace('\\', '/').Should().EndWith("/repo/.local/spruch-probe/2026-11-07.csv");
    }

    private static ProbeZeile Zeile(int ms = 500, string quelle = "Mistral", bool lang = false, bool sperr = false, string roh = "Hallo.") =>
        new("finn", "zug", 1, "vorlaut", ms, quelle, roh, "Hallo.", lang, sperr, "");

    private sealed class FestePersona : IPersonaQuelle
    {
        public string Persona(string gegnerKey) => "Persona " + gegnerKey;

        public string Tonregel(string gegnerKey) => "Tonregel " + gegnerKey;
    }

    private sealed class FakeProvider(Func<LlmRequest, string> antwort) : ILlmProvider
    {
        public int Aufrufe { get; private set; }

        public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            Aufrufe++;
            return Task.FromResult(new LlmCompletion(antwort(request), 400, 20));
        }
    }
}
