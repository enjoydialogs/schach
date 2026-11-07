using System.Diagnostics;
using OpenTelemetry;

namespace Schach.Api.Telemetry;

/// <summary>
/// Maskiert sensible Query-Werte in exportierten Spans (aus Kronrat uebernommen). Der Magic-Link
/// traegt sein Token im Fragment (SCHACH-SPEC 9.5), das nie zum Server kommt; die Maske ist die
/// zweite Linie, falls ein Client es doch als Query schickt. Je nach Instrumentierung steht die
/// Query in <c>url.query</c> (nur Query) oder in <c>url.full</c>, <c>http.url</c>, <c>http.target</c>.
/// </summary>
public sealed class QueryStringScrubbingProcessor : BaseProcessor<Activity>
{
    private static readonly string[] SensitiveKeys = ["t", "token", "code", "email"];
    private static readonly string[] WholeUrlTags = ["url.full", "http.url", "http.target"];
    private const string QueryOnlyTag = "url.query";
    private const string Mask = "***";

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);
        foreach (var tagName in WholeUrlTags)
        {
            ScrubTag(data, tagName, ScrubUrl);
        }

        ScrubTag(data, QueryOnlyTag, ScrubQuery);
    }

    private static void ScrubTag(Activity activity, string tagName, Func<string, string> scrub)
    {
        if (activity.GetTagItem(tagName) is not string value || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var scrubbed = scrub(value);
        if (!string.Equals(scrubbed, value, StringComparison.Ordinal))
        {
            activity.SetTag(tagName, scrubbed);
        }
    }

    /// <summary>Pfad mit Query oder absolute URL: nur der Teil nach dem ersten <c>?</c> wird angefasst.</summary>
    internal static string ScrubUrl(string url)
    {
        var queryIndex = url.IndexOf('?', StringComparison.Ordinal);
        return queryIndex < 0
            ? url
            : string.Concat(url.AsSpan(0, queryIndex + 1), ScrubQuery(url[(queryIndex + 1)..]));
    }

    /// <summary>Reine Query mit oder ohne fuehrendes <c>?</c>. Nicht sensible Paare bleiben byte-gleich.</summary>
    internal static string ScrubQuery(string query)
    {
        var pairs = query.Split('&');
        var changed = false;
        for (var i = 0; i < pairs.Length; i++)
        {
            var pair = pairs[i];
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            var key = pair[..separator].TrimStart('?');
            if (SensitiveKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                pairs[i] = string.Concat(pair.AsSpan(0, separator + 1), Mask);
                changed = true;
            }
        }

        return changed ? string.Join('&', pairs) : query;
    }
}
