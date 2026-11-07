using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Schach.Infrastructure.Email;

namespace Schach.Infrastructure.Tests.Email;

/// <summary>Globales Mail-Kontingent je Stunde und Tag (SCHACH-SPEC 11.3), im Speicher des Dienstes.</summary>
public sealed class MailKontingentTests
{
    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));

    private MailKontingent Erzeuge(int jeStunde, int jeTag) =>
        new(_zeit, Options.Create(new MailLimits { MaxJeStunde = jeStunde, MaxJeTag = jeTag }));

    [Fact]
    public void Stundenlimit_greift_und_gibt_nach_einer_Stunde_wieder_frei()
    {
        var kontingent = Erzeuge(jeStunde: 2, jeTag: 100);

        kontingent.TryBuchen().Should().BeTrue();
        kontingent.TryBuchen().Should().BeTrue();
        kontingent.TryBuchen().Should().BeFalse();

        _zeit.Advance(TimeSpan.FromMinutes(59));
        kontingent.TryBuchen().Should().BeFalse();

        _zeit.Advance(TimeSpan.FromMinutes(1));
        kontingent.TryBuchen().Should().BeTrue();
    }

    [Fact]
    public void Tageslimit_greift_ueber_mehrere_Stunden()
    {
        var kontingent = Erzeuge(jeStunde: 2, jeTag: 3);

        kontingent.TryBuchen().Should().BeTrue();
        kontingent.TryBuchen().Should().BeTrue();
        _zeit.Advance(TimeSpan.FromHours(1));
        kontingent.TryBuchen().Should().BeTrue();
        kontingent.TryBuchen().Should().BeFalse();

        _zeit.Advance(TimeSpan.FromHours(23));
        kontingent.TryBuchen().Should().BeTrue();
    }

    [Fact]
    public void Vorgaben_aus_der_Spec()
    {
        var limits = new MailLimits();

        limits.MaxJeStunde.Should().Be(40);
        limits.MaxJeTag.Should().Be(200);
    }
}
