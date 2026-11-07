using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Schach.Api.Contracts;

namespace Schach.Api.Tests.Konto;

/// <summary>
/// Ein Browser gegen die Api: Cookies, und vor jedem Nicht-GET das CSRF-Token aus <c>GET /sitzung</c>
/// als Header <c>X-CSRF-TOKEN</c> (SCHACH-SPEC 9.4). Nach An- und Abmelden holt der Client das Token neu,
/// wie die SPA (SCHACH-SPEC 9.3).
/// </summary>
internal sealed partial class ApiSitzung(SchachApiFactory factory) : IDisposable
{
    public static readonly JsonSerializerOptions Json = ApiJson.CreateOptions();

    public HttpClient Http { get; } = factory.CreateHttpsClient();

    public async Task<SitzungAntwort> SitzungAsync()
    {
        var sitzung = await Http.GetFromJsonAsync<SitzungAntwort>("/api/v1/sitzung", Json, TestContext.Current.CancellationToken);
        Http.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Http.DefaultRequestHeaders.Add("X-CSRF-TOKEN", sitzung!.CsrfToken);
        return sitzung;
    }

    public async Task<HttpResponseMessage> PostAsync(string pfad, object? body)
    {
        await SitzungAsync();
        return await Http.PostAsJsonAsync(pfad, body, Json, TestContext.Current.CancellationToken);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string pfad)
    {
        await SitzungAsync();
        return await Http.DeleteAsync(new Uri(pfad, UriKind.Relative), TestContext.Current.CancellationToken);
    }

    public async Task<Guid> AnfordernAsync(string email)
    {
        using var antwort = await PostAsync("/api/v1/auth/anmeldung", new AnmeldungAnfrage(email));
        antwort.StatusCode.Should().Be(HttpStatusCode.Accepted);
        return (await antwort.Content.ReadFromJsonAsync<AnmeldungAntwort>(Json, TestContext.Current.CancellationToken))!.AnmeldungId;
    }

    public Task<HttpResponseMessage> BestaetigenAsync(BestaetigenAnfrage anfrage) =>
        PostAsync("/api/v1/auth/anmeldung/bestaetigen", anfrage);

    /// <summary>Anfordern und per Token aus der letzten Mail bestaetigen.</summary>
    public async Task<BestaetigenAntwort> AnmeldenAsync(string email)
    {
        await AnfordernAsync(email);
        using var antwort = await BestaetigenAsync(new BestaetigenAnfrage(Token: TokenAus(factory.Mails()[^1])));
        antwort.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await antwort.Content.ReadFromJsonAsync<BestaetigenAntwort>(Json, TestContext.Current.CancellationToken))!;
    }

    public static string TokenAus(Schach.Application.Abstractions.OutboundEmail mail) =>
        LinkMuster().Match(mail.TextBody).Groups["t"].Value;

    public static string CodeAus(Schach.Application.Abstractions.OutboundEmail mail)
    {
        var m = CodeMuster().Match(mail.TextBody);
        return m.Groups["a"].Value + m.Groups["b"].Value;
    }

    public void Dispose() => Http.Dispose();

    [GeneratedRegex(@"/anmelden#t=(?<t>[A-Za-z0-9_-]+)")]
    private static partial Regex LinkMuster();

    [GeneratedRegex(@"Code in der App ein: (?<a>\d{3}) (?<b>\d{3})")]
    private static partial Regex CodeMuster();
}
