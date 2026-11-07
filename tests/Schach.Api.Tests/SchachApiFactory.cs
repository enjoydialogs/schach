using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Schach.Application.Abstractions;

namespace Schach.Api.Tests;

/// <summary>Die echte Api gegen eine eigene Datenbank im gemeinsamen Postgres-Container.
/// Mail geht an den <c>FileEmailSender</c> in einen eigenen Ordner (SCHACH-SPEC 15.2); Limits sind
/// hoch gesetzt, ausser ein Test setzt sie per <paramref name="einstellungen"/>. <paramref name="zeit"/>
/// ersetzt den <see cref="TimeProvider"/>.
/// Die Engine ist immer die <see cref="FakeEngine"/> (<see cref="Engine"/>).</summary>
internal sealed class SchachApiFactory(
    string connectionString, IReadOnlyDictionary<string, string>? einstellungen = null, TimeProvider? zeit = null)
    : WebApplicationFactory<Program>
{
    public string MailOrdner { get; } = Path.Combine(Path.GetTempPath(), "schach-mail-" + Guid.NewGuid().ToString("N"));

    /// <summary>S1c: deterministische Engine ohne Netz statt <c>EngineHttpClient</c>.</summary>
    public FakeEngine Engine { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Schach", connectionString);
        builder.UseSetting("Email:Provider", "File");
        builder.UseSetting("Email:FileDirectory", MailOrdner);
        builder.UseSetting("Email:ReplyToAddress", "kontakt@example.test");
        builder.UseSetting("Limits:AnmeldungenJeIpUnd10Minuten", "1000");
        builder.UseSetting("Limits:AnmeldungenJeAdresseUnd15Minuten", "1000");
        builder.UseSetting("Limits:AnmeldungenJeAdresseUnd24Stunden", "1000");
        builder.UseSetting("Mail:MaxJeStunde", "1000");
        builder.UseSetting("Mail:MaxJeTag", "1000");
        foreach (var (k, v) in einstellungen ?? new Dictionary<string, string>())
        {
            builder.UseSetting(k, v);
        }

        builder.ConfigureServices(s => s.AddSingleton<IEngineClient>(Engine));

        if (zeit is not null)
        {
            builder.ConfigureServices(s => s.AddSingleton(zeit));
        }
    }

    /// <summary>Client gegen <c>https://localhost</c>: das Sitzungs-Cookie ist <c>Secure</c>.</summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    /// <summary>Alle bisher „gesendeten“ Mails, aelteste zuerst.</summary>
    public IReadOnlyList<OutboundEmail> Mails() =>
        Directory.Exists(MailOrdner)
            ? [.. Directory.GetFiles(MailOrdner, "*.json").Order(StringComparer.Ordinal)
                .Select(f => JsonSerializer.Deserialize<OutboundEmail>(File.ReadAllText(f))!)]
            : [];

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(MailOrdner))
        {
            Directory.Delete(MailOrdner, recursive: true);
        }
    }
}
