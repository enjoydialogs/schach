using Schach.Application.Abstractions;

namespace Schach.Infrastructure.Time;

/// <summary><see cref="IGameClock"/> ueber den registrierten <see cref="TimeProvider"/>.</summary>
public sealed class SystemGameClock(TimeProvider timeProvider) : IGameClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
