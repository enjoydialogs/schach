namespace Schach.Domain;

/// <summary>Ein UCI-Zug ist in der Stellung nicht erlaubt (spaeter 400 <c>ZugUnzulaessig</c>, SCHACH-SPEC 8.5).</summary>
public sealed class ZugUnzulaessigException : Exception
{
    public ZugUnzulaessigException()
    {
    }

    public ZugUnzulaessigException(string message)
        : base(message)
    {
    }

    public ZugUnzulaessigException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ZugUnzulaessigException(string uci, string fen)
        : base($"Zug {uci} ist in {fen} nicht erlaubt.")
    {
        Uci = uci;
        Fen = fen;
    }

    public string? Uci { get; }

    public string? Fen { get; }
}
