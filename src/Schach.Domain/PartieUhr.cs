namespace Schach.Domain;

/// <summary>Bedenkzeit einer Partie (Spec 3.2). Namen wie Wire-Enum <c>bedenkzeit</c> (Spec 8.4a).</summary>
public enum Bedenkzeit
{
    OhneUhr,
    Fischer15Plus10,
    Fischer10Plus5,
    Fischer5Plus3,
    JeZug90s,
}

/// <summary>Für wen die Uhr läuft. Namen wie Wire-Enum <c>uhr.laeuftFuer</c>.</summary>
public enum UhrSeite
{
    Spieler,
    Gegner,
}

/// <summary>Ein Zug kam nach Ablauf der Uhr (API: 409 <c>UhrAbgelaufen</c>).</summary>
public sealed class UhrAbgelaufenException : InvalidOperationException
{
    public UhrAbgelaufenException()
        : base("Die Uhr ist abgelaufen.")
    {
    }

    public UhrAbgelaufenException(string message)
        : base(message)
    {
    }

    public UhrAbgelaufenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Server-Uhr einer Partie mit Bedenkzeit (Spec 8.6). Unveränderlich; jede Operation liefert den
/// neuen Stand. Gespeichert werden <see cref="SpielerRestMs"/>, <see cref="GegnerRestMs"/> und
/// <see cref="AmZugSeit"/>; die Restzeit der laufenden Seite gilt ab <see cref="AmZugSeit"/>.
/// Zeit nur über <see cref="TimeProvider"/>, keine Lag-Kompensation.
/// </summary>
public sealed record PartieUhr(
    Bedenkzeit Bedenkzeit,
    long SpielerRestMs,
    long GegnerRestMs,
    UhrSeite LaeuftFuer,
    DateTimeOffset AmZugSeit)
{
    /// <summary>Startet die Uhr bei Erstellung der Partie; <c>null</c> bei <see cref="Bedenkzeit.OhneUhr"/>.</summary>
    /// <param name="anZug">Wer den ersten Halbzug hat (Weiß).</param>
    public static PartieUhr? Starte(Bedenkzeit bedenkzeit, UhrSeite anZug, TimeProvider zeit)
    {
        ArgumentNullException.ThrowIfNull(zeit);
        if (bedenkzeit == Bedenkzeit.OhneUhr)
        {
            return null;
        }

        var start = StartMs(bedenkzeit);
        return new PartieUhr(bedenkzeit, start, start, anZug, zeit.GetUtcNow());
    }

    /// <summary>Restzeit je Bedenkzeit beim Start.</summary>
    public static long StartMs(Bedenkzeit bedenkzeit) => bedenkzeit switch
    {
        Bedenkzeit.Fischer15Plus10 => 15 * 60_000,
        Bedenkzeit.Fischer10Plus5 => 10 * 60_000,
        Bedenkzeit.Fischer5Plus3 => 5 * 60_000,
        Bedenkzeit.JeZug90s => 90_000,
        _ => throw new ArgumentOutOfRangeException(nameof(bedenkzeit)),
    };

    /// <summary>Fischer-Inkrement je Zug; bei „90 s je Zug“ 0.</summary>
    public static long InkrementMs(Bedenkzeit bedenkzeit) => bedenkzeit switch
    {
        Bedenkzeit.Fischer15Plus10 => 10_000,
        Bedenkzeit.Fischer10Plus5 => 5_000,
        Bedenkzeit.Fischer5Plus3 => 3_000,
        Bedenkzeit.JeZug90s => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(bedenkzeit)),
    };

    /// <summary>Zeitpunkt, zu dem die laufende Seite ohne Zug überzieht.</summary>
    public DateTimeOffset AblaufAm => AmZugSeit.AddMilliseconds(Rest(LaeuftFuer));

    /// <summary>Aktuelle Restzeit einer Seite, nie unter 0.</summary>
    public long RestMs(UhrSeite seite, TimeProvider zeit)
    {
        ArgumentNullException.ThrowIfNull(zeit);
        return seite == LaeuftFuer ? Math.Max(0, Rest(seite) - Verbraucht(zeit.GetUtcNow())) : Rest(seite);
    }

