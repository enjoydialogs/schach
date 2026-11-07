using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Email;
using Schach.Infrastructure.Persistence;

namespace Schach.Infrastructure.Identity;

/// <summary>Ausgang von <see cref="AnmeldeDienst.AnfordernAsync"/>.</summary>
public enum AnfordernErgebnis
{
    /// <summary>202. Auch ueber dem Limit je Adresse (dann ohne Mail).</summary>
    Angenommen,

    /// <summary>400 <c>EmailUngueltig</c>.</summary>
    EmailUngueltig,

    /// <summary>503 <c>MailKontingentErschoepft</c>.</summary>
    KontingentErschoepft,

    /// <summary>503 <c>MailNichtGesendet</c>.</summary>
    MailNichtGesendet,
}

/// <summary>Bestaetigte Anmeldung: der Spieler und ob er gerade angelegt wurde.</summary>
public sealed record Angemeldet(Spieler Spieler, bool Neu);

/// <summary>
/// Magic Link mit Code als zweitem Weg (SCHACH-SPEC 9.2, 9.3, 11.3). Registrierung und Anmeldung
/// sind ein Ablauf; der <see cref="Spieler"/> entsteht erst bei der Bestaetigung. Logs tragen nur
/// <c>anmeldungId</c> und Ergebnis (SCHACH-SPEC 9.6, 14.3).
/// </summary>
public sealed partial class AnmeldeDienst(
    SchachDbContext db,
    IEmailSender mail,
    MailKontingent kontingent,
    TimeProvider zeit,
    IOptions<AnmeldungOptionen> optionen,
    IOptions<EmailOptionen> emailOptionen,
    ILogger<AnmeldeDienst> log)
{
    /// <summary>Legt eine Anmeldung an und schickt die Mail. Die Id gibt es bei jedem 202,
    /// auch ohne Mail, damit die Antwort nichts verraet.</summary>
    public async Task<(AnfordernErgebnis Ergebnis, Guid AnmeldungId)> AnfordernAsync(string? eingabe, CancellationToken ct)
    {
        var anmeldungId = Guid.NewGuid();
        var email = EmailAdresse.Normalisiere(eingabe);
        if (email is null)
        {
            return (AnfordernErgebnis.EmailUngueltig, Guid.Empty);
        }

        var jetzt = zeit.GetUtcNow();
        var hash = EmailAdresse.ZaehlHash(email);
        var o = optionen.Value;
        var versuche = db.Set<AnmeldungVersuch>().Where(v => v.EmailHash == hash);
        var in15 = await versuche.CountAsync(v => v.ErstelltAm > jetzt.AddMinutes(-15), ct);
        var in24 = await versuche.CountAsync(v => v.ErstelltAm > jetzt.AddHours(-24), ct);
        if (in15 >= o.AnmeldungenJeAdresseUnd15Minuten || in24 >= o.AnmeldungenJeAdresseUnd24Stunden)
        {
            Protokoll(log, anmeldungId, "LimitAdresse");
            return (AnfordernErgebnis.Angenommen, anmeldungId);
        }

        if (!kontingent.TryBuchen())
        {
            Protokoll(log, anmeldungId, "KontingentErschoepft");
            return (AnfordernErgebnis.KontingentErschoepft, Guid.Empty);
        }

        var token = MagicLinkKrypto.NeuesToken();
        var code = MagicLinkKrypto.NeuerCode();
        var versuch = new AnmeldungVersuch { Id = Guid.NewGuid(), EmailHash = hash, ErstelltAm = jetzt };

        // Eine neue Anfrage loescht aeltere offene Zeilen derselben Adresse (SCHACH-SPEC 9.2).
        await db.Set<Anmeldung>().Where(a => a.Email == email).ExecuteDeleteAsync(ct);
        db.Set<AnmeldungVersuch>().Add(versuch);
        db.Set<Anmeldung>().Add(new Anmeldung
        {
            Id = anmeldungId,
            Email = email,
            TokenHash = MagicLinkKrypto.TokenHash(token)!,
            CodeHash = MagicLinkKrypto.CodeHash(anmeldungId, code),
            Fehlversuche = 0,
            ErstelltAm = jetzt,
            LaeuftAbAm = jetzt + AnmeldungOptionen.Gueltigkeit,
        });
        await db.SaveChangesAsync(ct);

        var link = $"{o.LinkBasis.TrimEnd('/')}/anmelden#t={token}";
        var gesendet = await mail.SendAsync(AnmeldeMail.Erzeuge(email, link, code, emailOptionen.Value.ReplyToAddress), ct);
        if (!gesendet)
        {
            await db.Set<Anmeldung>().Where(a => a.Id == anmeldungId).ExecuteDeleteAsync(ct);
            await db.Set<AnmeldungVersuch>().Where(v => v.Id == versuch.Id).ExecuteDeleteAsync(ct);
            Protokoll(log, anmeldungId, "MailNichtGesendet");
            return (AnfordernErgebnis.MailNichtGesendet, Guid.Empty);
        }

        Protokoll(log, anmeldungId, "MailGesendet");
        return (AnfordernErgebnis.Angenommen, anmeldungId);
    }

    /// <summary>Bestaetigt per Link-Token: einmalig per <c>DELETE … RETURNING</c>. <c>null</c> = 422.</summary>
    public async Task<Angemeldet?> BestaetigenPerTokenAsync(string? token, CancellationToken ct)
    {
        var hash = MagicLinkKrypto.TokenHash(token);
        if (hash is null)
        {
            return null;
        }

        var email = await LoescheGueltigeMitTokenAsync(hash, zeit.GetUtcNow(), ct);
        return email is null ? null : await SpielerFuerAsync(email, ct);
    }

    /// <summary>Bestaetigt per Code: hoechstens 5 Fehlversuche, dann ist die Zeile weg. <c>null</c> = 422.</summary>
    public async Task<Angemeldet?> BestaetigenPerCodeAsync(Guid anmeldungId, string? eingabe, CancellationToken ct)
    {
        var code = MagicLinkKrypto.NormalisiereCode(eingabe);
        var jetzt = zeit.GetUtcNow();
        var anmeldungen = db.Set<Anmeldung>();
        var zeile = await anmeldungen.AsNoTracking()
            .Where(a => a.Id == anmeldungId && a.LaeuftAbAm > jetzt && a.Fehlversuche < AnmeldungOptionen.MaxFehlversuche)
            .Select(a => new { a.Email, a.CodeHash })
            .FirstOrDefaultAsync(ct);
        if (zeile is null)
        {
            Protokoll(log, anmeldungId, "CodeAbgelehnt");
            return null;
        }

        if (code is null || !MagicLinkKrypto.CodePasst(anmeldungId, code, zeile.CodeHash))
        {
            // Atomar hochzaehlen, dann ab der Grenze loeschen: parallele Versuche zaehlen alle.
            await anmeldungen.Where(a => a.Id == anmeldungId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Fehlversuche, a => (short)(a.Fehlversuche + 1)), ct);
            await anmeldungen.Where(a => a.Id == anmeldungId && a.Fehlversuche >= AnmeldungOptionen.MaxFehlversuche)
                .ExecuteDeleteAsync(ct);
            Protokoll(log, anmeldungId, "CodeFalsch");
            return null;
        }

        // Einmalig: nur wer die Zeile tatsaechlich loescht, ist angemeldet.
        var geloescht = await anmeldungen
            .Where(a => a.Id == anmeldungId && a.LaeuftAbAm > jetzt && a.Fehlversuche < AnmeldungOptionen.MaxFehlversuche)
            .ExecuteDeleteAsync(ct);
        if (geloescht != 1)
        {
            Protokoll(log, anmeldungId, "CodeAbgelehnt");
            return null;
        }

        Protokoll(log, anmeldungId, "CodeBestaetigt");
        return await SpielerFuerAsync(zeile.Email, ct);
    }

    private async Task<string?> LoescheGueltigeMitTokenAsync(byte[] hash, DateTimeOffset jetzt, CancellationToken ct)
    {
        var verbindung = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var befehl = verbindung.CreateCommand();
            befehl.CommandText =
                "DELETE FROM \"Anmeldung\" WHERE \"TokenHash\" = @h AND \"LaeuftAbAm\" > @jetzt RETURNING \"Email\"";
            Parameter(befehl, "h", hash);
            Parameter(befehl, "jetzt", jetzt.ToUniversalTime());
            return await befehl.ExecuteScalarAsync(ct) as string;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static void Parameter(DbCommand befehl, string name, object wert)
    {
        var p = befehl.CreateParameter();
        p.ParameterName = name;
        p.Value = wert;
        befehl.Parameters.Add(p);
    }

    /// <summary>Spieler suchen oder anlegen. Ein Rennen faengt der Unique-Index <c>Spieler(Email)</c> ab.</summary>
    private async Task<Angemeldet> SpielerFuerAsync(string email, CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var spieler = await db.Set<Spieler>().FirstOrDefaultAsync(s => s.Email == email, ct);
        var neu = false;
        if (spieler is null)
        {
            spieler = new Spieler
            {
                Id = Guid.NewGuid(),
                Email = email,
                ErstelltAm = jetzt,
                Sicherheitsstempel = Guid.NewGuid(),
                Elo = AnmeldungOptionen.StartElo,
            };
            db.Set<Spieler>().Add(spieler);
            try
            {
                await db.SaveChangesAsync(ct);
                neu = true;
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                spieler = await db.Set<Spieler>().FirstAsync(s => s.Email == email, ct);
            }
        }

        spieler.ZuletztAngemeldetAm = jetzt;
        await db.SaveChangesAsync(ct);
        return new Angemeldet(spieler, neu);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "anmeldung id={AnmeldungId} ergebnis={Ergebnis}")]
    private static partial void Protokoll(ILogger log, Guid anmeldungId, string ergebnis);
}
