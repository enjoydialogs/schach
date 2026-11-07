using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Schach.Application.Abstractions;
using Schach.Application.Sprueche;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Llm;

namespace Schach.Infrastructure.Tests.Sprueche;

/// <summary>DI-Block <c>AddSprueche</c> und die Ladepruefung beim Start (SCHACH-SPEC 6.5).</summary>
public sealed class SpruecheRegistrierungTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiceProvider Dienste(Dictionary<string, string?>? werte = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(werte ?? []).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System);
        services.AddSprueche(config);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public async Task Ohne_Schluessel_ist_Mistral_aus_und_der_Start_laedt_die_Ersatzsprueche()
    {
        await using var sp = Dienste();

        sp.GetRequiredService<ILlmProviderResolver>().IsOff.Should().BeTrue();
        foreach (var dienst in sp.GetServices<IHostedService>())
        {
            await dienst.StartAsync(Ct);
        }

        var partie = new Partie { Id = Guid.NewGuid(), GegnerId = "konrad", Seed = 3 };
        var spruch = await sp.GetRequiredService<SpruchDienst>().ErzeugeAsync(
            new SpruchAnfrage(partie, "Konrad", SpruchEreignis.Sieg, 40,
                new SpruchDaten("Sieg", 20, "Endspiel", "Weiss", "OhneUhr", null, "Dh7#", true, 0, 5, null, [], "Matt", "trocken", []),
                new ErsatzWerte(null, 20), []),
            Ct);

        spruch.Quelle.Should().Be(SpruchQuelle.Ersatz);
        spruch.Text.Should().NotBeNullOrWhiteSpace();
        partie.MistralAufrufe.Should().Be(0);
    }

    [Fact]
    public async Task Mit_Schluessel_ist_der_Mistral_Anbieter_aktiv()
    {
        await using var sp = Dienste(new() { ["Llm:ApiKey"] = "placeholder" });

        var resolver = sp.GetRequiredService<ILlmProviderResolver>();

        resolver.IsOff.Should().BeFalse();
        resolver.ModelName.Should().Be("ministral-8b-2512");
        resolver.Current.Should().BeOfType<OpenAiCompatibleChatProvider>();
    }

    [Fact]
    public void Optionen_kommen_aus_der_Konfiguration()
    {
        using var sp = Dienste(new() { ["Spruch:ZeitlimitMs"] = "1800", ["Llm:MonthlyBudgetEuroCents"] = "0" });

        sp.GetRequiredService<SpruchOptions>().ZeitlimitMs.Should().Be(1800);
        sp.GetRequiredService<Application.Llm.LlmBudgetOptions>().MonthlyBudgetEuroCents.Should().Be(0);
    }

    [Fact]
    public async Task Start_scheitert_ohne_Ersatzsprueche()
    {
        var leer = Directory.CreateTempSubdirectory("schach-leer-");
        try
        {
            await using var sp = Dienste(new() { ["Llm:PromptsPath"] = leer.FullName });

            var act = async () =>
            {
                foreach (var dienst in sp.GetServices<IHostedService>())
                {
                    await dienst.StartAsync(Ct);
                }
            };

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Ersatzsprueche*");
        }
        finally
        {
            leer.Delete(recursive: true);
        }
    }
}
