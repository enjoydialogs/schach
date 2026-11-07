namespace Schach.Application.Limits;

/// <summary>
/// Pruefung vor dem Start einer Partie (<c>POST /partien</c>, SCHACH-SPEC 11.2). Der Start-Use-Case
/// (S1c) ruft sie vor jeder Engine-Arbeit auf und antwortet bei einer Ablehnung:
/// <list type="bullet">
/// <item><see cref="PartieStartAblehnung.TageslimitErreicht"/>: 429 <c>TageslimitErreicht</c> mit <c>Retry-After</c>.</item>
/// <item><see cref="PartieStartAblehnung.GeradeVielLos"/>: 503 <c>GeradeVielLos</c> (nur Gaeste).</item>
/// </list>
/// Das IP-Limit fuer Gaeste (<c>gast-partie-je-ip</c>) ist ein Rate-Limiter in <c>Schach.Api</c>.
/// </summary>
public interface IPartieStartLimits
{
    /// <param name="spielerId"><c>null</c> fuer Gaeste.</param>
    /// <returns><c>null</c>, wenn die Partie starten darf.</returns>
    Task<PartieStartAblehnung?> PruefeAsync(Guid? spielerId, CancellationToken cancellationToken = default);
}

public enum PartieStartAblehnung
{
    /// <summary>Spieler hat <c>Limits:PartienJeSpielerUndTag</c> in den letzten 24 h erreicht.</summary>
    TageslimitErreicht,

    /// <summary>Gast: <c>Limits:OffeneGastPartienGesamt</c> erreicht oder Engine-Ueberlastsperre aktiv.</summary>
    GeradeVielLos,
}
