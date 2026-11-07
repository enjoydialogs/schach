namespace Schach.Infrastructure.Llm;

/// <summary>Findet das Verzeichnis <c>prompts/</c> (nach Kronrat <c>PromptCatalog.ResolvePromptsPath</c>).</summary>
public static class PromptVerzeichnis
{
    /// <summary>
    /// Ein absoluter Pfad bleibt, wie er ist. Ein relativer (Vorgabe „prompts“) wird von
    /// <see cref="AppContext.BaseDirectory"/> aufwaerts gesucht: im Image liegt er neben der DLL
    /// (Content-Items des Api-Projekts), in Tests und Entwicklung findet der Aufstieg das Repo.
    /// </summary>
    public static string Aufloesen(string konfiguriert)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(konfiguriert);
        if (Path.IsPathRooted(konfiguriert))
        {
            return konfiguriert;
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var kandidat = Path.Combine(dir.FullName, konfiguriert);
            if (Directory.Exists(kandidat))
            {
                return kandidat;
            }
        }

        return Path.Combine(Directory.GetCurrentDirectory(), konfiguriert);
    }
}
