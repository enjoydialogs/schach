using Schach.Application.Sprueche;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Llm;

namespace Schach.Infrastructure.Tests.Sprueche;

/// <summary>SCHACH-SPEC 6.5: der Unit-Test ueber alle 8 Ersatzspruch-Dateien im Repo.</summary>
public class ErsatzDateienTests
{
    private static string PromptsDir => PromptVerzeichnis.Aufloesen("prompts");

    [Fact]
    public void Alle_8_Gegner_haben_eine_gueltige_Datei()
    {
        var katalog = ErsatzKatalogLader.Lade(PromptsDir);

        katalog.Gegner.Should().BeEquivalentTo(ErsatzKatalog.ErwarteteGegner);
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void Jede_Datei_besteht_die_Ladepruefung(string key)
    {
        var datei = ErsatzDatei.Parse(File.ReadAllText(Path.Combine(PromptsDir, "ersatz", key + ".de.md")), key);

        ErsatzDatei.Pruefe(datei, ErsatzKatalogLader.Name(key)).Should().BeEmpty();
    }

    [Fact]
    public void Insgesamt_etwa_330_Zeilen()
    {
        var zeilen = ErsatzKatalog.ErwarteteGegner
            .Sum(k => File.ReadAllLines(Path.Combine(PromptsDir, "ersatz", k + ".de.md")).Count(l => l.StartsWith("- ", StringComparison.Ordinal)));

        zeilen.Should().BeInRange(300, 370);
    }

    [Fact]
    public void Jeder_Gegner_liefert_fuer_jedes_Ereignis_einen_Spruch()
    {
        var katalog = ErsatzKatalogLader.Lade(PromptsDir);

        foreach (var key in ErsatzKatalog.ErwarteteGegner)
        {
            foreach (var ereignis in Enum.GetValues<SpruchEreignis>())
            {
                var text = katalog.Waehle(key, ereignis, 1, 1, [], new ErsatzWerte('q', 12));
                SpruchPruefer.Pruefe(text, ErsatzKatalogLader.Name(key)).Ok.Should().BeTrue($"{key}/{ereignis}: {text}");
            }
        }
    }

    [Fact]
    public void Ladepruefung_scheitert_bei_fehlender_Datei()
    {
        var dir = Directory.CreateTempSubdirectory("schach-ersatz-");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir.FullName, "ersatz"));
            File.Copy(Path.Combine(PromptsDir, "ersatz", "finn.de.md"), Path.Combine(dir.FullName, "ersatz", "finn.de.md"));

            var act = () => ErsatzKatalogLader.Lade(dir.FullName);

            act.Should().Throw<InvalidOperationException>().WithMessage("*ilse*");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    public static TheoryData<string> Keys() => [.. ErsatzKatalog.ErwarteteGegner];
}
