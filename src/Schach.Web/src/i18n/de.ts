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
  recht: {
    impressumTitel: 'Impressum',
    datenschutzTitel: 'Datenschutzerklärung',
    datenschutzStand: 'Stand: 7. November 2026',
  },
  // S3a: Brett und Partie-Bildschirm (SCHACH-SPEC 7, 10.3). `{name}`, `{feld}` usw. ersetzt `fuelle`.
  brett: {
    umwandlungTitel: 'Umwandeln in',
    umwandlungDame: 'Dame',
    umwandlungTurm: 'Turm',
    umwandlungLaeufer: 'Läufer',
    umwandlungSpringer: 'Springer',
  },
  partie: {
    elo: 'Elo {elo}',
    du: 'Du',
    menue: 'Menü',
    amZug: 'am Zug',
    farbeWeiss: 'Weiß',
    farbeSchwarz: 'Schwarz',
    uhrGegner: 'Uhr von {name}',
    uhrEigene: 'Deine Uhr',
    pilleZaehltNicht: 'Zählt nicht für die Elo',
    pilleGast: 'Gast',
    laedt: 'Partie wird geladen …',
    nichtGefunden: 'Diese Partie gibt es nicht oder nicht mehr.',
    knopfZuruecknehmen: 'Zurücknehmen',
    knopfTipp: 'Tipp',
    knopfRemis: 'Remis anbieten',
    knopfAufgeben: 'Aufgeben',
    knopfNachspielen: 'Nachspielen',
    knopfNeuePartie: 'Neue Partie',
  },
  status: {
    keineVerbindung: 'Keine Verbindung',
    schach: 'Schach!',
    denktNach: '{name} denkt nach …',
    zugNichtMoeglich: 'Zug nicht möglich',
    tippNichtVerfuegbar: 'Tipp gerade nicht verfügbar',
    aktionNichtMoeglich: 'Das geht gerade nicht.',
  },
  warnung: {
    Springer: 'Dein Springer auf {feld} ist angegriffen und ungedeckt.',
    Laeufer: 'Dein Läufer auf {feld} ist angegriffen und ungedeckt.',
    Turm: 'Dein Turm auf {feld} ist angegriffen und ungedeckt.',
    Dame: 'Deine Dame auf {feld} ist angegriffen und ungedeckt.',
  },
  tipp: {
    stufe1: {
      Koenig: 'Zieh mit dem König',
      Dame: 'Zieh mit der Dame',
      Turm: 'Zieh mit dem Turm',
      Laeufer: 'Zieh mit dem Läufer',
      Springer: 'Zieh mit dem Springer',
      Bauer: 'Zieh mit dem Bauern',
    },
    stufe2: '{figur} {von} nach {nach}',
    figur: {
      Koenig: 'König',
      Dame: 'Dame',
      Turm: 'Turm',
      Laeufer: 'Läufer',
      Springer: 'Springer',
      Bauer: 'Bauer',
    },
  },
  menue: {
    titel: 'Menü',
    erlaubteZuegeAnzeigen: 'Erlaubte Züge anzeigen',
    vorUngedecktenWarnen: 'Vor ungedeckten Figuren warnen',
    toeneUndVibration: 'Töne und Vibration',
    partieVerlassen: 'Partie verlassen',
    schliessen: 'Schließen',
  },
  dialog: {
    abbrechen: 'Abbrechen',
    aufgebenTitel: 'Partie aufgeben?',
    aufgebenKnopf: 'Aufgeben',
    aufgebenZaehlt: 'Aufgeben zählt als Niederlage und senkt deine Elo.',
    aufgebenZaehltNicht:
      'Aufgeben zählt als Niederlage. Deine Elo ändert sich nicht, weil diese Partie nicht für die Elo zählt.',
    aufgebenGast: 'Die Gastpartie endet als Niederlage und wird nicht gespeichert.',
    eloTitel: 'Partie zählt dann nicht für die Elo',
    eloTippText: 'Nach einem Tipp zählt diese Partie nicht mehr für deine Elo.',
    eloZuruecknehmenText: 'Nach dem Zurücknehmen zählt diese Partie nicht mehr für deine Elo.',
    eloTippKnopf: 'Tipp zeigen',
    eloZuruecknehmenKnopf: 'Zurücknehmen',
  },
  partieende: {
    titelSieg: 'Du hast gewonnen',
    titelNiederlage: 'Du hast verloren',
    titelRemis: 'Remis',
    schliessen: 'Schließen',
    eloZeile: 'Elo {elo} ({aenderung})',
    eloZaehltNicht: 'Elo: zählt nicht',
    gastHinweis: 'Konto anlegen, dann zählen Partien und du kannst sie nachspielen.',
    zurStartseite: 'Zur Startseite',
    grund: {
      Matt: 'Matt',
      Aufgabe: 'Aufgabe',
      Zeit: 'Zeit abgelaufen',
      Patt: 'Patt',
      Wiederholung: 'Stellungswiederholung',
      FuenfzigZuege: '50-Züge-Regel',
      Material: 'Zu wenig Material',
      ZeitOhneMattmaterial: 'Zeit abgelaufen ohne Mattmaterial',
      Vereinbarung: 'Remis vereinbart',
    },
    erklaerung: {
      Matt: {
        sieg: '{name} ist matt: Der König steht im Schach und hat keinen Ausweg mehr.',
        niederlage: 'Du bist matt: Dein König steht im Schach und hat keinen Ausweg mehr.',
      },
      Aufgabe: {
        niederlage: 'Du hast die Partie aufgegeben.',
        sieg: '{name} hat die Partie aufgegeben.',
      },
      Zeit: {
        sieg: 'Die Bedenkzeit von {name} ist abgelaufen.',
        niederlage: 'Deine Bedenkzeit ist abgelaufen.',
      },
      Patt: {
        remis: 'Patt: Wer am Zug ist, hat keinen erlaubten Zug, steht aber nicht im Schach.',
      },
      Wiederholung: {
        remis: 'Dieselbe Stellung ist zum dritten Mal entstanden.',
      },
      FuenfzigZuege: {
        remis: '50 Züge lang wurde kein Bauer gezogen und nichts geschlagen.',
      },
      Material: {
        remis: 'Keine Seite hat genug Figuren, um noch matt zu setzen.',
      },
      ZeitOhneMattmaterial: {
        remis: 'Die Bedenkzeit ist abgelaufen, aber die andere Seite konnte nicht mehr matt setzen.',
      },
      Vereinbarung: {
        remis: 'Du hast Remis angeboten und {name} hat angenommen.',
      },
    },
  },
  prototyp: {
    titel: 'Brett-Prototyp',
    hinweis: 'Partie gegen einen lokalen Fake-Server (MSW und chess.js). Der Gegner zieht zufällig.',
    gegner: 'Gegner',
    farbe: 'Farbe',
    farbeWeiss: 'Weiß',
    farbeSchwarz: 'Schwarz',
    farbeZufall: 'Zufall',
    bedenkzeit: 'Bedenkzeit',
    bedenkzeitOhneUhr: 'Ohne Uhr',
    bedenkzeit15Plus10: '15+10',
    bedenkzeit10Plus5: '10+5',
    bedenkzeit5Plus3: '5+3',
    bedenkzeit90s: '90 s je Zug',
    rolle: 'Rolle',
    rolleSpieler: 'Spieler',
    rolleGast: 'Gast',
    stellung: 'Stellung',
    stellungStart: 'Grundstellung',
    stellungUmwandlung: 'Umwandlung (Bauer auf der 7. Reihe)',
    stellungUngedeckt: 'Ungedeckter Springer',
    stellungMatt: 'Matt in 1',
    uhrKnapp: 'Eigene Uhr startet bei 25 s',
    starten: 'Partie starten',
    startFehler: 'Der Fake-Server startet nicht. Läuft die Seite auf localhost?',
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
