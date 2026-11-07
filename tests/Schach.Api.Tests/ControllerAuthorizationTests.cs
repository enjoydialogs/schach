using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Schach.Api.Controllers;

namespace Schach.Api.Tests;

/// <summary>
/// Append-Register (SCHACH-SPEC 15.1 Regel 5): jede Controller-Aktion entscheidet sich ausdruecklich
/// fuer <see cref="AuthorizeAttribute"/> oder <see cref="AllowAnonymousAttribute"/>, an der Aktion oder
/// am Controller. Neue Sessions haengen ihre Erwartungen unten an.
/// </summary>
public sealed class ControllerAuthorizationTests
{
    private static readonly Type[] Controller =
        [.. typeof(AuthController).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)];

    private static IEnumerable<MethodInfo> Aktionen(Type controller) =>
        controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    private static bool Anonym(MethodInfo aktion) =>
        aktion.GetCustomAttribute<AllowAnonymousAttribute>() is not null
        || (aktion.GetCustomAttribute<AuthorizeAttribute>() is null && aktion.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>() is not null);

    [Fact]
    public void Jede_Aktion_ist_ausdruecklich_geschuetzt_oder_anonym()
    {
        var offen = Controller.SelectMany(Aktionen)
            .Where(m => m.GetCustomAttribute<AuthorizeAttribute>() is null && m.GetCustomAttribute<AllowAnonymousAttribute>() is null
                && m.DeclaringType!.GetCustomAttribute<AuthorizeAttribute>() is null && m.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>() is null)
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}");

        offen.Should().BeEmpty();
    }

    // --- S2a: Konto und Sitzung ---
    [Theory]
    [InlineData(typeof(AuthController), nameof(AuthController.Anfordern), true)]
    [InlineData(typeof(AuthController), nameof(AuthController.Bestaetigen), true)]
    [InlineData(typeof(AuthController), nameof(AuthController.Sitzung), true)]
    [InlineData(typeof(AuthController), nameof(AuthController.Abmelden), true)]
    [InlineData(typeof(KontoController), nameof(KontoController.Loeschen), false)]
    [InlineData(typeof(KontoController), nameof(KontoController.Export), false)]
    public void Konto_und_Sitzung(Type controller, string aktion, bool anonym)
    {
        Anonym(controller.GetMethod(aktion)!).Should().Be(anonym);
    }

    // --- S1c: Partie-Kern ---
    [Theory]
    [InlineData(typeof(PartienController), nameof(PartienController.Starten), true)]
    [InlineData(typeof(PartienController), nameof(PartienController.Offen), false)]
    [InlineData(typeof(PartienController), nameof(PartienController.Laden), true)]
    [InlineData(typeof(PartienController), nameof(PartienController.Zug), true)]
    [InlineData(typeof(PartienController), nameof(PartienController.Zuruecknehmen), true)]
    [InlineData(typeof(PartienController), nameof(PartienController.Aufgeben), true)]
    [InlineData(typeof(GegnerController), nameof(GegnerController.Alle), true)]
    public void Partie_Kern(Type controller, string aktion, bool anonym)
    {
        Anonym(controller.GetMethod(aktion)!).Should().Be(anonym);
    }

    // --- S1d: Partie-Rand ---
    [Theory]
    [InlineData(typeof(PartienController), nameof(PartienController.RemisAngebot), true)]
    [InlineData(typeof(PartienController), nameof(PartienController.Tipp), true)]
    [InlineData(typeof(PartienController), nameof(PartienController.Pgn), true)]
    [InlineData(typeof(PartienController), nameof(PartienController.Liste), false)]
    public void Partie_Rand(Type controller, string aktion, bool anonym)
    {
        Anonym(controller.GetMethod(aktion)!).Should().Be(anonym);
    }

    // --- #77: Sprachausgabe ---
    [Theory]
    [InlineData(typeof(SprachausgabeController), nameof(SprachausgabeController.Ton), true)]
    public void Sprachausgabe(Type controller, string aktion, bool anonym)
    {
        Anonym(controller.GetMethod(aktion)!).Should().Be(anonym);
    }
}
