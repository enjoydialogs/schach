using Npgsql;
using Schach.Infrastructure.Persistence;

namespace Schach.Infrastructure.Tests;

public sealed class SchachConnectionStringTests
{
    [Fact]
    public void Setzt_GSS_Encryption_Mode_Disable_wenn_nicht_angegeben()
    {
        var normalized = SchachConnectionString.Normalize("Host=localhost;Database=schach;Username=schach");

        new NpgsqlConnectionStringBuilder(normalized).GssEncryptionMode.Should().Be(GssEncryptionMode.Disable);
    }

    [Fact]
    public void Laesst_einen_gesetzten_GSS_Modus_stehen()
    {
        var normalized = SchachConnectionString.Normalize("Host=localhost;GSS Encryption Mode=Prefer");

        new NpgsqlConnectionStringBuilder(normalized).GssEncryptionMode.Should().Be(GssEncryptionMode.Prefer);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Fehlende_Verbindung_nennt_die_Variable(string? value)
    {
        var act = () => SchachConnectionString.Normalize(value);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ConnectionStrings__Schach*");
    }
}
