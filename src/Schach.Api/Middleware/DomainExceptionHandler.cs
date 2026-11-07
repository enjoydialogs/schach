using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Schach.Api.Contracts;
using Schach.Application.Abstractions;

namespace Schach.Api.Middleware;

/// <summary>
/// Letzte Stufe fuer Ausnahmen, die kein Controller selbst beantwortet (nach Kronrat-Muster).
/// Fachliche Fehler mit Zustand (409 <c>{fehler, zustand}</c>, SCHACH-SPEC 8.5) liefern die
/// Controller selbst als <c>FehlerAntwort</c>, weil nur sie den Zustand kennen. Hier landen:
/// <list type="bullet">
/// <item>Concurrency-Konflikt (<c>Partie.Version</c>, SCHACH-SPEC 8.8): 409. Der Client laedt neu.</item>
/// <item>Fehlendes Aggregat: 404.</item>
/// <item>Unlesbarer Body: 400.</item>
/// <item>Engine nicht verfuegbar: 503.</item>
/// </list>
/// 409 und 503 tragen <c>FehlerAntwort</c> (<c>fehler</c> ohne Zustand, SCHACH-SPEC 8.5), 404 und 400
/// RFC-7807-ProblemDetails. Nie Ausnahmetext (keine Interna, keine Adressen).
/// </summary>
internal sealed class DomainExceptionHandler : IExceptionHandler
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = ApiJson.CreateOptions();

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            BadHttpRequestException => StatusCodes.Status400BadRequest,
            EngineNichtVerfuegbarException => StatusCodes.Status503ServiceUnavailable,
            _ => 0,
        };
        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        if (status is StatusCodes.Status409Conflict or StatusCodes.Status503ServiceUnavailable)
        {
            // SCHACH-SPEC 8.5: der Client entscheidet ueber `fehler`. Den Zustand kennt nur der Controller;
            // ohne ihn laedt der Client per GET neu.
            var code = status == StatusCodes.Status409Conflict
                ? FehlerCode.HalbzugVeraltet
                : httpContext.Request.Path.Value?.TrimEnd('/').EndsWith("/tipp", StringComparison.OrdinalIgnoreCase) == true
                    ? FehlerCode.TippNichtVerfuegbar
                    : FehlerCode.GeradeVielLos;
            await httpContext.Response.WriteAsJsonAsync(new FehlerAntwort(code), Json, cancellationToken);
            return true;
        }

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = status switch
                {
                    StatusCodes.Status409Conflict => "Die Partie wurde inzwischen geändert.",
                    StatusCodes.Status404NotFound => "Nicht gefunden.",
                    StatusCodes.Status400BadRequest => "Ungültige Anfrage.",
                    _ => "Gerade nicht verfügbar.",
                },
            },
            cancellationToken);
        return true;
    }
}
