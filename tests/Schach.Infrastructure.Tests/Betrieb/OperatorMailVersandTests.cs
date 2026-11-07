using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;
using Schach.Infrastructure.Betrieb;

namespace Schach.Infrastructure.Tests.Betrieb;

/// <summary>Budget- und Warnmails gehen im Hintergrund an <c>Email:OperatorEmail</c> (SCHACH-SPEC 14.2).</summary>
public sealed class OperatorMailVersandTests
{
    private const string Betreiber = "betreiber@example.org";

    private readonly OperatorPost _post = new();
    private readonly ListLogger<OperatorMailVersand> _log = new();

    private OperatorMailVersand Versand(string adresse, IEmailSender? sender)
    {
        var services = new ServiceCollection();
        if (sender is not null)
        {
            services.AddSingleton(sender);
        }

        return new OperatorMailVersand(
            _post,
            services.BuildServiceProvider(),
            Options.Create(new OperatorOptions { Email = adresse }),
            _log);
    }

    [Fact]
    public async Task Eingereihte_Mail_geht_an_den_Betreiber_und_die_Adresse_steht_nie_im_Log()
    {
        var sender = new MerkenderSender();
        var versand = Versand(Betreiber, sender);
        _post.Einreihen(new OperatorMail("Schach: BudgetWarn", "82 %"));

        await versand.StartAsync(TestContext.Current.CancellationToken);
        var mail = await sender.Erste.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await versand.StopAsync(TestContext.Current.CancellationToken);

        mail.To.Should().Be(Betreiber);
        mail.Subject.Should().Be("Schach: BudgetWarn");
        mail.TextBody.Should().Be("82 %");
        _log.Zeilen.Should().NotContain(z => z.Text.Contains(Betreiber, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ohne_Betreiberadresse_wird_nichts_gesendet_nur_geloggt()
    {
        var sender = new MerkenderSender();
        var versand = Versand("", sender);
        _post.Einreihen(new OperatorMail("Schach: EngineUeberlast", "-"));

        await versand.StartAsync(TestContext.Current.CancellationToken);
        await WarteAufLogzeile();
        await versand.StopAsync(TestContext.Current.CancellationToken);

        sender.Anzahl.Should().Be(0);
        _log.Zeilen.Should().ContainSingle().Which.Text.Should().Contain("OperatorEmail");
    }

    [Fact]
    public async Task Ohne_Mail_Dienst_wird_nur_geloggt()
    {
        var versand = Versand(Betreiber, sender: null);
        _post.Einreihen(new OperatorMail("Schach: MailKontingent", "-"));

        await versand.StartAsync(TestContext.Current.CancellationToken);
        await WarteAufLogzeile();
        await versand.StopAsync(TestContext.Current.CancellationToken);

        _log.Zeilen.Should().ContainSingle().Which.Text.Should().NotContain(Betreiber);
    }

    private async Task WarteAufLogzeile()
    {
        for (var i = 0; i < 200 && _log.Zeilen.Count == 0; i++)
        {
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }

    private sealed class MerkenderSender : IEmailSender
    {
        private int _anzahl;

        public TaskCompletionSource<OutboundEmail> Erste { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Anzahl => _anzahl;

        public Task<bool> SendAsync(OutboundEmail email, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _anzahl);
            Erste.TrySetResult(email);
            return Task.FromResult(true);
        }
    }
}
