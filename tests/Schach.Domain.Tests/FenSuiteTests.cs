using System.Text.Json;
using Chess;

namespace Schach.Domain.Tests;

/// <summary>
/// Haelt das Format der gemeinsamen FEN-Suite fest (tests/fen-suite/README.md): jede Datei ist
/// lesbar, jede Id eindeutig, jede FEN laedt in Gera.Chess, jede Kategorie traegt ihre Felder.
/// Die fachliche Pruefung steht in <see cref="StellungTests"/> (perft, zuege) und
/// <see cref="RemisregelnTests"/> (remis, mattmaterial). <c>ungedeckt</c> prueft der Browser (SCHACH-SPEC 7.2).
/// </summary>
public sealed class FenSuiteTests
{
    private static readonly string SuiteDir = Path.Combine(AppContext.BaseDirectory, "fen-suite");

    private static readonly Dictionary<string, string[]> PflichtfelderJeKategorie = new()
    {
        ["perft"] = ["perft"],
        ["zuege"] = [],
        ["remis"] = ["erwartet"],
        ["mattmaterial"] = ["weissHatMattmaterial", "schwarzHatMattmaterial"],
        ["ungedeckt"] = ["seite", "ungedeckt"],
    };

    private static List<(string Datei, string Kategorie, JsonElement Fall)> LadeFaelle()
    {
        var faelle = new List<(string, string, JsonElement)>();
        foreach (var datei in Directory.GetFiles(SuiteDir, "*.json").Order(StringComparer.Ordinal))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(datei));
            var kategorie = doc.RootElement.GetProperty("kategorie").GetString()!;
            foreach (var fall in doc.RootElement.GetProperty("faelle").EnumerateArray())
            {
                faelle.Add((Path.GetFileName(datei), kategorie, fall.Clone()));
            }
        }

        return faelle;
    }

    [Fact]
    public void Suite_hat_mindestens_vierzig_Faelle_in_allen_fuenf_Kategorien()
    {
        var faelle = LadeFaelle();

        faelle.Should().HaveCountGreaterThanOrEqualTo(40);
        faelle.Select(f => f.Kategorie).Distinct().Should().BeEquivalentTo(PflichtfelderJeKategorie.Keys);
    }

    [Fact]
    public void Dateiname_entspricht_der_Kategorie()
    {
        foreach (var (datei, kategorie, _) in LadeFaelle())
        {
            datei.Should().Be($"{kategorie}.json");
        }
    }

    [Fact]
    public void Ids_sind_eindeutig_und_tragen_das_Kategoriepraefix()
    {
        var faelle = LadeFaelle();
        var ids = faelle.Select(f => f.Fall.GetProperty("id").GetString()).ToList();

        ids.Should().OnlyHaveUniqueItems();
        foreach (var (_, kategorie, fall) in faelle)
        {
            fall.GetProperty("id").GetString().Should().StartWith(kategorie + "-");
        }
    }

    [Fact]
    public void Jeder_Fall_traegt_Beschreibung_und_die_Felder_seiner_Kategorie()
    {
        foreach (var (_, kategorie, fall) in LadeFaelle())
        {
            fall.GetProperty("beschreibung").GetString().Should().NotBeNullOrWhiteSpace();
            foreach (var feld in PflichtfelderJeKategorie[kategorie])
            {
                fall.TryGetProperty(feld, out _).Should().BeTrue($"{fall.GetProperty("id")} braucht {feld}");
            }
        }
    }

    [Fact]
    public void Jede_Fen_hat_sechs_Felder_und_laedt_in_Gera_Chess()
    {
        foreach (var (_, _, fall) in LadeFaelle())
        {
            var fen = fall.GetProperty("fen").GetString()!;
            fen.Split(' ').Should().HaveCount(6, fen);
            ChessBoard.TryLoadFromFen(fen, out var board).Should().BeTrue(fen);
            board.Should().NotBeNull();
        }
    }
}
