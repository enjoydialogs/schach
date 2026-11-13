import type { Farbe } from '../api/types'

// Uebungen (SCHACH-SPEC 10.16): feste Stellungen mit vorgegebenen Zuegen und Erklaerungen. Alles
// laeuft im Browser; der Server kennt keine Uebung. `uebungen.test.ts` spielt jede Uebung mit
// chess.js durch und prueft, dass alle Zuege erlaubt sind.

export type Bereich = 'regeln' | 'matt' | 'taktik' | 'eroeffnung'

export interface Schritt {
  /** Was der Spieler tun soll; steht in der Statuszeile. */
  aufgabe: string
  /** Erlaubte Loesungen als UCI (Umwandlung mit Figur, z. B. `e7e8q`). Der erste ist der Tipp. */
  zuege: readonly string[]
  /** Hinweis des Gegners nach einem anderen Zug. Ohne ihn: allgemeiner Satz. */
  hinweis?: string
  /** Erklaerung des Gegners nach dem richtigen Zug. */
  erklaerung: string
  /** Antwort des Gegners als UCI, gespielt nach der Erklaerung. */
  antwort?: string
}

export interface Uebung {
  key: string
  bereich: Bereich
  titel: string
  /** Eine Zeile fuer die Liste. */
  kurz: string
  /** Startstellung als FEN. */
  fen: string
  /** Farbe des Spielers; das Brett steht von seiner Seite. */
  farbe: Farbe
  /** Erster Zug des Gegners, bevor der Spieler dran ist. */
  vorzug?: string
  /** Erster Satz des Gegners. */
  einleitung: string
  schritte: readonly Schritt[]
  /** Satz am Ende, nach dem letzten Schritt. */
  abschluss: string
}

export const BEREICHE: readonly Bereich[] = ['regeln', 'matt', 'taktik', 'eroeffnung']

