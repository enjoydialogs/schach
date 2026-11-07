using System.Net;
using System.Text;

namespace Schach.Infrastructure.Tests.Engine;

/// <summary>Spielt den Engine-Dienst ueber eine Folge von Antworten nach. Jede Anfrage nimmt die
/// naechste Antwort; die letzte gilt fuer alle weiteren.</summary>
internal sealed class FakeEngineHandler : HttpMessageHandler
{
    private readonly List<Func<CancellationToken, Task<HttpResponseMessage>>> _antworten = [];

    public List<(HttpMethod Methode, string Pfad, string Body)> Anfragen { get; } = [];

    public FakeEngineHandler Json(HttpStatusCode status, string json)
    {
        _antworten.Add(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }));
        return this;
    }

    public FakeEngineHandler Haengt()
    {
        _antworten.Add(async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("unerreichbar");
        });
        return this;
    }

    public FakeEngineHandler Verbindungsfehler()
    {
        _antworten.Add(_ => throw new HttpRequestException("connection refused"));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        int index;
        lock (Anfragen)
        {
            Anfragen.Add((request.Method, request.RequestUri!.AbsolutePath, body));
            index = Math.Min(Anfragen.Count - 1, _antworten.Count - 1);
        }

        return await _antworten[index](cancellationToken);
    }
}
