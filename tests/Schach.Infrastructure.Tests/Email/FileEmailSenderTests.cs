using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;
using Schach.Infrastructure.Email;

namespace Schach.Infrastructure.Tests.Email;

/// <summary>Testsender fuer Api.Tests und E2E (SCHACH-SPEC 9.3, 15.2): wirft nie, meldet Fehler als <c>false</c>.</summary>
public sealed class FileEmailSenderTests
{
    private static readonly OutboundEmail Mail = new("anna@example.de", "Anmelden bei Schach", "Text", "<p>Html</p>", "kontakt@example.test");

    private static FileEmailSender Sender(string ordner) =>
        new(Options.Create(new EmailOptionen { FileDirectory = ordner }));

    [Fact]
    public async Task Schreibt_die_Mail_als_Json_Datei()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "schach-mail-" + Guid.NewGuid().ToString("N"));
        try
        {
            var ok = await Sender(ordner).SendAsync(Mail, TestContext.Current.CancellationToken);

            ok.Should().BeTrue();
            Directory.GetFiles(ordner, "*.json").Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(ordner, recursive: true);
        }
    }

    [Fact]
    public async Task Ohne_Schreibrecht_liefert_er_false_statt_zu_werfen()
    {
        // Gleiches Bild wie im E2E-Stapel: der Ordner gehoert einem anderen Nutzer. Nur unter Unix
        // per Dateimodus nachstellbar; root ignoriert ihn.
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Dateimodus nur unter Unix.");
        Assert.SkipWhen(Environment.UserName == "root", "root ignoriert den Dateimodus.");
        var gesperrt = Path.Combine(Path.GetTempPath(), "schach-gesperrt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(gesperrt);
#pragma warning disable CA1416 // Oben auf Unix begrenzt.
        File.SetUnixFileMode(gesperrt, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            var ok = await Sender(Path.Combine(gesperrt, "mail")).SendAsync(Mail, TestContext.Current.CancellationToken);

            ok.Should().BeFalse();
        }
        finally
        {
            File.SetUnixFileMode(gesperrt, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
#pragma warning restore CA1416
            Directory.Delete(gesperrt, recursive: true);
        }
    }

    [Fact]
    public async Task Ohne_Ordner_liefert_er_false()
    {
        (await Sender(" ").SendAsync(Mail, TestContext.Current.CancellationToken)).Should().BeFalse();
    }
}
