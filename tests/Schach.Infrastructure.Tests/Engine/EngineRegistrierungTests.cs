using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Schach.Application.Abstractions;
using Schach.Application.Limits;
using Schach.Infrastructure.Engine;

namespace Schach.Infrastructure.Tests.Engine;

/// <summary><c>AddSchachEngine</c> bindet <c>Engine:BaseUrl</c> (Compose: <c>Engine__BaseUrl</c>)
/// und die Limits-Schluessel.</summary>
public sealed class EngineRegistrierungTests
{
    [Fact]
    public async Task IEngineClient_spricht_die_konfigurierte_BaseUrl_an()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Engine:BaseUrl"] = "http://engine:8081",
                ["Engine:UeberlastFehlerIn30s"] = "3",
                ["Limits:OffeneGastPartienGesamt"] = "7",
            })
            .Build();
        var handler = new FakeEngineHandler().Json(HttpStatusCode.OK, """{"status":"ok"}""");
        var services = new ServiceCollection().AddLogging().AddMetrics();
        services.AddSchachEngine(config);
        services.AddHttpClient(EngineHttpClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        var client = provider.GetRequiredService<IEngineClient>();

        (await client.IsHealthyAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        handler.Anfragen.Should().ContainSingle().Which.Pfad.Should().Be("/healthz");
        provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EngineUeberlastOptions>>().Value.UeberlastFehlerIn30s.Should().Be(3);
        provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LimitsOptions>>().Value.OffeneGastPartienGesamt.Should().Be(7);
        provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EngineClientOptions>>().Value.GesamtTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }
}
