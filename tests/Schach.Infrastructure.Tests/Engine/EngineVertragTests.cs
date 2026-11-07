using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;
using Schach.Infrastructure.Engine;
using Schach.Infrastructure.Telemetry;
using Schach.Tests.Shared;

namespace Schach.Infrastructure.Tests.Engine;

/// <summary>
/// Vertragstest: der echte <see cref="EngineHttpClient"/> gegen den Engine-Stub aus <c>engine/</c>
/// (Fixture-Antworten, SCHACH-SPEC 15.3 Phase 0c). Baut das Image aus <c>engine/Dockerfile</c>.
/// Nur in CI (<see cref="ContainerFactAttribute"/>). Nach S4a laeuft derselbe Test gegen die echten
/// Engines, solange der Stub fuer Tests waehlbar bleibt; die Erwartungen pruefen nur den Vertrag.
/// </summary>
public sealed class EngineVertragTests
{
    private const string Grundstellung = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    /// <summary>Stellung aus <c>engine/API.md</c> (Mattbeispiel): nach 1. f3 e5 2. g4 setzt Dh4 matt.</summary>
    private static readonly string[] NarrenmattZuege = ["f2f3", "e7e5", "g2g4"];

    private static readonly SemaphoreSlim BauGate = new(1, 1);
    private static IFutureDockerImage? _image;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Stub_erfuellt_den_Vertrag_fuer_move_analyse_und_healthz()
    {
        await using var engine = await StarteAsync(rollen: null);
        using var messwerte = new Messwerte();
        var client = Client(engine, messwerte);

        (await client.IsHealthyAsync(Ct)).Should().BeTrue();

        var maia = await client.MoveAsync(new EngineMoveRequest(Grundstellung, [], 1000, 800, EngineArt.Maia), Ct);
        maia.Engine.Should().Be(EngineArt.Maia);
        maia.Bestmove.Should().BeNull();
        maia.Policy.Should().HaveCount(20);
        maia.Policy!.Sum(p => p.P).Should().BeApproximately(1.0, 1e-9);
        maia.Policy.Select(p => p.P).Should().BeInDescendingOrder();
        (maia.Wdl!.W + maia.Wdl.D + maia.Wdl.L).Should().BeApproximately(1.0, 1e-9);

        var fairy = await client.MoveAsync(new EngineMoveRequest(Grundstellung, ["e2e4"], 600, 800, EngineArt.Fairy, 600), Ct);
        fairy.Engine.Should().Be(EngineArt.Fairy);
        fairy.Policy.Should().BeNull();
        fairy.Bestmove.Should().MatchRegex("^[a-h][1-8][a-h][1-8][qrbn]?$");

        var matt = await client.AnalyseAsync(new EngineAnalyseRequest(Grundstellung, NarrenmattZuege, 800, 2), Ct);
        matt.Bestmove.Should().Be("d8h4");
        matt.Lines.Should().HaveCount(2);
        matt.Lines[0].Uci.Should().Be("d8h4");
        matt.Lines[0].Mate.Should().Be(1);
        matt.Lines[0].Cp.Should().BeNull();
        matt.Lines[1].Cp.Should().NotBeNull();

        messwerte.Von(SchachMetrics.EngineFehler).Should().BeEmpty();
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Programmfehler_des_Aufrufers_sind_EngineFehler()
    {
        await using var engine = await StarteAsync(rollen: null);
        using var messwerte = new Messwerte();
        var client = Client(engine, messwerte);

        var ungueltigeFen = () => client.MoveAsync(new EngineMoveRequest("kein fen", [], 1000, 800, EngineArt.Maia), Ct);
        (await ungueltigeFen.Should().ThrowAsync<EngineFehlerException>()).WithMessage("*invalid_fen*");

        var illegal = () => client.AnalyseAsync(new EngineAnalyseRequest(Grundstellung, ["e2e5"], 800, 1), Ct);
        (await illegal.Should().ThrowAsync<EngineFehlerException>()).WithMessage("*illegal_move*");

        var mattStellung = () => client.MoveAsync(new EngineMoveRequest(Grundstellung, [.. NarrenmattZuege, "d8h4"], 1000, 800, EngineArt.Maia), Ct);
        (await mattStellung.Should().ThrowAsync<EngineFehlerException>()).WithMessage("*no_legal_moves*");
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Ohne_Maia_Rolle_springt_Fairy_Stockfish_ein_und_ohne_Stockfish_ist_analyse_nicht_verfuegbar()
    {
        await using var engine = await StarteAsync(rollen: "fairy");
        using var messwerte = new Messwerte();
        var client = Client(engine, messwerte);

        var zug = await client.MoveAsync(new EngineMoveRequest(Grundstellung, ["e2e4"], 300, 800, EngineArt.Maia), Ct);

        zug.Engine.Should().Be(EngineArt.Fairy);
        zug.Bestmove.Should().NotBeNullOrEmpty();
        messwerte.Von(SchachMetrics.EngineFehler).Select(t => t["art"]).Should().Equal("Ueberlastet", "Ueberlastet", "FairyErsatz");

        var tipp = () => client.AnalyseAsync(new EngineAnalyseRequest(Grundstellung, [], 800, 1), Ct);
        await tipp.Should().ThrowAsync<EngineNichtVerfuegbarException>();
    }

    private static EngineHttpClient Client(IContainer engine, Messwerte messwerte)
    {
        var options = new EngineClientOptions { BaseUrl = new UriBuilder("http", engine.Hostname, engine.GetMappedPublicPort(8081)).Uri };
        var http = new HttpClient { BaseAddress = options.BaseUrl, Timeout = options.GesamtTimeout };
        var waechter = new EngineUeberlastWaechter(Options.Create(new EngineUeberlastOptions()), TimeProvider.System, NullLogger<EngineUeberlastWaechter>.Instance);
        return new EngineHttpClient(http, Options.Create(options), new SchachZaehler(messwerte.Factory), waechter);
    }

    private static async Task<IContainer> StarteAsync(string? rollen)
    {
        var image = await ImageAsync();
        var builder = new ContainerBuilder(image)
            .WithPortBinding(8081, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8081).ForPath("/healthz")));
        if (rollen is not null)
        {
            builder = builder.WithEnvironment("ENGINE_ROLES", rollen);
        }

        var container = builder.Build();
        await container.StartAsync(Ct);
        return container;
    }

    private static async Task<IFutureDockerImage> ImageAsync()
    {
        await BauGate.WaitAsync(Ct);
        try
        {
            if (_image is null)
            {
                var image = new ImageFromDockerfileBuilder()
                    .WithDockerfileDirectory(Path.Combine(RepoWurzel(), "engine"))
                    .WithDockerfile("Dockerfile")
                    .WithName($"schach-engine-vertrag:{Guid.NewGuid():N}")
                    .WithCleanUp(true)
                    .Build();
                await image.CreateAsync(Ct);
                _image = image;
            }

            return _image;
        }
        finally
        {
            BauGate.Release();
        }
    }

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
