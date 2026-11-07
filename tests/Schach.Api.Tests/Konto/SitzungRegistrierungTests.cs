using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Schach.Api.Auth;

namespace Schach.Api.Tests.Konto;

/// <summary>Die globale CSRF-Pruefung (SCHACH-SPEC 9.4) laesst sich ohne Datenbank aufloesen.</summary>
public sealed class SitzungRegistrierungTests
{
    [Fact]
    public void Globaler_Antiforgery_Filter_ist_registriert_und_aufloesbar()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        services.AddSchachSitzung(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<MvcOptions>>().Value.Filters
            .Should().ContainSingle(f => f is AutoValidateAntiforgeryTokenAttribute);
        var filterTyp = typeof(AutoValidateAntiforgeryTokenAttribute).Assembly
            .GetType("Microsoft.AspNetCore.Mvc.ViewFeatures.Filters.AutoValidateAntiforgeryTokenAuthorizationFilter", throwOnError: true)!;
        provider.GetService(filterTyp).Should().NotBeNull();
    }
}
