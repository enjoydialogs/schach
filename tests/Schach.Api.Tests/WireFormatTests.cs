using System.Text.Json;
using Schach.Api.Contracts;

namespace Schach.Api.Tests;

/// <summary>Haelt das Wire-Format aus SCHACH-SPEC 8.4a fest: camelCase, Enums als PascalCase-Strings,
/// Zeitpunkte in UTC mit Z.</summary>
public sealed class WireFormatTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    private static readonly GegnerDto Oscar = new(
        "oscar", "Oscar", 800, Stufe.Anfaenger, Persoenlichkeit.Lehrerhaft,
        "Ich erklär dir hinterher gern, warum ich so gezogen hab.", null);

    [Fact]
    public void Enums_sind_PascalCase_Strings()
    {
        var json = JsonSerializer.Serialize(new NeuePartieAnfrage("oscar", FarbeWahl.Zufall, Bedenkzeit.Fischer15Plus10), Options);

        json.Should().Be("""{"gegnerId":"oscar","farbe":"Zufall","bedenkzeit":"Fischer15Plus10"}""");
    }

    [Fact]
    public void Enum_als_Zahl_wird_abgelehnt()
    {
        var act = () => JsonSerializer.Deserialize<NeuePartieAnfrage>("""{"gegnerId":"oscar","farbe":2,"bedenkzeit":0}""", Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Zeitpunkte_sind_UTC_mit_Z_auch_bei_anderem_Offset()
    {
        var zug = new ZugDto(1, Farbe.Weiss, "e2e4", "e4", new DateTimeOffset(2026, 11, 7, 13, 5, 9, 120, TimeSpan.FromHours(1)));

        var json = JsonSerializer.Serialize(zug, Options);

        json.Should().Be("""{"halbzug":1,"seite":"Weiss","uci":"e2e4","san":"e4","gespieltAm":"2026-11-07T12:05:09.120Z"}""");
    }

    [Fact]
    public void Spieler_traegt_erstelltAm_in_UTC_mit_Z()
    {
        var id = Guid.Parse("0b6f2a3c-5d4e-4f10-9a8b-7c6d5e4f3a2b");
        var spieler = new SpielerDto(id, "anna@example.test", 800, new DateTimeOffset(2026, 11, 7, 13, 5, 9, TimeSpan.FromHours(1)));

        var json = JsonSerializer.Serialize(spieler, Options);

        json.Should().Be("""{"id":"0b6f2a3c-5d4e-4f10-9a8b-7c6d5e4f3a2b","email":"anna@example.test","elo":800,"erstelltAm":"2026-11-07T12:05:09.000Z"}""");
    }

    [Fact]
    public void Zeitpunkt_mit_Offset_wird_nach_UTC_gelesen()
    {
        var zug = JsonSerializer.Deserialize<ZugDto>(
            """{"halbzug":1,"seite":"Schwarz","uci":"e7e5","san":"e5","gespieltAm":"2026-11-07T13:05:09+01:00"}""", Options)!;

        zug.GespieltAm.Offset.Should().Be(TimeSpan.Zero);
        zug.GespieltAm.Hour.Should().Be(12);
    }

    [Fact]
    public void Fehlerantwort_mit_offener_Partie()
    {
        var id = Guid.Parse("0b6f2a3c-5d4e-4f10-9a8b-7c6d5e4f3a2b");

        var json = JsonSerializer.Serialize(new FehlerAntwort(FehlerCode.OffenePartie, OffenePartieId: id), Options);

        json.Should().Be("""{"fehler":"OffenePartie","zustand":null,"offenePartieId":"0b6f2a3c-5d4e-4f10-9a8b-7c6d5e4f3a2b"}""");
    }

    [Fact]
    public void Zustand_traegt_alle_Felder_aus_8_4_in_camelCase()
    {
        var zustand = new ZustandDto(
            Guid.Empty, PartieStatus.Offen, Farbe.Weiss, Oscar, Bedenkzeit.Fischer5Plus3, 0,
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", [], ["e2e4"], true, false, null,
            new UhrDto(300_000, 300_000, UhrSeite.Spieler, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
            true, 1, [], null, null, null, null, null);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(zustand, Options));
        var namen = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        namen.Should().Equal(
            "id", "status", "spielerFarbe", "gegner", "bedenkzeit", "halbzug", "fen", "zugfolge", "erlaubteZuege",
            "amZug", "gegnerZugAusstehend", "gegnerZug", "uhr", "zaehltFuerElo", "remisAngebotMoeglichAbZug",
            "sprueche", "ergebnis", "grund", "eloVorher", "eloNachher", "pgn");
        doc.RootElement.GetProperty("uhr").GetProperty("laeuftFuer").GetString().Should().Be("Spieler");
        doc.RootElement.GetProperty("uhr").GetProperty("serverZeit").GetString().Should().Be("1970-01-01T00:00:00.000Z");
        doc.RootElement.GetProperty("gegner").GetProperty("stufe").GetString().Should().Be("Anfaenger");
    }

    [Fact]
    public void Spruch_traegt_stimme_auch_als_null()
    {
        var mit = JsonSerializer.Serialize(new SpruchDto(3, SpruchEreignis.SchachGegeben, "Schach!", "abc_-1"), Options);
        var ohne = JsonSerializer.Serialize(new SpruchDto(0, SpruchEreignis.Partiebeginn, "Na dann!"), Options);

        mit.Should().Be("""{"nachHalbzug":3,"ereignis":"SchachGegeben","text":"Schach!","stimme":"abc_-1"}""");
        ohne.Should().Be("""{"nachHalbzug":0,"ereignis":"Partiebeginn","text":"Na dann!","stimme":null}""");
    }

    [Fact]
    public void TippAntwort_hat_tipp_und_zustand()
    {
        var tipp = new TippDto("c3e5", "c3", "e5", Figur.Springer);

        var json = JsonSerializer.Serialize(tipp, Options);

        json.Should().Be("""{"uci":"c3e5","von":"c3","nach":"e5","figur":"Springer"}""");
        typeof(TippAntwort).GetProperties().Select(p => p.Name).Should().Equal("Tipp", "Zustand");
    }
}
