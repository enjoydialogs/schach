using System.Threading.Channels;

namespace Schach.Infrastructure.Betrieb;

/// <summary>Eine Mail an den Betreiber, noch ohne Empfaenger (den setzt <see cref="OperatorMailVersand"/>).</summary>
public sealed record OperatorMail(string Betreff, string Text);

/// <summary>
/// Warteschlange fuer Betreibermails. Gesendet wird im Hintergrund (<see cref="OperatorMailVersand"/>),
/// nie auf dem Request-Pfad. Begrenzt: laeuft sie voll, faellt die neue Mail weg; die Warnzeile im
/// Log steht trotzdem.
/// </summary>
public sealed class OperatorPost
{
    private readonly Channel<OperatorMail> _kanal = Channel.CreateBounded<OperatorMail>(
        new BoundedChannelOptions(32) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public bool Einreihen(OperatorMail mail) => _kanal.Writer.TryWrite(mail);

    public ChannelReader<OperatorMail> Reader => _kanal.Reader;
}
