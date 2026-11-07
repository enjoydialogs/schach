namespace Schach.Api.Contracts;

/// <summary>
/// Einheitliche Fehlerantwort (SCHACH-SPEC 8.5). Bei 409 mit Partiebezug traegt sie den aktuellen
/// <see cref="Zustand"/>, den der Client immer uebernimmt. 409 <see cref="FehlerCode.OffenePartie"/>
/// traegt <see cref="OffenePartieId"/>. 400, 422, 429 und 503 tragen nur <see cref="Fehler"/>.
/// Ein 429 setzt zusaetzlich den Header <c>Retry-After</c>.
/// </summary>
public sealed record FehlerAntwort(FehlerCode Fehler, ZustandDto? Zustand = null, Guid? OffenePartieId = null);
