using Schach.Application.Abstractions;
using Schach.Application.Llm;

namespace Schach.Application.Tests.Llm;

internal sealed class FakeVerbrauchSpeicher : ILlmVerbrauchSpeicher
{
    public Dictionary<DateOnly, LlmVerbrauchStand> Staende { get; } = [];

    public List<(DateOnly Monat, LlmBuchung Buchung)> Buchungen { get; } = [];

    public int Lesungen { get; private set; }

    public Task<LlmVerbrauchStand> LeseAsync(DateOnly monat, CancellationToken cancellationToken = default)
    {
        Lesungen++;
        return Task.FromResult(Staende.GetValueOrDefault(monat, new LlmVerbrauchStand(0, 0)));
    }

    public bool BuchungWirft { get; set; }

    public Task BucheAsync(DateOnly monat, LlmBuchung buchung, CancellationToken cancellationToken = default)
    {
        if (BuchungWirft)
        {
            return Task.FromException(new InvalidOperationException("db weg"));
        }

        Buchungen.Add((monat, buchung));
        return Task.CompletedTask;
    }
}

internal sealed class FakeResolver : ILlmProviderResolver
{
    public ILlmProvider Current { get; set; } = null!;

    public ILlmProvider Fallback { get; set; } = null!;

    public bool IsOff { get; set; }

    public string ModelName { get; set; } = "ministral-8b-2512";
}
