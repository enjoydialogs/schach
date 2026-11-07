using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Schach.Application.Betrieb;
using Schach.Infrastructure.Betrieb;
using Schach.Infrastructure.Email;
using Schach.Infrastructure.Telemetry;

namespace Schach.Infrastructure.Tests.Betrieb;

/// <summary><c>AddSchachBetrieb</c> stellt Warner, Meter, Mailversand und Tageszeile bereit.</summary>
public sealed class BetriebRegistrierungTests
{
    [Fact]
    public void Registriert_Warner_Meter_und_beide_Hintergrunddienste_mit_der_Betreiberadresse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:OperatorEmail"] = "betreiber@example.org",
                ["Betrieb:WarnFensterMinuten"] = "15",
                ["Betrieb:TageszeileUm"] = "07:30",
            })
            .Build();
        var services = new ServiceCollection().AddLogging();

        services.AddSchachBetrieb(config);
        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<IBetriebsWarner>().Should().BeSameAs(sp.GetRequiredService<BetriebsWarner>());
        sp.GetRequiredService<SchachMeter>().Should().NotBeNull();
        sp.GetServices<IHostedService>().Select(s => s.GetType())
            .Should().BeEquivalentTo([typeof(OperatorMailVersand), typeof(TageszeileDienst)]);
        sp.GetRequiredService<IOptions<EmailOptionen>>().Value.OperatorEmail.Should().Be("betreiber@example.org");
        sp.GetRequiredService<IOptions<BetriebOptions>>().Value.Should().BeEquivalentTo(
            new BetriebOptions { WarnFensterMinuten = 15, TageszeileUm = new TimeOnly(7, 30), Zeitzone = "Europe/Berlin" });
    }
}
