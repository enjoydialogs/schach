namespace Schach.Api.Middleware;

/// <summary>
/// Sicherheits-Header jeder Antwort (SCHACH-SPEC 12.3: keine Fremd-Skripte, Fonts selbst gehostet,
/// strikte CSP). <c>script-src</c> ohne <c>'unsafe-inline'</c>: darum registriert die PWA ihren
/// Service Worker manuell aus einer Datei (SCHACH-SPEC 10.13). <c>style-src</c> erlaubt Inline-Stile,
/// weil React-Komponenten (auch react-chessboard) <c>style</c>-Attribute setzen.
/// </summary>
internal static class SecurityHeaders
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; font-src 'self'; connect-src 'self'; manifest-src 'self'; " +
        "worker-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";

    public static IApplicationBuilder UseSchachSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            await next();
        });
}
