using Schach.Infrastructure.Llm;

namespace Schach.Infrastructure.Tests.Sprueche;

/// <summary>Kronrat-Uebernahme <c>PromptCatalog</c>: Platzhalter in einem Durchlauf.</summary>
public sealed class PromptCatalogTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("schach-prompts-");

    private PromptCatalog Catalog(string vorlage)
    {
        File.WriteAllText(Path.Combine(_dir.FullName, "t.de.md"), vorlage);
        return new PromptCatalog(_dir.FullName);
    }

    [Fact]
    public void Ersetzt_alle_Platzhalter() =>
        Catalog("Hallo {{name}}, {{daten}}.")
            .Render("t.de.md", new Dictionary<string, string> { ["name"] = "Finn", ["daten"] = "{}" })
            .Should().Be("Hallo Finn, {}.");

    [Fact]
    public void Eingesetzte_Werte_werden_nicht_erneut_ersetzt()
    {
        // Injektionshygiene: ein Wert, der wie ein Platzhalter aussieht, bleibt inert.
        var text = Catalog("{{a}} und {{b}}")
            .Render("t.de.md", new Dictionary<string, string> { ["a"] = "{{b}}", ["b"] = "B" });

        text.Should().Be("{{b}} und B");
    }

    [Fact]
    public void Wirft_bei_fehlendem_Wert()
    {
        var act = () => Catalog("{{name}} {{fehlt}}")
            .Render("t.de.md", new Dictionary<string, string> { ["name"] = "Finn" });

        act.Should().Throw<InvalidOperationException>().WithMessage("*{{fehlt}}*");
    }

    [Fact]
    public void Wirft_bei_fehlender_Datei()
    {
        var act = () => new PromptCatalog(_dir.FullName).Render("gibtsnicht.de.md", new Dictionary<string, string>());

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Liest_Dateien_in_Unterordnern() =>
        new PromptCatalog(PromptVerzeichnis.Aufloesen("prompts"))
            .Render("gegner/finn.de.md", new Dictionary<string, string>())
            .Should().Contain("Finn");

    public void Dispose() => _dir.Delete(recursive: true);
}
