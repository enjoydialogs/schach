using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Schach.Application.Limits;
using Schach.Infrastructure.Email;

namespace Schach.Api.Tests;

/// <summary>Betreiber-Einstellungen in <c>docker-compose.dokploy.yml</c> (SCHACH-SPEC 11.2, 11.3, 16).
/// Die Compose-Vorgabe nach <c>:-</c> wiederholt die Vorgabe aus dem Code, weil ein leerer Wert die
/// int-Bindung bricht. Diese Tests halten beide Stellen und den Environment-Tab gleich.</summary>
public sealed partial class BetreiberKonfigTests
{
    private static readonly Dictionary<string, Type> Abschnitte = new(StringComparer.Ordinal)
    {
        [LimitsOptions.Abschnitt] = typeof(LimitsOptions),
        [MailLimits.Abschnitt] = typeof(MailLimits),
        [Schach.Application.Sprachausgabe.SprachausgabeOptions.SectionName] = typeof(Schach.Application.Sprachausgabe.SprachausgabeOptions),
    };

    private sealed record Zuordnung(string Abschnitt, string Eigenschaft, string Variable, string Vorgabe);

    [GeneratedRegex(@"^\s+(?<abschnitt>\w+)__(?<eigenschaft>\w+):\s*\$\{(?<variable>\w+):-(?<vorgabe>[^}]*)\}\s*$")]
    private static partial Regex ZuordnungsZeile();

    private static List<Zuordnung> Zuordnungen()
    {
        var zeilen = File.ReadAllLines(Path.Combine(RepoWurzel(), "docker-compose.dokploy.yml"));
        var ergebnis = new List<Zuordnung>();
        foreach (var zeile in zeilen)
        {
            var treffer = ZuordnungsZeile().Match(zeile);
            if (treffer.Success && Abschnitte.ContainsKey(treffer.Groups["abschnitt"].Value))
            {
                ergebnis.Add(new Zuordnung(
                    treffer.Groups["abschnitt"].Value,
                    treffer.Groups["eigenschaft"].Value,
                    treffer.Groups["variable"].Value,
                    treffer.Groups["vorgabe"].Value));
            }
        }

        return ergebnis;
    }

    [Fact]
    public void Compose_bildet_Mail_MaxJeTag_und_alle_Limits_aus_11_2_ab()
    {
        var schluessel = Zuordnungen().Select(z => $"{z.Abschnitt}:{z.Eigenschaft}").ToList();

        schluessel.Should().OnlyHaveUniqueItems();
        schluessel.Should().Contain(
        [
            "Mail:MaxJeTag",
            "Limits:PartienJeSpielerUndTag",
            "Limits:GastPartienJeIpUndStunde",
            "Limits:OffeneGastPartienGesamt",
            "Limits:ZuegeJePartieUndMinute",
            "Limits:TippsJePartieUndMinute",
            "Limits:AktionenJePartieUndMinute",
            "Limits:EngineZuegeGleichzeitig",
            "Limits:EngineZuegeWarteschlange",
            "Limits:StockfishTippsGleichzeitig",
            "Limits:StockfishTippsWarteschlange",
            "Limits:SprachausgabenJeIpUndMinute",
            "Sprachausgabe:MonthlyBudgetEuroCents",
        ]);
    }

    [Fact]
    public void Jede_Compose_Vorgabe_trifft_eine_Eigenschaft_und_ihre_Code_Vorgabe()
    {
        foreach (var z in Zuordnungen())
        {
            var typ = Abschnitte[z.Abschnitt];
            var eigenschaft = typ.GetProperty(z.Eigenschaft);
            eigenschaft.Should().NotBeNull($"{z.Abschnitt}__{z.Eigenschaft} muss eine Eigenschaft von {typ.Name} sein");
            z.Vorgabe.Should().NotBeEmpty($"{z.Variable} braucht eine Vorgabe, leer bricht die Bindung");

            var codeVorgabe = Convert.ToString(eigenschaft!.GetValue(Activator.CreateInstance(typ)), CultureInfo.InvariantCulture);
            z.Vorgabe.Should().Be(codeVorgabe, $"{z.Variable} muss der Vorgabe in {typ.Name}.{z.Eigenschaft} folgen");

            var gebunden = Binde(typ, z.Abschnitt, z.Eigenschaft, z.Vorgabe);
            Convert.ToString(eigenschaft.GetValue(gebunden), CultureInfo.InvariantCulture).Should().Be(z.Vorgabe);
        }
    }

    [Fact]
    public void Jede_Compose_Variable_steht_im_Environment_Tab_Beispiel()
    {
        var beispiel = File.ReadAllLines(Path.Combine(RepoWurzel(), "deploy", ".env.dokploy.example"));

        foreach (var z in Zuordnungen())
        {
            beispiel.Should().Contain($"{z.Variable}=");
        }
    }

    [Fact]
    public void Leerer_Wert_bricht_die_int_Bindung()
    {
        var binden = () => Binde(typeof(MailLimits), MailLimits.Abschnitt, nameof(MailLimits.MaxJeTag), "");

        binden.Should().Throw<InvalidOperationException>();
    }

    private static object? Binde(Type typ, string abschnitt, string eigenschaft, string wert) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{abschnitt}:{eigenschaft}"] = wert })
            .Build()
            .GetSection(abschnitt)
            .Get(typ);

    private static string RepoWurzel()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Schach.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Schach.slnx nicht gefunden");
    }
}
