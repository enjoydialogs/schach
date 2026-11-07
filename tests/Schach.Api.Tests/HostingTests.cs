using System.Net;
using Schach.Api.Middleware;
using Schach.Tests.Shared;

namespace Schach.Api.Tests;

public sealed class HostingTests
{
    [Fact]
    public void Hashed_Assets_sind_immutable_alles_andere_revalidiert()
    {
        SpaHosting.CacheControlFor("/assets/index-abc123.js", "index-abc123.js").Should().Be(SpaHosting.Immutable);
        SpaHosting.CacheControlFor("/partie/123", "index.html").Should().Be(SpaHosting.Revalidate);
        SpaHosting.CacheControlFor("/sw.js", "sw.js").Should().Be(SpaHosting.Revalidate);
    }

    [Fact]
    public void Csp_erlaubt_keine_Inline_Skripte_und_keine_Fremd_Hosts()
    {
        SecurityHeaders.ContentSecurityPolicy.Should().Contain("script-src 'self';")
            .And.NotContain("unsafe-eval")
            .And.NotContain("http");
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Healthz_antwortet_mit_Sicherheits_Headern_und_unbekannte_Api_Route_ist_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await SharedPostgres.CreateDatabaseAsync("schach_api");
        try
        {
            await using var factory = new SchachApiFactory(connectionString);
            using var client = factory.CreateClient();

            using var health = await client.GetAsync(new Uri("/healthz", UriKind.Relative), ct);
            health.StatusCode.Should().Be(HttpStatusCode.OK);
            health.Headers.GetValues("Content-Security-Policy").Should().ContainSingle(SecurityHeaders.ContentSecurityPolicy);
            health.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");

            using var ready = await client.GetAsync(new Uri("/readyz", UriKind.Relative), ct);
            ready.StatusCode.Should().Be(HttpStatusCode.OK);

            using var missing = await client.GetAsync(new Uri("/api/v1/gibtsnicht", UriKind.Relative), ct);
            missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(connectionString);
        }
    }
}