export const UEBUNGEN: readonly Uebung[] = [
  {
    key: 'en-passant',
    bereich: 'regeln',
    titel: 'Schlagen en passant',
    kurz: 'Ein Bauer schlägt einen Bauern, der an ihm vorbeizieht.',
    fen: '4k3/3p4/8/4P3/8/8/8/4K3 b - - 0 1',
    farbe: 'Weiss',
    vorzug: 'd7d5',
    einleitung:
      'Mein Bauer ist gerade zwei Felder nach vorn gezogen, direkt neben deinen. Er dachte, er kommt so an dir vorbei.',
    schritte: [
      {
        aufgabe: 'Schlag den Bauern auf d5 en passant: zieh deinen Bauern von e5 nach d6.',
        zuege: ['e5d6'],
        hinweis:
          'Das geht, aber nur genau jetzt darfst du en passant schlagen. Zieh deinen Bauern schräg nach d6, hinter meinen.',
        erklaerung:
          'Genau! Du ziehst schräg auf das Feld, das mein Bauer übersprungen hat, und nimmst ihn vom Brett. Das geht nur im Zug direkt danach.',
      },
    ],
    abschluss:
      'En passant gilt nur, wenn ein Bauer zwei Felder zieht und neben deinem Bauern landet. Wartest du einen Zug, ist die Chance weg.',
  },
  {
    key: 'rochade',
    bereich: 'regeln',
    titel: 'Rochade',
    kurz: 'König in Sicherheit bringen und den Turm ins Spiel holen.',
    fen: 'r3k2r/pppppppp/8/8/8/8/PPPPPPPP/R3K2R w KQkq - 0 1',
    farbe: 'Weiss',
    einleitung:
      'Bei der Rochade ziehen König und Turm in einem Zug. Das ist der einzige Zug, bei dem zwei Figuren gleichzeitig ziehen.',
    schritte: [
      {
        aufgabe: 'Mach die kurze Rochade: zieh deinen König von e1 nach g1.',
        zuege: ['e1g1'],
        hinweis: 'Zieh den König zwei Felder nach rechts, nach g1. Der Turm springt dann von allein über ihn.',
        erklaerung:
          'Sehr gut! Der König steht jetzt hinter seinen Bauern sicher, und der Turm ist auf f1 bereit. Jetzt zeige ich dir die lange Rochade.',
        antwort: 'e8c8',
      },
    ],
    abschluss:
      'Rochieren darfst du nur, wenn König und Turm noch nie gezogen haben, die Felder dazwischen frei sind und dein König nicht im Schach steht oder durch ein angegriffenes Feld zieht.',
  },
  {
    key: 'umwandlung',
    bereich: 'regeln',
    titel: 'Bauernumwandlung',
    kurz: 'Ein Bauer auf der letzten Reihe wird zur Dame.',
    fen: '8/4P3/8/8/8/2k5/8/4K3 w - - 0 1',
    farbe: 'Weiss',
    einleitung: 'Dein Bauer ist fast am Ziel. Kommt ein Bauer auf der letzten Reihe an, wird er zu einer anderen Figur.',
    schritte: [
      {
        aufgabe: 'Zieh den Bauern nach e8 und wähle die Dame.',
        zuege: ['e7e8q'],
        hinweis: 'Das ist erlaubt, aber die Dame ist fast immer die stärkste Wahl. Versuch es noch einmal.',
        erklaerung: 'Prima, eine neue Dame! Du darfst sogar eine zweite Dame haben, auch wenn die erste noch steht.',
      },
    ],
    abschluss:
      'Du kannst zwischen Dame, Turm, Läufer und Springer wählen. Meistens nimmst du die Dame, nur selten ist ein Springer besser.',
  },
  {
    key: 'grundreihenmatt',
    bereich: 'matt',
    titel: 'Grundreihenmatt',
    kurz: 'Der König ist von seinen eigenen Bauern eingesperrt.',
    fen: '6k1/5ppp/8/8/8/8/5PPP/3R2K1 w - - 0 1',
    farbe: 'Weiss',
    einleitung:
      'Schau dir meinen König auf g8 an. Vor ihm stehen drei Bauern, und er hat kein Fluchtfeld nach vorn. Das ist gefährlich für mich.',
    schritte: [
      {
        aufgabe: 'Setz mich matt: zieh deinen Turm auf die letzte Reihe.',
        zuege: ['d1d8'],
        hinweis: 'Schau auf die achte Reihe. Dein Turm kann bis ganz nach hinten ziehen.',
        erklaerung: 'Matt! Mein König kann nicht nach vorn, weil seine eigenen Bauern im Weg stehen, und die Reihe deckt dein Turm.',
      },
    ],
    abschluss:
      'Gib deinem König darum früh ein Luftloch, zum Beispiel mit dem Bauern h2 nach h3. Dann kann ihn ein Turm auf der Grundreihe nicht matt setzen.',
  },
  {
    key: 'treppenmatt',
    bereich: 'matt',
    titel: 'Treppenmatt mit zwei Türmen',
    kurz: 'Zwei Türme treiben den König Reihe für Reihe an den Rand.',
    fen: '8/4k3/8/8/8/8/RR6/6K1 w - - 0 1',
    farbe: 'Weiss',
    einleitung:
      'Mit zwei Türmen kannst du einen König allein matt setzen. Die Türme arbeiten wie eine Treppe: einer sperrt eine Reihe, der andere gibt Schach.',
    schritte: [
      {
        aufgabe: 'Sperr meinem König die sechste Reihe: zieh den Turm von b2 nach b6.',
        zuege: ['b2b6'],
        hinweis: 'Ein Turm soll eine ganze Reihe sperren. Zieh den Turm von b2 nach b6.',
        erklaerung: 'Gut, jetzt kann mein König nicht mehr nach unten. Er muss auf der siebten oder achten Reihe bleiben.',
        antwort: 'e7d7',
      },
      {
        aufgabe: 'Gib Schach auf der siebten Reihe: Turm a2 nach a7.',
        zuege: ['a2a7'],
        hinweis: 'Jetzt ist der andere Turm dran. Er gibt Schach auf der siebten Reihe, von a7 aus.',
        erklaerung: 'Schach! Mein König muss auf die letzte Reihe, denn die sechste sperrt dein anderer Turm.',
        antwort: 'd7e8',
      },
      {
        aufgabe: 'Setz mich matt: Turm b6 nach b8.',
        zuege: ['b6b8'],
        hinweis: 'Der Turm auf b6 kann jetzt auf die achte Reihe und Schach geben.',
        erklaerung: 'Matt! Ein Turm deckt die siebte Reihe, der andere gibt Schach auf der achten.',
      },
    ],
    abschluss:
      'Achte darauf, dass deine Türme weit genug vom König weg stehen. Sonst schlägt er einen. Dann ziehst du den Turm einfach ans andere Ende der Reihe.',
  },
  {
    key: 'springergabel',
    bereich: 'taktik',
    titel: 'Springergabel',
    kurz: 'Ein Springer greift zwei Figuren gleichzeitig an.',
    fen: '2q3k1/5ppp/8/3N4/8/8/5PPP/6K1 w - - 0 1',
    farbe: 'Weiss',
    einleitung:
      'Ein Springer kann zwei Figuren auf einmal angreifen. Das nennt man Gabel. Am stärksten ist sie, wenn eine der Figuren der König ist.',
    schritte: [
      {
        aufgabe: 'Finde das Feld, von dem dein Springer meinen König und meine Dame angreift.',
        zuege: ['d5e7'],
        hinweis: 'Suche ein Feld, von dem dein Springer g8 und c8 zugleich erreicht. Probier e7.',
        erklaerung: 'Gabel! Mein König steht im Schach und muss weg. Meine Dame auf c8 kann ich nicht mehr retten.',
        antwort: 'g8h8',
      },
      {
        aufgabe: 'Schlag jetzt meine Dame auf c8.',
        zuege: ['e7c8'],
        hinweis: 'Dein Springer greift noch meine Dame auf c8 an. Schlag sie.',
        erklaerung: 'Die Dame gehört dir. Für einen Springer hast du die stärkste Figur bekommen.',
      },
    ],
    abschluss:
      'Schau bei jedem Zug, ob dein Springer zwei Figuren gleichzeitig angreifen kann. Und pass auf, dass dir nicht dasselbe passiert.',
  },
  {
    key: 'italienisch',
    bereich: 'eroeffnung',
    titel: 'Italienische Partie',
    kurz: 'Eine klassische Eröffnung: Mitte besetzen, Figuren entwickeln, rochieren.',
    fen: 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1',
    farbe: 'Weiss',
    einleitung:
      'In der Eröffnung hast du drei Ziele: die Mitte besetzen, Springer und Läufer ins Spiel bringen und den König in Sicherheit bringen.',
    schritte: [
      {
        aufgabe: 'Besetz die Mitte: Bauer e2 nach e4.',
        zuege: ['e2e4'],
        hinweis: 'Fang mit einem Bauern in der Mitte an. Zieh den Bauern von e2 nach e4.',
        erklaerung: 'Gut, dein Bauer kontrolliert jetzt die Mitte und macht Platz für Läufer und Dame. Ich mache dasselbe.',
        antwort: 'e7e5',
      },
      {
        aufgabe: 'Bring einen Springer ins Spiel und greif meinen Bauern an: Springer g1 nach f3.',
        zuege: ['g1f3'],
        hinweis: 'Springer kommen zuerst ins Spiel. Zieh den Springer von g1 nach f3.',
        erklaerung: 'Prima, dein Springer greift meinen Bauern auf e5 an. Ich decke ihn mit meinem Springer.',
        antwort: 'b8c6',
      },
      {
        aufgabe: 'Entwickle deinen Läufer nach c4. Von dort zielt er auf f7.',
        zuege: ['f1c4'],
        hinweis: 'Der Läufer auf f1 will ins Spiel. Zieh ihn nach c4.',
        erklaerung: 'Das ist die Italienische Partie. Dein Läufer zielt auf f7, den schwächsten Punkt neben meinem König.',
        antwort: 'f8c5',
      },
      {
        aufgabe: 'Bring deinen König in Sicherheit: kurze Rochade, König e1 nach g1.',
        zuege: ['e1g1'],
        hinweis: 'Jetzt ist der Weg frei für die Rochade. Zieh den König nach g1.',
        erklaerung: 'Perfekt! Mitte besetzt, zwei Figuren entwickelt, König sicher. So sieht eine gute Eröffnung aus.',
        antwort: 'g8f6',
      },
    ],
    abschluss:
      'Merk dir für jede Eröffnung: Mitte, Figuren raus, Rochade. Zieh nicht zu früh mit der Dame und nicht zweimal mit derselben Figur.',
  },
  {
    key: 'schaefermatt-abwehren',
    bereich: 'eroeffnung',
    titel: 'Schäfermatt abwehren',
    kurz: 'Eine bekannte Falle für Anfänger erkennen und verhindern.',
    fen: 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1',
    farbe: 'Schwarz',
    vorzug: 'e2e4',
    einleitung:
      'Heute spielst du Schwarz. Ich versuche einen frechen Trick, das Schäfermatt. Mal sehen, ob du ihn durchschaust.',
    schritte: [
      {
        aufgabe: 'Antworte in der Mitte: Bauer e7 nach e5.',
        zuege: ['e7e5'],
        hinweis: 'Stell dich mir in der Mitte entgegen. Zieh den Bauern von e7 nach e5.',
        erklaerung: 'Gut. Jetzt bringe ich meinen Läufer nach c4. Er zielt auf dein Feld f7.',
        antwort: 'f1c4',
      },
      {
        aufgabe: 'Entwickle deinen Springer nach c6.',
        zuege: ['b8c6'],
        hinweis: 'Bring zuerst einen Springer ins Spiel, nach c6.',
        erklaerung: 'Ordentlich. Pass jetzt auf: Meine Dame kommt nach h5. Dame und Läufer greifen beide f7 an.',
        antwort: 'd1h5',
      },
      {
        aufgabe: 'Meine Dame droht auf f7 matt. Versperr ihr den Weg oder deck f7.',
        zuege: ['g7g6', 'd8e7'],
        hinweis: 'Achtung, ich drohe Dame schlägt f7, matt! Hier zeige ich dir zwei gute Abwehren: Bauer g7 nach g6 greift meine Dame an, Dame d8 nach e7 deckt f7.',
        erklaerung: 'Abgewehrt! Jetzt kann meine Dame nicht nach f7. Ich versuche es noch einmal von f3 aus.',
        antwort: 'h5f3',
      },
      {
        aufgabe: 'Meine Dame zielt wieder auf f7. Stell deinen Springer davor: g8 nach f6.',
        zuege: ['g8f6'],
        hinweis: 'Der Springer auf g8 kann sich vor f7 stellen. Zieh ihn nach f6.',
        erklaerung: 'Sehr gut! Dein Springer sperrt die Linie und ist gleichzeitig gut entwickelt. Mein Trick ist gescheitert.',
      },
    ],
    abschluss:
      'Kommt die Dame früh heraus, schau immer auf f7 (als Weiß auf f2). Deck das Feld oder versperr den Weg, und entwickle dabei deine Figuren.',
  },
]

export function findeUebung(key: string): Uebung | undefined {
  return UEBUNGEN.find((u) => u.key === key)
}
