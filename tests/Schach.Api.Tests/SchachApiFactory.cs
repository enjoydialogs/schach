using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Schach.Api.Tests;

/// <summary>Die echte Api gegen eine eigene Datenbank im gemeinsamen Postgres-Container.
/// Erweiterungspunkt fuer spaetere Sessions: Fake-Engine und <c>FileEmailSender</c> (SCHACH-SPEC 15.2).</summary>
internal sealed class SchachApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Schach", connectionString);
    }
}
