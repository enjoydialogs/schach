namespace Schach.Domain.Tests;

/// <summary><see cref="Remisregeln"/> nach SCHACH-SPEC 3.4 gegen die FEN-Suite und Einzelfaelle.</summary>
public sealed class RemisregelnTests
{
    public static TheoryData<string> RemisIds => FenSuite.Ids("remis");

    public static TheoryData<string> MattmaterialIds => FenSuite.Ids("mattmaterial");

    [Theory]
    [MemberData(nameof(RemisIds))]
    public void Remis_nach_Pruefreihenfolge_stimmt_mit_der_Suite_ueberein(string id)
    {
        var fall = FenSuite.Fall("remis", id);
        var erwartet = fall.GetProperty("erwartet").GetString();

        var ende = Remisregeln.Pruefe(fall.GetProperty("fen").GetString()!, FenSuite.Zugfolge(fall));

        if (erwartet is null)
        {
            (ende is null || ende.Grund == Grund.Matt).Should().BeTrue($"{id}: kein Remis erwartet, war {ende}");
        }
        else
        {
            ende.Should().Be(new Partieende(Ergebnis.Remis, Enum.Parse<Grund>(erwartet)), id);
        }
    }

    [Theory]
    [MemberData(nameof(MattmaterialIds))]
    public void Mattmaterial_stimmt_mit_der_Suite_ueberein(string id)
    {
        var fall = FenSuite.Fall("mattmaterial", id);
        var stellung = Stellung.AusFen(fall.GetProperty("fen").GetString()!).Ziehe(FenSuite.Zugfolge(fall));

        Remisregeln.HatMattmaterial(stellung, Farbe.Weiss).Should().Be(fall.GetProperty("weissHatMattmaterial").GetBoolean(), id);
        Remisregeln.HatMattmaterial(stellung, Farbe.Schwarz).Should().Be(fall.GetProperty("schwarzHatMattmaterial").GetBoolean(), id);
    }

    [Fact]
    public void Matt_gewinnt_fuer_die_ziehende_Seite()
    {
        Remisregeln.Pruefe(SchachDomain.GrundstellungFen, ["f2f3", "e7e5", "g2g4", "d8h4"])
            .Should().Be(new Partieende(Ergebnis.SchwarzGewinnt, Grund.Matt));
    }

    [Fact]
    public void Matt_im_hundertsten_Halbzug_zaehlt_als_Matt()
    {
        // Halbzugzaehler 99, Weiss setzt mit Tb8 matt: Zaehler wird 100, trotzdem Matt.
        Remisregeln.Pruefe("6k1/5ppp/8/8/8/8/8/1R4K1 w - - 99 80", ["b1b8"])
            .Should().Be(new Partieende(Ergebnis.WeissGewinnt, Grund.Matt));
    }

    [Fact]
    public void Hundertster_Halbzug_ohne_Matt_ist_FuenfzigZuege()
    {
        Remisregeln.Pruefe("6k1/5ppp/8/8/8/8/8/1R4K1 w - - 99 80", ["b1b2"])
            .Should().Be(new Partieende(Ergebnis.Remis, Grund.FuenfzigZuege));
    }

    [Fact]
    public void Patt_geht_vor_totem_Material()
    {
        // Lg3-e5 setzt Schwarz patt; K+L gegen K waere auch totes Material, Patt wird zuerst geprueft.
        Remisregeln.Pruefe("k7/8/1K6/8/8/6B1/8/8 w - - 0 1", ["g3e5"])
            .Should().Be(new Partieende(Ergebnis.Remis, Grund.Patt));
    }

    [Fact]
    public void Ohne_Ende_liefert_null()
    {
        Remisregeln.Pruefe(SchachDomain.GrundstellungFen, ["e2e4", "e7e5"]).Should().BeNull();
    }

    [Fact]
    public void Wiederholungsschluessel_ignoriert_en_passant_ohne_moeglichen_Schlag()
    {
        var mitFeld = Stellung.AusFen("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1");
        var ohneFeld = Stellung.AusFen("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1");

        Remisregeln.Wiederholungsschluessel(mitFeld).Should().Be(Remisregeln.Wiederholungsschluessel(ohneFeld));
    }

    [Fact]
    public void Wiederholungsschluessel_behaelt_en_passant_bei_moeglichem_Schlag()
    {
        var mitFeld = Stellung.AusFen("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1");
        var ohneFeld = Stellung.AusFen("4k3/8/8/3pP3/8/8/8/4K3 w - - 0 1");

        Remisregeln.Wiederholungsschluessel(mitFeld).Should().NotBe(Remisregeln.Wiederholungsschluessel(ohneFeld));
    }

    [Theory]
    [InlineData("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", Farbe.Weiss, Ergebnis.Remis, Grund.ZeitOhneMattmaterial)]
    [InlineData("4k3/8/8/8/8/8/8/R3K3 b - - 0 1", Farbe.Schwarz, Ergebnis.WeissGewinnt, Grund.Zeit)]
    [InlineData("4k3/p7/8/8/8/8/8/4K3 w - - 0 1", Farbe.Weiss, Ergebnis.SchwarzGewinnt, Grund.Zeit)]
    [InlineData("4k3/8/8/8/8/8/8/2B1K3 b - - 0 1", Farbe.Schwarz, Ergebnis.Remis, Grund.ZeitOhneMattmaterial)]
    public void Zeitueberschreitung_ist_Remis_ohne_Mattmaterial_beim_Gegner(string fen, Farbe ueberzogen, Ergebnis ergebnis, Grund grund)
    {
        Remisregeln.Zeitueberschreitung(Stellung.AusFen(fen), ueberzogen).Should().Be(new Partieende(ergebnis, grund));
    }
}
