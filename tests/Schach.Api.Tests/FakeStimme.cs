using System.Text;
using Schach.Application.Sprachausgabe;

namespace Schach.Api.Tests;

/// <summary>Sprachausgabe ohne Netz (#77): liefert „mp3:{voice}:{text}“ und zaehlt die Aufrufe.</summary>
internal sealed class FakeStimme : ISprachProvider
{
    private int _aufrufe;

    public bool Verfuegbar => true;

    public int Aufrufe => Volatile.Read(ref _aufrufe);

    public Task<byte[]> SprichAsync(SprachAnfrage anfrage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        Interlocked.Increment(ref _aufrufe);
        return Task.FromResult(Encoding.UTF8.GetBytes($"mp3:{anfrage.VoiceId}:{anfrage.Text}"));
    }
}
