using System.Security.Cryptography;
using System.Text;
using Schach.Infrastructure.Identity;

namespace Schach.Infrastructure.Tests.Identity;

/// <summary>Formpruefung (SCHACH-SPEC 9.3), Speicherform (8.12) und Zaehl-Hash (11.3).</summary>
public sealed class EmailAdresseTests
{
    [Fact]
    public void Gueltige_Adresse_wird_getrimmt_und_klein_geschrieben()
    {
        EmailAdresse.Normalisiere("  Anna.Beispiel+Schach@Example.DE ").Should().Be("anna.beispiel+schach@example.de");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("keine-adresse")]
    [InlineData("anna@")]
    [InlineData("Anna <anna@example.de>")]
    public void Formfehler_ergeben_null(string? eingabe)
    {
        EmailAdresse.Normalisiere(eingabe).Should().BeNull();
    }

    [Fact]
    public void Mehr_als_254_Zeichen_sind_ein_Formfehler()
    {
        var lang = new string('a', 64) + "@" + string.Join('.', Enumerable.Repeat(new string('b', 60), 4)) + ".de";
        lang.Length.Should().BeGreaterThan(254);

        EmailAdresse.Normalisiere(lang).Should().BeNull();
    }

    [Fact]
    public void ZaehlHash_streicht_das_Plus_Tag_und_ist_SHA256()
    {
        var erwartet = SHA256.HashData(Encoding.UTF8.GetBytes("anna@example.de"));

        EmailAdresse.ZaehlHash("anna+schach@example.de").Should().Equal(erwartet);
        EmailAdresse.ZaehlHash("anna@example.de").Should().Equal(erwartet);
    }
}
