namespace Schach.Application.Abstractions;

/// <summary>
/// Zugang zum Engine-Dienst (<c>engine/</c>, nur HTTP). Der Vertrag steht in <c>engine/API.md</c>
/// und ist ab Phase 0 eingefroren; die DTOs dazu stehen in <c>EngineContracts.cs</c>.
/// Umgesetzt von <c>EngineHttpClient</c> in <c>Schach.Infrastructure/Engine</c> (S2b/S4a).
///
/// Fehlerbild (SCHACH-SPEC 5.2):
/// <list type="bullet">
/// <item>503 des Dienstes (Warteschlange voll, Wartegrenze) oder Zeitueberschreitung:
/// <see cref="EngineNichtVerfuegbarException"/>.</item>
/// <item>Jede andere Abweichung vom Vertrag (400, 500, unlesbare Antwort):
/// <see cref="EngineFehlerException"/>.</item>
/// </list>
/// </summary>
public interface IEngineClient
{
    /// <summary><c>POST /move</c>: Maia-Policy oder Fairy-Stockfish-Zug fuer die Seite am Zug.</summary>
    Task<EngineMoveResponse> MoveAsync(EngineMoveRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>POST /analyse</c>: Stockfish in voller Staerke (Tipp, Remis-Bewertung).</summary>
    Task<EngineAnalyseResponse> AnalyseAsync(EngineAnalyseRequest request, CancellationToken cancellationToken = default);

    /// <summary><c>GET /healthz</c>: wahr erst nach Modell-Laden und Stockfish <c>isready</c>.</summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

/// <summary>Der Dienst ist ueberlastet oder antwortet nicht rechtzeitig (503 oder Timeout).</summary>
public sealed class EngineNichtVerfuegbarException : Exception
{
    public EngineNichtVerfuegbarException()
    {
    }

    public EngineNichtVerfuegbarException(string message)
        : base(message)
    {
    }

    public EngineNichtVerfuegbarException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Der Dienst hat den Vertrag verletzt oder die Anfrage abgelehnt (nicht 503).</summary>
public sealed class EngineFehlerException : Exception
{
    public EngineFehlerException()
    {
    }

    public EngineFehlerException(string message)
        : base(message)
    {
    }

    public EngineFehlerException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
