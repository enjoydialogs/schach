using Microsoft.Extensions.Options;

namespace Schach.Infrastructure.Email;

/// <summary>
/// Globales Mail-Kontingent je Stunde und Tag (SCHACH-SPEC 11.3), als Speicherzaehler im Prozess
/// (Singleton). Gleitende Fenster ueber die gebuchten Zeitpunkte der letzten 24 h.
/// </summary>
public sealed class MailKontingent(TimeProvider zeit, IOptions<MailLimits> limits)
{
    private readonly Queue<DateTimeOffset> _gebucht = new();
    private readonly Lock _sperre = new();

    /// <summary>Bucht eine Mail, wenn Stunden- und Tageskontingent frei sind.</summary>
    public bool TryBuchen()
    {
        var jetzt = zeit.GetUtcNow();
        lock (_sperre)
        {
            while (_gebucht.Count > 0 && _gebucht.Peek() <= jetzt.AddDays(-1))
            {
                _gebucht.Dequeue();
            }

            var stundeStart = jetzt.AddHours(-1);
            var inDerStunde = _gebucht.Count(t => t > stundeStart);
            if (_gebucht.Count >= limits.Value.MaxJeTag || inDerStunde >= limits.Value.MaxJeStunde)
            {
                return false;
            }

            _gebucht.Enqueue(jetzt);
            return true;
        }
    }
}
