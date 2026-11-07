namespace Schach.Application.Sprachausgabe;

/// <summary>
/// Sprachausgabe-Anbieter (Mistral Voxtral TTS, SCHACH-SPEC 6.9). Umgesetzt in
/// <c>Schach.Infrastructure</c>. Ein Versuch; das Zeitlimit setzt der Aufrufer ueber das Token.
/// Fehler werfen <see cref="HttpRequestException"/> mit Statuscode.
/// </summary>
public interface ISprachProvider
{
    /// <summary>Falsch ohne Schluessel oder mit <c>Llm:Provider</c> aus: dann keine Stimme.</summary>
    bool Verfuegbar { get; }

    /// <returns>MP3-Bytes.</returns>
    Task<byte[]> SprichAsync(SprachAnfrage anfrage, CancellationToken cancellationToken = default);
}

public sealed record SprachAnfrage(string Model, string VoiceId, double? Expressiveness, string Text);

/// <summary>Ton-Cache <c>SpruchStimme</c>, Schluessel = SHA-256 ueber Modell, Voice-Id und Text.</summary>
public interface ISpruchStimmeSpeicher
{
    /// <summary>Liefert den Ton und setzt <c>ZuletztGenutztAm</c>; <c>null</c> ohne Eintrag.</summary>
    Task<byte[]?> LeseAsync(byte[] hash, DateTimeOffset jetzt, CancellationToken cancellationToken = default);

    /// <summary>Legt an; ein vorhandener Eintrag bleibt.</summary>
    Task SpeichereAsync(byte[] hash, byte[] audio, int zeichen, DateTimeOffset jetzt, CancellationToken cancellationToken = default);

    /// <summary>Loescht Eintraege mit <c>ZuletztGenutztAm</c> vor <paramref name="grenze"/>.</summary>
    Task<int> AufraeumenAsync(DateTimeOffset grenze, CancellationToken cancellationToken = default);
}

/// <summary>Was ein Aufruf zum Monats-Zaehlwerk <c>SprachausgabeVerbrauch</c> beitraegt.</summary>
public sealed record SprachBuchung(long Zeichen, int Aufrufe, int Zeitueberschreitungen, int Fehler);

/// <summary>Monats-Zaehlwerk der Sprachausgabe, atomar hochgezaehlt wie <c>LlmVerbrauch</c>.</summary>
public interface ISprachVerbrauchSpeicher
{
    /// <param name="monat">Erster Tag des Monats (UTC).</param>
    Task<long> LeseZeichenAsync(DateOnly monat, CancellationToken cancellationToken = default);

    Task BucheAsync(DateOnly monat, SprachBuchung buchung, CancellationToken cancellationToken = default);
}

/// <summary>Schuetzt das Stimm-Token (Data Protection). <c>null</c> beim Oeffnen heisst: ungueltig.</summary>
public interface IStimmSiegel
{
    string Versiegeln(string inhalt);

    string? Oeffnen(string token);
}

/// <summary>Erzeugt das Token fuer <c>SpruchDto.stimme</c>. Liest nur Konfiguration, nie die Datenbank.</summary>
public interface ISpruchStimmen
{
    /// <returns><c>null</c>, wenn die Sprachausgabe aus ist oder der Gegner keine Stimme hat.</returns>
    string? Token(string gegnerKey, string text, bool gast);
}
