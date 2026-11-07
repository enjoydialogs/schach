using Schach.Application.Partien;
using Schach.Application.Sprachausgabe;
using Schach.Infrastructure.Identity;

namespace Schach.Api.HostedServices;

/// <summary>
/// Sweep alle 5 s (SCHACH-SPEC 8.7) mit den Bloecken A (Uhren), B (ausstehende Gegnerzuege),
/// C (Gast-Partien nach 24 h) aus <see cref="PartieSweep"/>, D (<see cref="AnmeldungenAufraeumen"/>)
/// und E (Ton-Cache der Sprachausgabe nach 90 Tagen ohne Nutzung, <see cref="SprachausgabeDienst"/>).
/// Jeder Block laeuft in eigenem Scope, ein Fehler stoppt die Schleife nicht. <c>Sweep:Aktiv=false</c>
/// schaltet die Schleife ab (Api.Tests rufen die Bloecke selbst).
/// </summary>
internal sealed partial class SweepDienst(IServiceScopeFactory scopes, TimeProvider zeit, IConfiguration konfiguration, ILogger<SweepDienst> log) : BackgroundService
{
    public static readonly TimeSpan Takt = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!konfiguration.GetValue("Sweep:Aktiv", true))
        {
            return;
        }

        using var timer = new PeriodicTimer(Takt, zeit);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await BlockAsync("A", sp => sp.GetRequiredService<PartieSweep>().UhrenAsync(stoppingToken), stoppingToken);
            await BlockAsync("B", sp => sp.GetRequiredService<PartieSweep>().GegnerzuegeAsync(stoppingToken), stoppingToken);
            await BlockAsync("C", sp => sp.GetRequiredService<PartieSweep>().GastPartienAsync(stoppingToken), stoppingToken);
            await BlockAsync("D", sp => sp.GetRequiredService<AnmeldungenAufraeumen>().AusfuehrenAsync(stoppingToken), stoppingToken);
            await BlockAsync("E", sp => sp.GetRequiredService<SprachausgabeDienst>().AufraeumenAsync(stoppingToken), stoppingToken);
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
