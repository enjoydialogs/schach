namespace Schach.Api.Middleware;

/// <summary>Wie das SPA aus wwwroot ausgeliefert wird (aus Kronrat uebernommen): wie lange ein
/// Browser eine Datei behalten darf und welche Pfade die Rueckfallroute NICHT mit <c>index.html</c>
/// beantwortet.
///
/// <para>Zwei Stufen. Unter <c>/assets/</c> schreibt Vite nur Dateien mit Inhaltshash im Namen:
/// die URL wechselt mit dem Inhalt, also ein Jahr <c>immutable</c>. Alles andere liegt unter einer
/// festen URL (<c>index.html</c>, spaeter <c>sw.js</c>, <c>manifest.webmanifest</c>, Icons) und
/// bekommt <c>no-cache</c>: speichern ja, aber vor jeder Nutzung nachfragen. Sonst nimmt ein Browser
/// nach einem Release die alte <c>index.html</c>, die auf nicht mehr vorhandene Bundles zeigt.</para></summary>
internal static class SpaHosting
{
    /// <summary>Fuer jede Datei unter fester URL, auch <c>sw.js</c> und <c>manifest.webmanifest</c> (S3d).</summary>
    public const string Revalidate = "no-cache";

    /// <summary>Nur fuer <c>/assets/</c> (inhaltsadressierte URLs).</summary>
    public const string Immutable = "public, max-age=31536000, immutable";

    /// <summary>Was die Rueckfallroute mit <c>index.html</c> beantwortet: alles ausser <c>api/</c>
    /// (eine fehlende API-Route ist ein 404, kein 200 mit HTML) und <c>assets/</c> (eine fehlende
    /// gehashte Datei ist ein 404, kein HTML unter einer Skript-URL). <c>/healthz</c> und
    /// <c>/readyz</c> sind eigene Endpunkte und gehen der Rueckfallroute vor.</summary>
    public const string FallbackPathRegex = "^(?!api/|assets/).*$";

    /// <summary>Kein <c>:</c> und keine geschweifte Klammer in <see cref="FallbackPathRegex"/>.</summary>
    public const string FallbackRoute = "{*path:regex(" + FallbackPathRegex + ")}";

    /// <summary>Vites <c>build.assetsDir</c>. Einen Ordner <c>public/assets/</c> darf es nicht geben:
    /// Vite kopierte ihn ungehasht unter dieses Praefix, und er bekaeme ein Jahr <c>immutable</c>.</summary>
    private static readonly PathString AssetsPath = new("/assets");

    /// <summary>Eine Instanz fuer <c>UseStaticFiles</c> UND <c>MapFallbackToFile</c>; sonst kaeme
    /// <c>index.html</c> fuer <c>/partie/…</c> ohne Cache-Control.</summary>
    public static StaticFileOptions CreateStaticFileOptions() => new()
    {
        OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl =
            CacheControlFor(ctx.Context.Request.Path, ctx.File.Name),
    };

    /// <summary><paramref name="fileName"/> ist die ausgelieferte Datei, nicht die angefragte URL:
    /// so landen <c>/</c>, <c>/index.html</c> und jeder Treffer der Rueckfallroute im selben Zweig.</summary>
    public static string CacheControlFor(PathString requestPath, string fileName) =>
        string.Equals(fileName, "index.html", StringComparison.OrdinalIgnoreCase) ? Revalidate
        : requestPath.StartsWithSegments(AssetsPath) ? Immutable
        : Revalidate;
}
