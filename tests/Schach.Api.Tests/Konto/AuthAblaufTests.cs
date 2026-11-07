using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Schach.Api.Contracts;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Persistence;
using Schach.Tests.Shared;

namespace Schach.Api.Tests.Konto;

/// <summary>Der komplette Auth-Ablauf ueber HTTP (SCHACH-SPEC 9, 11.3, 15.3 „Auth-Ablauf komplett in Api.Tests“).</summary>
public sealed class AuthAblaufTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static async Task MitApiAsync(
        Func<SchachApiFactory, Task> test,
        IReadOnlyDictionary<string, string>? einstellungen = null,
        TimeProvider? zeit = null)
    {
        var connectionString = await SharedPostgres.CreateDatabaseAsync("schach_api");
        try
        {
            await using var factory = new SchachApiFactory(connectionString, einstellungen, zeit);
            await test(factory);
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(connectionString);
        }
    }

    internal static async Task<T> MitDbAsync<T>(SchachApiFactory factory, Func<SchachDbContext, Task<T>> arbeit)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await arbeit(scope.ServiceProvider.GetRequiredService<SchachDbContext>());
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Token_Weg_meldet_an_legt_Spieler_an_und_gilt_nur_einmal() => await MitApiAsync(async factory =>
    {
        using var browser = new ApiSitzung(factory);

        var leer = await browser.SitzungAsync();
        leer.Angemeldet.Should().BeFalse();
        leer.CsrfToken.Should().NotBeNullOrEmpty();

        var id = await browser.AnfordernAsync("  Anna@Example.TEST ");
        id.Should().NotBeEmpty();
        var mail = factory.Mails().Should().ContainSingle().Subject;
        mail.To.Should().Be("anna@example.test");
        mail.Subject.Should().Be("Anmelden bei Schach");
        mail.ReplyTo.Should().Be("kontakt@example.test");
        mail.TextBody.Should().Contain("https://schach.enjoy-dialogs.de/anmelden#t=");
        var token = ApiSitzung.TokenAus(mail);

        using var bestaetigt = await browser.BestaetigenAsync(new BestaetigenAnfrage(Token: token));
        bestaetigt.StatusCode.Should().Be(HttpStatusCode.OK);
        var antwort = (await bestaetigt.Content.ReadFromJsonAsync<BestaetigenAntwort>(ApiSitzung.Json, Ct))!;
        antwort.Neu.Should().BeTrue();
        antwort.Spieler.Email.Should().Be("anna@example.test");
        antwort.Spieler.Elo.Should().Be(800);
        var cookie = bestaetigt.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith("schach_sitzung=", StringComparison.Ordinal)).Which;
        cookie.Should().ContainEquivalentOf("httponly").And.ContainEquivalentOf("secure").And.ContainEquivalentOf("samesite=lax").And.ContainEquivalentOf("expires=");

        var sitzung = await browser.SitzungAsync();
        sitzung.Angemeldet.Should().BeTrue();
        sitzung.Spieler.Should().Be(antwort.Spieler);

        using var nochmal = await browser.BestaetigenAsync(new BestaetigenAnfrage(Token: token));
        await SollAbgelehntSein(nochmal);
        (await MitDbAsync(factory, db => db.Set<Anmeldung>().CountAsync(Ct))).Should().Be(0);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Code_Weg_meldet_bestehenden_Spieler_an() => await MitApiAsync(async factory =>
    {
        using var erst = new ApiSitzung(factory);
        var konto = await erst.AnmeldenAsync("bert@example.test");

        using var zweit = new ApiSitzung(factory);
        var id = await zweit.AnfordernAsync("bert@example.test");
        var code = ApiSitzung.CodeAus(factory.Mails()[^1]);

        using var antwort = await zweit.BestaetigenAsync(new BestaetigenAnfrage(AnmeldungId: id, Code: code));
        antwort.StatusCode.Should().Be(HttpStatusCode.OK);
        var bestaetigt = (await antwort.Content.ReadFromJsonAsync<BestaetigenAntwort>(ApiSitzung.Json, Ct))!;
        bestaetigt.Neu.Should().BeFalse();
        bestaetigt.Spieler.Id.Should().Be(konto.Spieler.Id);
        (await zweit.SitzungAsync()).Angemeldet.Should().BeTrue();

        using var nochmal = await zweit.BestaetigenAsync(new BestaetigenAnfrage(AnmeldungId: id, Code: code));
        await SollAbgelehntSein(nochmal);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Nach_fuenf_Fehlversuchen_ist_auch_der_richtige_Code_abgelehnt() => await MitApiAsync(async factory =>
    {
        using var browser = new ApiSitzung(factory);
        var id = await browser.AnfordernAsync("carla@example.test");
        var code = ApiSitzung.CodeAus(factory.Mails()[^1]);
        var falsch = code == "000000" ? "000001" : "000000";

        for (var i = 0; i < 5; i++)
        {
            using var abgelehnt = await browser.BestaetigenAsync(new BestaetigenAnfrage(AnmeldungId: id, Code: falsch));
            await SollAbgelehntSein(abgelehnt);
        }

        using var richtig = await browser.BestaetigenAsync(new BestaetigenAnfrage(AnmeldungId: id, Code: code));
        await SollAbgelehntSein(richtig);
        (await MitDbAsync(factory, db => db.Set<Anmeldung>().CountAsync(Ct))).Should().Be(0);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Sitzung_und_Bestaetigen_tragen_erstelltAm_des_Spielers_in_UTC()
    {
        // Fester Zeitpunkt in der Zukunft, damit das 180-Tage-Cookie im Testclient nicht abgelaufen ist.
        var zeit = new FakeTimeProvider(new DateTimeOffset(2036, 3, 14, 9, 26, 53, TimeSpan.Zero));
        await MitApiAsync(
            async factory =>
            {
                using var browser = new ApiSitzung(factory);
                await browser.AnfordernAsync("frida@example.test");

                using var bestaetigt = await browser.BestaetigenAsync(new BestaetigenAnfrage(Token: ApiSitzung.TokenAus(factory.Mails()[^1])));
                bestaetigt.StatusCode.Should().Be(HttpStatusCode.OK);
                using (var antwort = JsonDocument.Parse(await bestaetigt.Content.ReadAsStringAsync(Ct)))
                {
                    antwort.RootElement.GetProperty("spieler").GetProperty("erstelltAm").GetString().Should().Be("2036-03-14T09:26:53.000Z");
                }

                zeit.Advance(TimeSpan.FromDays(1));
                var json = await browser.Http.GetStringAsync(new Uri("/api/v1/sitzung", UriKind.Relative), Ct);
                using var sitzung = JsonDocument.Parse(json);
                sitzung.RootElement.GetProperty("angemeldet").GetBoolean().Should().BeTrue();
                sitzung.RootElement.GetProperty("spieler").GetProperty("erstelltAm").GetString().Should().Be("2036-03-14T09:26:53.000Z");
            },
            zeit: zeit);
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Nach_15_Minuten_sind_Token_und_Code_abgelehnt()
    {
        var zeit = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await MitApiAsync(
            async factory =>
            {
                using var browser = new ApiSitzung(factory);
                var id = await browser.AnfordernAsync("dora@example.test");
                var mail = factory.Mails()[^1];

                zeit.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

                using var perCode = await browser.BestaetigenAsync(new BestaetigenAnfrage(AnmeldungId: id, Code: ApiSitzung.CodeAus(mail)));
                await SollAbgelehntSein(perCode);
                using var perToken = await browser.BestaetigenAsync(new BestaetigenAnfrage(Token: ApiSitzung.TokenAus(mail)));
                await SollAbgelehntSein(perToken);
            },
            zeit: zeit);
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Unbekanntes_oder_kaputtes_Token_und_leere_Anfrage_sind_422() => await MitApiAsync(async factory =>
    {
        using var browser = new ApiSitzung(factory);

        using var unbekannt = await browser.BestaetigenAsync(new BestaetigenAnfrage(Token: new string('A', 43)));
        await SollAbgelehntSein(unbekannt);
        using var kaputt = await browser.BestaetigenAsync(new BestaetigenAnfrage(Token: "kaputt!"));
        await SollAbgelehntSein(kaputt);
        using var leer = await browser.BestaetigenAsync(new BestaetigenAnfrage());
        await SollAbgelehntSein(leer);
        using var fremdeId = await browser.BestaetigenAsync(new BestaetigenAnfrage(AnmeldungId: Guid.NewGuid(), Code: "123456"));
        await SollAbgelehntSein(fremdeId);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Neue_Anfrage_loescht_die_aeltere_offene_Anmeldung() => await MitApiAsync(async factory =>
    {
        using var browser = new ApiSitzung(factory);
        await browser.AnfordernAsync("emil@example.test");
        var alt = factory.Mails()[^1];
        await browser.AnfordernAsync("emil@example.test");

        (await MitDbAsync(factory, db => db.Set<Anmeldung>().CountAsync(Ct))).Should().Be(1);
        using var altesToken = await browser.BestaetigenAsync(new BestaetigenAnfrage(Token: ApiSitzung.TokenAus(alt)));
        await SollAbgelehntSein(altesToken);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Formfehler_ist_400_EmailUngueltig() => await MitApiAsync(async factory =>
    {
        using var browser = new ApiSitzung(factory);

        using var antwort = await browser.PostAsync("/api/v1/auth/anmeldung", new AnmeldungAnfrage("keine-adresse"));

        antwort.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Fehler(antwort)).Should().Be(FehlerCode.EmailUngueltig);
        factory.Mails().Should().BeEmpty();
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Versandfehler_ist_503_und_hinterlaesst_keine_Zeilen() => await MitApiAsync(
        async factory =>
        {
            using var browser = new ApiSitzung(factory);

            using var antwort = await browser.PostAsync("/api/v1/auth/anmeldung", new AnmeldungAnfrage("fritz@example.test"));

            antwort.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await Fehler(antwort)).Should().Be(FehlerCode.MailNichtGesendet);
            (await MitDbAsync(factory, db => db.Set<Anmeldung>().CountAsync(Ct))).Should().Be(0);
            (await MitDbAsync(factory, db => db.Set<AnmeldungVersuch>().CountAsync(Ct))).Should().Be(0);
        },
        new Dictionary<string, string> { ["Email:FileDirectory"] = "" });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Ueber_dem_Limit_je_Adresse_kommt_202_ohne_Mail() => await MitApiAsync(
        async factory =>
        {
            using var browser = new ApiSitzung(factory);
            for (var i = 0; i < 4; i++)
            {
                (await browser.AnfordernAsync(i % 2 == 0 ? "gina@example.test" : "Gina+spiel@example.test")).Should().NotBeEmpty();
            }

            factory.Mails().Should().HaveCount(3);
            var versuche = await MitDbAsync(factory, db => db.Set<AnmeldungVersuch>().ToListAsync(Ct));
            versuche.Should().HaveCount(3);
            versuche.Select(v => Convert.ToHexString(v.EmailHash)).Distinct().Should().ContainSingle();
        },
        new Dictionary<string, string> { ["Limits:AnmeldungenJeAdresseUnd15Minuten"] = "3" });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Ueber_dem_IP_Limit_kommt_429() => await MitApiAsync(
        async factory =>
        {
            using var browser = new ApiSitzung(factory);
            await browser.AnfordernAsync("hans1@example.test");
            await browser.AnfordernAsync("hans2@example.test");

            using var dritte = await browser.PostAsync("/api/v1/auth/anmeldung", new AnmeldungAnfrage("hans3@example.test"));

            dritte.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        },
        new Dictionary<string, string> { ["Limits:AnmeldungenJeIpUnd10Minuten"] = "2" });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Ueber_dem_globalen_Kontingent_kommt_503_MailKontingentErschoepft() => await MitApiAsync(
        async factory =>
        {
            using var browser = new ApiSitzung(factory);
            await browser.AnfordernAsync("ida1@example.test");

            using var zweite = await browser.PostAsync("/api/v1/auth/anmeldung", new AnmeldungAnfrage("ida2@example.test"));

            zweite.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await Fehler(zweite)).Should().Be(FehlerCode.MailKontingentErschoepft);
            factory.Mails().Should().ContainSingle();
        },
        new Dictionary<string, string> { ["Mail:MaxJeStunde"] = "1" });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Nicht_GET_ohne_CSRF_Header_ist_400() => await MitApiAsync(async factory =>
    {
        using var http = factory.CreateHttpsClient();
        await http.GetAsync(new Uri("/api/v1/sitzung", UriKind.Relative), Ct);

        using var antwort = await http.PostAsJsonAsync("/api/v1/auth/anmeldung", new AnmeldungAnfrage("jan@example.test"), ApiSitzung.Json, Ct);

        antwort.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        factory.Mails().Should().BeEmpty();
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Abmelden_beendet_die_Sitzung() => await MitApiAsync(async factory =>
    {
        using var browser = new ApiSitzung(factory);
        await browser.AnmeldenAsync("kai@example.test");

        using var antwort = await browser.PostAsync("/api/v1/auth/abmelden", new AbmeldenAnfrage());

        antwort.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await browser.SitzungAsync()).Angemeldet.Should().BeFalse();
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Abmelden_ueberall_entwertet_andere_Sitzungen_ueber_den_Stempel() => await MitApiAsync(async factory =>
    {
        using var handy = new ApiSitzung(factory);
        using var laptop = new ApiSitzung(factory);
        await handy.AnmeldenAsync("lea@example.test");
        await laptop.AnmeldenAsync("lea@example.test");
        (await laptop.SitzungAsync()).Angemeldet.Should().BeTrue();

        using var antwort = await handy.PostAsync("/api/v1/auth/abmelden", new AbmeldenAnfrage(Ueberall: true));

        antwort.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await laptop.SitzungAsync()).Angemeldet.Should().BeFalse();
        using var export = await laptop.Http.GetAsync(new Uri("/api/v1/konto/export", UriKind.Relative), Ct);
        export.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Ohne_Sitzung_ist_Export_401() => await MitApiAsync(async factory =>
    {
        using var http = factory.CreateHttpsClient();

        using var antwort = await http.GetAsync(new Uri("/api/v1/konto/export", UriKind.Relative), Ct);

        antwort.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    });

    internal static async Task SollAbgelehntSein(HttpResponseMessage antwort)
    {
        antwort.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Fehler(antwort)).Should().Be(FehlerCode.AnmeldungUngueltig);
    }

    internal static async Task<FehlerCode> Fehler(HttpResponseMessage antwort) =>
        (await antwort.Content.ReadFromJsonAsync<FehlerAntwort>(ApiSitzung.Json, Ct))!.Fehler;
}