    /// <summary>Wahr, wenn die laufende Seite ihre Zeit verbraucht hat.</summary>
    public bool IstAbgelaufen(TimeProvider zeit)
    {
        ArgumentNullException.ThrowIfNull(zeit);
        return zeit.GetUtcNow() >= AblaufAm;
    }

    /// <summary>Die laufende Seite hat jetzt gezogen: Zeit abziehen, Inkrement bzw. 90-s-Satz, Seite wechseln.</summary>
    /// <exception cref="UhrAbgelaufenException">Der Zug kam nach Ablauf.</exception>
    public PartieUhr ZugGespielt(TimeProvider zeit)
    {
        ArgumentNullException.ThrowIfNull(zeit);
        return Ziehe(zeit.GetUtcNow(), Verbraucht(zeit.GetUtcNow()));
    }

    /// <summary>
    /// Gegnerzug zum Zeitpunkt <paramref name="gespieltAm"/> (siehe <see cref="GegnerzugGespieltAm"/>).
    /// Belastet wird die Zeit von <see cref="AmZugSeit"/> (Empfang des Spielerzugs) bis dahin.
    /// </summary>
    public PartieUhr GegnerzugGespielt(DateTimeOffset gespieltAm)
    {
        PruefeGegnerAmZug();
        return Ziehe(gespieltAm, Verbraucht(gespieltAm));
    }

    /// <summary>Gegnerzug nach Engine-Ausfall (Sweep): belastet wird nur D, die Uhr läuft ab jetzt für den Spieler.</summary>
    public PartieUhr GegnerzugNachEngineAusfall(int denkzeitMs, TimeProvider zeit)
    {
        ArgumentNullException.ThrowIfNull(zeit);
        PruefeGegnerAmZug();
        return Ziehe(zeit.GetUtcNow(), denkzeitMs);
    }

    /// <summary><c>gespieltAm = max(empfangenAm + D, Commitzeit)</c> (Spec 8.6).</summary>
    public static DateTimeOffset GegnerzugGespieltAm(DateTimeOffset empfangenAm, int denkzeitMs, DateTimeOffset commitzeit)
    {
        var mitDenkzeit = empfangenAm.AddMilliseconds(denkzeitMs);
        return mitDenkzeit > commitzeit ? mitDenkzeit : commitzeit;
    }

    private PartieUhr Ziehe(DateTimeOffset jetzt, long verbrauchtMs)
    {
        var rest = Rest(LaeuftFuer) - verbrauchtMs;
        if (rest <= 0)
        {
            throw new UhrAbgelaufenException();
        }

        var neu = Bedenkzeit == Bedenkzeit.JeZug90s ? StartMs(Bedenkzeit) : rest + InkrementMs(Bedenkzeit);
        var naechste = LaeuftFuer == UhrSeite.Spieler ? UhrSeite.Gegner : UhrSeite.Spieler;
        var spieler = LaeuftFuer == UhrSeite.Spieler ? neu : SpielerRestMs;
        var gegner = LaeuftFuer == UhrSeite.Gegner ? neu : GegnerRestMs;
        if (Bedenkzeit == Bedenkzeit.JeZug90s)
        {
            // Rest je Zug auf 90 000 ms, ohne Übertrag: auch die nun laufende Seite beginnt neu.
            spieler = gegner = StartMs(Bedenkzeit);
        }

        return this with { SpielerRestMs = spieler, GegnerRestMs = gegner, LaeuftFuer = naechste, AmZugSeit = jetzt };
    }

    private void PruefeGegnerAmZug()
    {
        if (LaeuftFuer != UhrSeite.Gegner)
        {
            throw new InvalidOperationException("Die Uhr läuft nicht für den Gegner.");
        }
    }

    private long Rest(UhrSeite seite) => seite == UhrSeite.Spieler ? SpielerRestMs : GegnerRestMs;

    private long Verbraucht(DateTimeOffset jetzt) => Math.Max(0, (long)(jetzt - AmZugSeit).TotalMilliseconds);
}
