using System.Security.Cryptography;
using System.Text;
using Schach.Infrastructure.Identity;

namespace Schach.Infrastructure.Tests.Identity;

/// <summary>Token und Code nach SCHACH-SPEC 9.2: gespeichert nur Hashes, Vergleich zeitkonstant.</summary>
public sealed class MagicLinkKryptoTests
{
    [Fact]
    public void Token_ist_32_Byte_base64url_und_gespeichert_wird_nur_SHA256()
    {
        var token = MagicLinkKrypto.NeuesToken();

        token.Should().MatchRegex("^[A-Za-z0-9_-]{43}$");
        var erwartet = SHA256.HashData(Base64UrlDecode(token));
        MagicLinkKrypto.TokenHash(token).Should().Equal(erwartet);
    }

    [Fact]
    public void Zwei_Token_sind_verschieden()
    {
        MagicLinkKrypto.NeuesToken().Should().NotBe(MagicLinkKrypto.NeuesToken());
    }

    [Fact]
    public void Kaputtes_Token_ergibt_keinen_Hash()
    {
        MagicLinkKrypto.TokenHash("kein token!").Should().BeNull();
        MagicLinkKrypto.TokenHash("").Should().BeNull();
        MagicLinkKrypto.TokenHash("AAAA").Should().BeNull();
    }

    [Fact]
    public void Code_hat_sechs_Ziffern()
    {
        for (var i = 0; i < 50; i++)
        {
            MagicLinkKrypto.NeuerCode().Should().MatchRegex("^[0-9]{6}$");
        }
    }

    [Fact]
    public void CodeHash_ist_SHA256_aus_Id_Bytes_und_Code()
    {
        var id = Guid.Parse("0b6f2a3c-5d4e-4f10-9a8b-7c6d5e4f3a2b");
        var erwartet = SHA256.HashData([.. id.ToByteArray(), .. Encoding.ASCII.GetBytes("042917")]);

        MagicLinkKrypto.CodeHash(id, "042917").Should().Equal(erwartet);
    }

    [Fact]
    public void CodePasst_nur_bei_gleicher_Id_und_gleichem_Code()
    {
        var id = Guid.NewGuid();
        var hash = MagicLinkKrypto.CodeHash(id, "123456");

        MagicLinkKrypto.CodePasst(id, "123456", hash).Should().BeTrue();
        MagicLinkKrypto.CodePasst(id, "123457", hash).Should().BeFalse();
        MagicLinkKrypto.CodePasst(Guid.NewGuid(), "123456", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    [InlineData("")]
    [InlineData(null)]
    public void Code_mit_Formfehler_passt_nie(string? code)
    {
        MagicLinkKrypto.IstCodeFormat(code).Should().BeFalse();
    }

    [Fact]
    public void Code_mit_Leerzeichen_wie_in_der_Mail_wird_akzeptiert()
    {
        MagicLinkKrypto.NormalisiereCode("123 456").Should().Be("123456");
        MagicLinkKrypto.IstCodeFormat(MagicLinkKrypto.NormalisiereCode("123 456")).Should().BeTrue();
    }

    private static byte[] Base64UrlDecode(string s)
    {
        var b64 = s.Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + ((4 - (b64.Length % 4)) % 4), '=');
        return Convert.FromBase64String(b64);
    }
}
