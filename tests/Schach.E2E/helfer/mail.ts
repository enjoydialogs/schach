import { readdir, readFile } from 'node:fs/promises'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect } from '@playwright/test'

// Mails des FileEmailSender (src/Schach.Infrastructure/Email/EntwicklungsEmailSender.cs): je Mail
// eine JSON-Datei `OutboundEmail` mit PascalCase-Feldern im Bind-Mount aus compose.e2e.yml.
export const MAIL_ORDNER = process.env.E2E_MAIL_DIR ?? fileURLToPath(new URL('../.mail', import.meta.url))

interface Mail {
  To: string
  Subject: string
  TextBody: string
}

/** Eindeutige Adresse je Test, damit Mails und Konten sich nie mischen. */
export function neueAdresse(prefix = 'e2e'): string {
  const zufall = Math.random().toString(36).slice(2, 8)
  return `${prefix}-${Date.now()}-${zufall}@example.test`
}

async function mailsAn(adresse: string): Promise<Mail[]> {
  let dateien: string[]
  try {
    dateien = (await readdir(MAIL_ORDNER)).filter((d) => d.endsWith('.json')).sort()
  } catch {
    return []
  }
  const mails: Mail[] = []
  for (const datei of dateien) {
    try {
      const mail = JSON.parse(await readFile(join(MAIL_ORDNER, datei), 'utf8')) as Mail
      if (mail.To.toLowerCase() === adresse.toLowerCase()) mails.push(mail)
    } catch {
      // Datei wird gerade geschrieben: beim naechsten Versuch
    }
  }
  return mails
}

/**
 * Wartet auf die `anzahl`-te Anmelde-Mail an `adresse` und liefert ihren 6-stelligen Code. Die Mail
 * zeigt ihn als „123 456“ (AnmeldeMail.cs); Dateinamen beginnen mit einem Zeitstempel.
 */
export async function leseCode(adresse: string, anzahl = 1): Promise<string> {
  let mails: Mail[] = []
  await expect
    .poll(async () => (mails = await mailsAn(adresse)).length, {
      message: `Anmelde-Mail an ${adresse} in ${MAIL_ORDNER}`,
      timeout: 20_000,
    })
    .toBeGreaterThanOrEqual(anzahl)
  const text = mails[anzahl - 1]!.TextBody
  const treffer = /\b(\d{3}) (\d{3})\b/.exec(text)
  if (treffer === null) throw new Error(`Kein Code in der Mail an ${adresse}`)
  return `${treffer[1]}${treffer[2]}`
}
