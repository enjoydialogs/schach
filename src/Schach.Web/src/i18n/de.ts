// Alle UI-Texte (SCHACH-SPEC 10.14). Append-Register (15.1 Regel 5): Schluessel nur anhaengen,
// nie umbenennen. Anzeigetexte kommen von hier, nie aus Enum-Werten der API (8.4a).
export const de = {
  app: {
    name: 'Schach',
  },
  allgemein: {
    fehler: 'Etwas ist schiefgegangen. Bitte versuch es noch einmal.',
    erneutVersuchen: 'Erneut versuchen',
    zurStartseite: 'Zur Startseite',
  },
  fusszeile: {
    impressum: 'Impressum',
    datenschutz: 'Datenschutz',
    quellcode: 'Quellcode',
    trenner: ' · ',
  },
  start: {
    untertitel: 'Lerne Schach in Partien gegen Gegner mit eigener Persönlichkeit.',
    neuePartie: 'Neue Partie',
  },
  nichtGefunden: {
    titel: 'Diese Seite gibt es nicht.',
  },
} as const

type Pfade<T, P extends string = ''> = {
  [K in keyof T & string]: T[K] extends string ? `${P}${K}` : Pfade<T[K], `${P}${K}.`>
}[keyof T & string]

export type TextSchluessel = Pfade<typeof de>

/** Text zu einem Punkt-Schluessel, z. B. `t('fusszeile.impressum')`. */
export function t(schluessel: TextSchluessel): string {
  let knoten: unknown = de
  for (const teil of schluessel.split('.')) {
    knoten = (knoten as Record<string, unknown>)[teil]
  }
  return knoten as string
}
