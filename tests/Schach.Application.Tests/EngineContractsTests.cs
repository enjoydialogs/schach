using System.Text.Json;
using Schach.Application.Abstractions;

namespace Schach.Application.Tests;

/// <summary>
/// Die Engine-DTOs muessen engine/API.md wortgleich treffen (snake_case), auch wenn der Aufrufer
/// Web-Optionen mit camelCase-Policy benutzt.
/// </summary>
public sealed class EngineContractsTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void MoveRequest_schreibt_snake_case_und_lässt_fairy_skill_bei_Maia_weg()
    {
        var request = new EngineMoveRequest(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", ["e2e4"], 1000, 800, EngineArt.Maia);

        var json = JsonSerializer.Serialize(request, WebOptions);

        json.Should().Be(
            """{"fen":"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1","moves":["e2e4"],"self_elo":1000,"oppo_elo":800,"engine":"maia"}""");
    }

    [Fact]
    public void MoveRequest_mit_Fairy_traegt_fairy_skill()
    {
        var request = new EngineMoveRequest("8/8/8/8/8/8/8/K6k w - - 0 1", [], 600, 800, EngineArt.Fairy, 500);

        var json = JsonSerializer.Serialize(request, WebOptions);

        json.Should().Contain("\"engine\":\"fairy\"").And.Contain("\"fairy_skill\":500");
    }

    [Fact]
    public void MoveResponse_aus_API_md_Beispiel_wird_gelesen()
    {
        const string json = """
            {
              "engine": "maia",
              "policy": [ { "uci": "e7e5", "p": 0.412 }, { "uci": "c7c5", "p": 0.188 } ],
              "wdl": { "w": 0.41, "d": 0.18, "l": 0.41 },
              "bestmove": null
            }
            """;

        var response = JsonSerializer.Deserialize<EngineMoveResponse>(json, WebOptions)!;

        response.Engine.Should().Be(EngineArt.Maia);
        response.Policy.Should().HaveCount(2);
        response.Policy![0].Uci.Should().Be("e7e5");
        response.Wdl!.W.Should().Be(0.41);
        response.Bestmove.Should().BeNull();
    }

    [Fact]
    public void AnalyseResponse_mit_Matt_wird_gelesen()
    {
        const string json = """
            { "bestmove": "d8h4", "lines": [ { "uci": "d8h4", "cp": null, "mate": 1, "wdl": { "w": 1.0, "d": 0.0, "l": 0.0 } } ] }
            """;

        var response = JsonSerializer.Deserialize<EngineAnalyseResponse>(json, WebOptions)!;

        response.Bestmove.Should().Be("d8h4");
        response.Lines.Should().ContainSingle().Which.Mate.Should().Be(1);
        response.Lines[0].Cp.Should().BeNull();
    }

    [Fact]
    public void AnalyseRequest_schreibt_movetime_ms_und_multipv()
    {
        var json = JsonSerializer.Serialize(new EngineAnalyseRequest("8/8/8/8/8/8/8/K6k w - - 0 1", [], 800, 3), WebOptions);

        json.Should().Contain("\"movetime_ms\":800").And.Contain("\"multipv\":3");
    }
}
