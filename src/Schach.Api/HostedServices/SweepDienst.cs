using Schach.Infrastructure.Identity;

namespace Schach.Api.HostedServices;

/// <summary>
/// Sweep alle 5 s (SCHACH-SPEC 8.7). S2a bringt nur Block D (<see cref="AnmeldungenAufraeumen"/>);
/// S1d haengt die Bloecke A bis C hier an. Jeder Block laeuft in eigenem Scope, ein Fehler stoppt
/// die Schleife nicht.
/// </summary>
internal sealed partial class SweepDienst(IServiceScopeFactory scopes, TimeProvider zeit, ILogger<SweepDienst> log) : BackgroundService
{
    public static readonly TimeSpan Takt = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Takt, zeit);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await BlockAsync("D", sp => sp.GetRequiredService<AnmeldungenAufraeumen>().AusfuehrenAsync(stoppingToken), stoppingToken);
        }
    }

    private async Task BlockAsync(string name, Func<IServiceProvider, Task> block, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await block(scope.ServiceProvider);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
#pragma warning disable CA1031 // Ein Fehler eines Blocks darf den Sweep nicht beenden.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            BlockFehler(log, name, ex.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "sweep block={Block} fehler={Fehler}")]
    private static partial void BlockFehler(ILogger log, string block, string fehler);
}
