using System.Runtime.CompilerServices;

namespace Schach.Tests.Shared;

/// <summary>
/// Ein Test, der Docker braucht (Testcontainers). Er laeuft nur mit <c>SCHACH_CONTAINER_TESTS=1</c>,
/// sonst wird er uebersprungen. CI auf <c>ubuntu-latest</c> setzt die Variable; lokal auf den
/// Windows-Rechnern bleibt sie aus. Zusaetzlich mit <c>[Trait("Category", "Container")]</c>
/// markieren, damit ein Filter die Tests auch ganz ausblenden kann.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ContainerFactAttribute : FactAttribute
{
    public const string Variable = "SCHACH_CONTAINER_TESTS";

    public ContainerFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (!Enabled)
        {
            Skip = $"Container-Test: nur mit {Variable}=1 (CI).";
        }
    }

    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable(Variable), "1", StringComparison.Ordinal);
}
