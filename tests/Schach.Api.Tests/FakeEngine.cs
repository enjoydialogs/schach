using Schach.Application.Abstractions;
using Schach.Domain;

namespace Schach.Api.Tests;

/// <summary>Wie die Fake-Engine antwortet.</summary>
internal enum FakeEngineModus
{
    /// <summary>Maia-Policy: der gewaehlte Zug mit <c>p = 1</c>, alle anderen erlaubten mit 0.</summary>
    Maia,

    /// <summary>Fairy-Stockfish-Ersatz: nur <c>bestmove</c> (so liefert <c>EngineHttpClient</c> nach zwei Maia-Ausfaellen).</summary>
    Fairy,

    /// <summary>Maia und Fairy fallen aus: <see cref="EngineNichtVerfuegbarException"/>.</summary>
    Ausfall,
}

/// <summary>
/// Engine fuer Api.Tests (SCHACH-SPEC 15.2): deterministisch, ohne Netz. Zieht die Zuege aus
/// <see cref="Vorgabe"/> der Reihe nach, danach den ersten erlaubten Zug (ordinal).
/// </summary>
internal sealed class FakeEngine : IEngineClient
{
    private readonly Queue<string> _vorgabe = new();
    private readonly Lock _sperre = new();

    public FakeEngineModus Modus { get; set; } = FakeEngineModus.Maia;

    public int Aufrufe { get; private set; }

    public void Vorgabe(params string[] zuege)
    {
        lock (_sperre)
        {
            foreach (var zug in zuege)
            {
                _vorgabe.Enqueue(zug);
            }
        }
    }

    public Task<EngineMoveResponse> MoveAsync(EngineMoveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_sperre)
        {
            Aufrufe++;
            if (Modus == FakeEngineModus.Ausfall)
            {
                throw new EngineNichtVerfuegbarException("Fake: Engine aus.");
            }

            var erlaubt = Stellung.AusFen(request.Fen).Ziehe(request.Moves).ErlaubteZuege();
            var zug = _vorgabe.TryDequeue(out var vorgabe) ? vorgabe : erlaubt[0];
            return Task.FromResult(Modus == FakeEngineModus.Fairy
                ? new EngineMoveResponse(EngineArt.Fairy, null, null, zug)
                : new EngineMoveResponse(
                    EngineArt.Maia,
                    [.. erlaubt.Select(u => new EnginePolicyEintrag(u, u == zug ? 1.0 : 0.0))],
                    new EngineWdl(0.5, 0.3, 0.2),
                    null));
        }
    }

    /// <summary>S1d: Bewertung fuer <c>/analyse</c> in Centipawns aus Sicht der Seite am Zug; <c>null</c> heisst 503.</summary>
    public int? AnalyseCp { get; set; } = 0;

    public int AnalyseAufrufe { get; private set; }

    /// <summary>Stockfish-Ersatz: bestmove ist der erste erlaubte Zug (ordinal), Bewertung <see cref="AnalyseCp"/>.</summary>
    public Task<EngineAnalyseResponse> AnalyseAsync(EngineAnalyseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_sperre)
        {
            AnalyseAufrufe++;
            if (Modus == FakeEngineModus.Ausfall || AnalyseCp is not { } cp)
            {
                throw new EngineNichtVerfuegbarException("Fake: keine Analyse.");
            }

            var best = Stellung.AusFen(request.Fen).Ziehe(request.Moves).ErlaubteZuege()[0];
            return Task.FromResult(new EngineAnalyseResponse(best, [new EngineAnalyseZeile(best, cp, null, new EngineWdl(0.4, 0.3, 0.3))]));
        }
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(Modus != FakeEngineModus.Ausfall);
}
