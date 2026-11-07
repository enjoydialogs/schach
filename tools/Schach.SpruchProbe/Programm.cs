using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Schach.Application.Abstractions;
using Schach.Application.Sprueche;
using Schach.Infrastructure;

namespace Schach.SpruchProbe;

/// <summary>
/// Spruch-Probe gegen den echten Mistral-Endpunkt (SCHACH-SPEC 6.8). Betreiber-Schritt, nie in
/// Tests oder CI. Exit 0 = automatisch bestanden, 1 = nicht bestanden, 2 = kein Schluessel.
/// </summary>
public static class Programm
{
    public static async Task<int> Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Programm).Assembly, optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var services = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System);
        services.AddSprueche(config);
        await using var sp = services.BuildServiceProvider();

        var resolver = sp.GetRequiredService<ILlmProviderResolver>();
        if (resolver.IsOff)
        {
            await Console.Error.WriteLineAsync("Kein Mistral-Schluessel: Llm:ApiKey per user-secrets oder Llm__ApiKey setzen.").ConfigureAwait(false);
            return 2;
        }

        Console.WriteLine($"Spruch-Probe: {resolver.ModelName}, 224 Aufrufe ...");
        var lauf = new ProbeLauf(
            resolver.Current,
            sp.GetRequiredService<ErsatzKatalog>(),
            sp.GetRequiredService<IPersonaQuelle>(),
            sp.GetRequiredService<SpruchOptions>(),
            TimeProvider.System);
        var zeilen = await lauf.AusfuehrenAsync().ConfigureAwait(false);

        var pfad = ProbeCsv.Pfad(RepoWurzel(), DateOnly.FromDateTime(DateTime.Now));
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        await File.WriteAllTextAsync(pfad, ProbeCsv.Schreibe(zeilen), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)).ConfigureAwait(false);

        var auswertung = ProbeAuswertung.Werte(zeilen);
        Console.WriteLine(pfad);
        Console.WriteLine(auswertung);
        Console.WriteLine("Danach von Hand: Blindlesung (48 Sprueche, zwei Leser) und Haeufigkeit (10 Testpartien), SCHACH-SPEC 6.8.");
        return auswertung.Bestanden ? 0 : 1;
    }

    /// <summary>Verzeichnis mit <c>Schach.slnx</c> oberhalb des Arbeitsverzeichnisses, sonst das Arbeitsverzeichnis.</summary>
    private static string RepoWurzel()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Schach.slnx")))
            {
                return dir.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
