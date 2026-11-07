using Microsoft.Extensions.Configuration;
using Schach.Api.RateLimiting;

namespace Schach.Api.Tests;

/// <summary><c>ForwardedHeaders__KnownNetworks</c> ist Pflicht (SCHACH-SPEC 11.4). Der Wert steht nur
/// auf dem VPS; ohne ihn sehen die IP-Limiter nur die Adresse von Traefik. Die App warnt beim Start
/// ausserhalb der Entwicklung. Erzwungen wird der Wert in <c>docker-compose.dokploy.yml</c>.</summary>
public sealed class ForwardedHeadersPruefungTests
{
    private static IConfiguration Config(string? netze) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ForwardedHeaders:KnownNetworks"] = netze })
        .Build();

    [Theory]
    [InlineData("Production", null, true)]
    [InlineData("Production", " ", true)]
    [InlineData("Development", null, false)]
    [InlineData("Testing", null, false)]
    public void Warnt_ohne_Vertrauensnetz_ausserhalb_der_Entwicklung(string umgebung, string? netze, bool warnt) =>
        ForwardedHeadersPruefung.FehltVertrauensnetz(Config(netze), umgebung).Should().Be(warnt);

    [Fact]
    public void Gesetztes_Vertrauensnetz_warnt_nicht()
    {
        // Zur Laufzeit gebaut: keine Netze oder IPs als Literal im Repo.
        var netz = new System.Net.IPNetwork(System.Net.IPAddress.Loopback, 8).ToString();

        ForwardedHeadersPruefung.FehltVertrauensnetz(Config(netz), "Production").Should().BeFalse();
    }
}
