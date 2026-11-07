using System.Net;
using Schach.Application.Betrieb;
using Schach.Application.Sprachausgabe;

namespace Schach.Application.Tests.Sprachausgabe;

internal sealed class FakeSprachProvider : ISprachProvider
{
    public bool Verfuegbar { get; set; } = true;

    public List<SprachAnfrage> Anfragen { get; } = [];

    /// <summary>Wirft statt zu antworten, z. B. <see cref="HttpStatusCode.TooManyRequests"/>.</summary>
    public HttpStatusCode? Fehler { get; set; }

    /// <summary>Wartet, bis das Token abbricht (Zeitlimit).</summary>
    public bool Haengt { get; set; }

    /// <summary>Laeuft bei jedem Aufruf, z. B. um die Testuhr ueber das Zeitlimit zu stellen.</summary>
    public Action? BeimAufruf { get; set; }

    public async Task<byte[]> SprichAsync(SprachAnfrage anfrage, CancellationToken cancellationToken = default)
    {
        Anfragen.Add(anfrage);
        BeimAufruf?.Invoke();
        if (Haengt)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        if (Fehler is { } status)
        {
            throw new HttpRequestException("fake", null, status);
        }

        return [.. System.Text.Encoding.UTF8.GetBytes($"mp3:{anfrage.VoiceId}:{anfrage.Text}")];
    }
}

internal sealed class FakeStimmeSpeicher : ISpruchStimmeSpeicher
{
    public Dictionary<string, (byte[] Audio, DateTimeOffset ZuletztGenutztAm)> Eintraege { get; } = [];

    public bool LesenWirft { get; set; }

    public bool SpeichernWirft { get; set; }

    public Task<byte[]?> LeseAsync(byte[] hash, DateTimeOffset jetzt, CancellationToken cancellationToken = default)
    {
        if (LesenWirft)
        {
            return Task.FromException<byte[]?>(new InvalidOperationException("db weg"));
        }

        var key = Convert.ToHexString(hash);
        if (!Eintraege.TryGetValue(key, out var e))
        {
            return Task.FromResult<byte[]?>(null);
        }

        Eintraege[key] = (e.Audio, jetzt);
        return Task.FromResult<byte[]?>(e.Audio);
    }

    public Task SpeichereAsync(byte[] hash, byte[] audio, int zeichen, DateTimeOffset jetzt, CancellationToken cancellationToken = default)
    {
        if (SpeichernWirft)
        {
            return Task.FromException(new InvalidOperationException("db weg"));
        }

        Eintraege.TryAdd(Convert.ToHexString(hash), (audio, jetzt));
        return Task.CompletedTask;
    }

    public Task<int> AufraeumenAsync(DateTimeOffset grenze, CancellationToken cancellationToken = default)
    {
        var alt = Eintraege.Where(e => e.Value.ZuletztGenutztAm < grenze).Select(e => e.Key).ToList();
        alt.ForEach(k => Eintraege.Remove(k));
        return Task.FromResult(alt.Count);
    }
}

internal sealed class FakeSprachVerbrauch : ISprachVerbrauchSpeicher
{
    public Dictionary<DateOnly, long> Zeichen { get; } = [];

    public List<SprachBuchung> Buchungen { get; } = [];

    public Task<long> LeseZeichenAsync(DateOnly monat, CancellationToken cancellationToken = default) =>
        Task.FromResult(Zeichen.GetValueOrDefault(monat));

    public Task BucheAsync(DateOnly monat, SprachBuchung buchung, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buchung);
        Buchungen.Add(buchung);
        Zeichen[monat] = Zeichen.GetValueOrDefault(monat) + buchung.Zeichen;
        return Task.CompletedTask;
    }
}

/// <summary>Siegel ohne Krypto: Praefix plus Base64. Echtheit prueft der Praefix.</summary>
internal sealed class FakeSiegel : IStimmSiegel
{
    private const string Praefix = "ok.";

    public string Versiegeln(string inhalt) => Praefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(inhalt));

    public string? Oeffnen(string token)
    {
        if (!token.StartsWith(Praefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token[Praefix.Length..]));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

internal sealed class FakeWarner : IBetriebsWarner
{
    public List<BetriebsWarnung> Meldungen { get; } = [];

    public bool Melden(BetriebsWarnung art, string? zusatz = null)
    {
        Meldungen.Add(art);
        return true;
    }
}
