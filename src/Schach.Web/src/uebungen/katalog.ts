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
    key: 'schach-abwehren',
    bereich: 'regeln',
    titel: 'Schach abwehren',
    kurz: 'Wegziehen, dazwischenstellen oder schlagen.',
    fen: '4r1k1/5ppp/8/8/8/3N3P/5PP1/6K1 b - - 0 1',
    farbe: 'Weiss',
    vorzug: 'e8e1',
    einleitung:
      'Schach! Mein Turm greift deinen König an. Im Schach musst du sofort etwas tun: König wegziehen, eine Figur dazwischenstellen oder den Angreifer schlagen.',
    schritte: [
      {
        aufgabe: 'Wehr das Schach ab. Am besten schlägst du meinen Turm auf e1.',
        zuege: ['d3e1'],
        hinweis: 'Das rettet deinen König, aber es geht besser: Dein Springer auf d3 kann meinen Turm schlagen.',
        erklaerung: 'Stark! Du hast das Schach abgewehrt und dabei noch meinen Turm gewonnen. Schlagen ist oft die beste Abwehr.',
      },
    ],
    abschluss:
      'Steht dein König im Schach, sind nur Züge erlaubt, die das Schach beenden. Prüf immer zuerst, ob du den Angreifer schlagen kannst.',
  },
  {
    key: 'patt-vermeiden',
    bereich: 'regeln',
    titel: 'Patt vermeiden',
    kurz: 'Gewonnen ist erst, wenn der König im Schach steht.',
    fen: '7k/5K2/8/8/4Q3/8/8/8 w - - 0 1',
    farbe: 'Weiss',
    einleitung:
      'Ich habe nur noch meinen König. Aber Vorsicht: Kann ich nicht mehr ziehen und stehe nicht im Schach, ist es Patt. Dann endet die Partie Remis.',
    schritte: [
      {
        aufgabe: 'Setz mich matt, ohne Patt. Gib Schach mit der Dame auf der h-Linie.',
        zuege: ['e4h4', 'e4h1'],
        hinweis:
          'Prüf nach deinem Zug: Kann mein König noch ziehen? Steht er nicht im Schach und hat kein freies Feld, ist es Patt. Such einen Zug mit Schach auf der h-Linie.',
        erklaerung: 'Matt! Mein König steht im Schach, und alle Felder um ihn decken deine Dame und dein König.',
      },
    ],
    abschluss:
      'Hätte deine Dame auf g6 gestanden, wäre es Patt gewesen: kein Schach, aber auch kein Zug für mich. Mit viel Material lohnt es sich, vor jedem Zug kurz nachzuzählen.',
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
    key: 'dame-und-koenig',
    bereich: 'matt',
    titel: 'Matt mit Dame und König',
    kurz: 'Die Dame sperrt, der König hilft beim Matt.',
    fen: '4k3/8/4K3/8/8/8/8/Q7 w - - 0 1',
    farbe: 'Weiss',
    einleitung:
      'Mit Dame und König setzt du einen König allein matt. Die Dame kann das nicht allein, sie braucht die Hilfe deines Königs.',
    schritte: [
      {
        aufgabe: 'Sperr meinem König die siebte Reihe: Dame a1 nach a7.',
        zuege: ['a1a7'],
        hinweis: 'Die Dame soll zuerst die siebte Reihe sperren. Zieh sie nach a7.',
        erklaerung: 'Gut. Mein König kann nur noch auf der letzten Reihe hin und her. Pass auf, dass er noch ein Feld hat, sonst wäre es Patt.',
        antwort: 'e8f8',
      },
      {
        aufgabe: 'Setz mich matt: Dame nach f7, direkt vor meinen König.',
        zuege: ['a7f7'],
        hinweis: 'Deine Dame darf direkt neben meinen König, wenn dein König sie deckt. Probier f7.',
        erklaerung: 'Matt! Ich kann deine Dame nicht schlagen, weil dein König sie deckt.',
      },
    ],
    abschluss:
      'Treib den König mit der Dame an den Rand, hol deinen König heran und setz dann matt. Die Dame steht dabei oft direkt vor dem gegnerischen König, gedeckt vom eigenen.',
  },
  {
    key: 'ersticktes-matt',
    bereich: 'matt',
    titel: 'Ersticktes Matt',
    kurz: 'Ein Springer setzt einen König matt, der sich selbst eingemauert hat.',
    fen: '6rk/6pp/8/6N1/8/8/8/6K1 w - - 0 1',
    farbe: 'Weiss',
    einleitung:
      'Mein König steht in der Ecke, umringt von seinem Turm und seinen Bauern. Er hat sich selbst eingemauert. Das kann ein Springer ausnutzen.',
    schritte: [
      {
        aufgabe: 'Setz mich mit dem Springer matt.',
        zuege: ['g5f7'],
        hinweis: 'Von welchem Feld greift dein Springer h8 an? Schau auf f7.',
        erklaerung: 'Matt! Mein König kann nicht weg, weil seine eigenen Figuren alle Felder besetzen. Das nennt man ersticktes Matt.',
      },
    ],
    abschluss:
      'Springer springen über Figuren. Darum ist ein König, der von eigenen Figuren eingeklemmt ist, gegen einen Springer besonders gefährdet.',
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
    key: 'fesselung',
    bereich: 'taktik',
    titel: 'Fesselung',
    kurz: 'Eine Figur darf nicht weg, weil dahinter der König steht.',
    fen: '4k3/8/8/4q3/8/8/5K2/R7 w - - 0 1',
    farbe: 'Weiss',
    einleitung:
      'Meine Dame und mein König stehen auf derselben Linie. Greifst du die Dame auf dieser Linie an, darf sie nicht weg, sonst stünde mein König im Schach. Das nennt man Fesselung.',
    schritte: [
      {
        aufgabe: 'Fessle meine Dame: Turm a1 nach e1.',
        zuege: ['a1e1'],
        hinweis: 'Stell deinen Turm auf die e-Linie, auf der meine Dame und mein König stehen.',
        erklaerung: 'Gefesselt! Meine Dame kann nicht zur Seite, sonst stünde mein König im Schach. Ich schlage deinen Turm, bevor ich sie ganz verliere.',
        antwort: 'e5e1',
      },
      {
        aufgabe: 'Schlag meine Dame mit dem König.',
        zuege: ['f2e1'],
        hinweis: 'Meine Dame steht direkt neben deinem König. Schlag sie.',
        erklaerung: 'Ein Turm gegen eine Dame, ein sehr gutes Geschäft für dich.',
      },
    ],
    abschluss:
      'Achte auf Figuren, die mit dem König auf einer Linie stehen. Ein Läufer, Turm oder eine Dame kann sie fesseln. Gefesselte Figuren sind leichte Beute.',
  },
  {
    key: 'spiess',
    bereich: 'taktik',
    titel: 'Spieß',
    kurz: 'Schach dem König, und die Figur dahinter fällt.',
    fen: '4q3/8/8/4k3/8/8/8/R5K1 w - - 0 1',
    farbe: 'Weiss',
    einleitung:
      'Beim Spieß greifst du eine wertvolle Figur an, hinter der eine andere steht. Weicht die vordere aus, schlägst du die hintere. Mein König steht vor meiner Dame.',
    schritte: [
      {
        aufgabe: 'Gib Schach auf der e-Linie: Turm a1 nach e1.',
        zuege: ['a1e1'],
        hinweis: 'Mein König und meine Dame stehen beide auf der e-Linie. Gib dort Schach.',
        erklaerung: 'Schach! Mein König muss die Linie verlassen, und dann ist meine Dame frei für deinen Turm.',
        antwort: 'e5d6',
      },
      {
        aufgabe: 'Schlag meine Dame auf e8.',
        zuege: ['e1e8'],
        hinweis: 'Dein Turm sieht jetzt meine Dame auf e8. Schlag sie.',
        erklaerung: 'Die Dame gehört dir. Das war ein Spieß.',
      },
    ],
    abschluss:
      'Der Spieß ist die umgekehrte Fesselung: Vorne steht die wertvollere Figur. Mit einem Schach klappt er fast immer, weil der König ausweichen muss.',
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
  {
    key: 'damengambit',
    bereich: 'eroeffnung',
    titel: 'Damengambit',
    kurz: 'Ein Bauer wird angeboten, um die Mitte zu bekommen.',
    fen: 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1',
    farbe: 'Weiss',
    einleitung:
      'Das Damengambit beginnt mit dem Damenbauern. Ein Gambit heißt: Du bietest einen Bauern an, damit du dafür die Mitte bekommst.',
    schritte: [
      {
        aufgabe: 'Besetz die Mitte mit dem Damenbauern: d2 nach d4.',
        zuege: ['d2d4'],
        hinweis: 'Diesmal fängst du mit dem Bauern vor der Dame an. Zieh ihn von d2 nach d4.',
        erklaerung: 'Gut, auch so kontrollierst du die Mitte. Ich antworte genauso.',
        antwort: 'd7d5',
      },
      {
        aufgabe: 'Biete mir einen Bauern an: c2 nach c4.',
        zuege: ['c2c4'],
        hinweis: 'Jetzt kommt das Gambit. Zieh den Bauern von c2 nach c4, er greift meinen Bauern auf d5 an.',
        erklaerung: 'Das ist das Damengambit. Ich nehme den Bauern nicht, sondern stütze meinen Bauern auf d5 mit e6.',
        antwort: 'e7e6',
      },
      {
        aufgabe: 'Entwickle einen Springer, am besten nach c3 oder f3.',
        zuege: ['b1c3', 'g1f3'],
        hinweis: 'Springer kommen früh ins Spiel. Zieh einen nach c3 oder f3.',
        erklaerung: 'Prima, dein Springer erhöht den Druck auf d5. Ich bringe auch meinen Springer.',
        antwort: 'g8f6',
      },
      {
        aufgabe: 'Bring deinen Läufer nach g5. Dort greift er meinen Springer an.',
        zuege: ['c1g5'],
        hinweis: 'Der Läufer auf c1 will raus. Zieh ihn nach g5.',
        erklaerung: 'Sehr gut! Dein Läufer greift meinen Springer an, der d5 schützt. Ich stelle meinen Läufer dazwischen.',
        antwort: 'f8e7',
      },
    ],
    abschluss:
      'Im Damengambit kämpfst du mit c4 um die Mitte. Nimmt Schwarz den Bauern, holst du ihn meistens später zurück. Danach: Figuren raus und rochieren.',
  },
  {
    key: 'sizilianisch',
    bereich: 'eroeffnung',
    titel: 'Sizilianische Verteidigung',
    kurz: 'Als Schwarz gegen e4 kämpfen, ohne den Zug nachzumachen.',
    fen: 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1',
    farbe: 'Schwarz',
    vorzug: 'e2e4',
    einleitung:
      'Du spielst Schwarz. Ich beginne mit e4. Die Sizilianische Verteidigung ist eine sehr beliebte Antwort darauf: Du kämpfst von der Seite um die Mitte.',
    schritte: [
      {
        aufgabe: 'Antworte mit dem c-Bauern: c7 nach c5.',
        zuege: ['c7c5'],
        hinweis: 'Diesmal nicht e5. Zieh den Bauern von c7 nach c5, er kontrolliert d4.',
        erklaerung: 'Das ist Sizilianisch. Dein Bauer auf c5 hält das Feld d4 im Blick. Ich bringe meinen Springer.',
        antwort: 'g1f3',
      },
      {
        aufgabe: 'Mach Platz für deinen Läufer: d7 nach d6.',
        zuege: ['d7d6'],
        hinweis: 'Ein kleiner Bauernzug: d7 nach d6. Er öffnet den Weg für deinen Läufer auf c8.',
        erklaerung: 'Gut. Jetzt öffne ich die Mitte mit d4.',
        antwort: 'd2d4',
      },
      {
        aufgabe: 'Schlag meinen Bauern auf d4.',
        zuege: ['c5d4'],
        hinweis: 'Mein Bauer auf d4 steht in Reichweite deines c-Bauern. Schlag ihn.',
        erklaerung: 'Genau. Du tauschst deinen Randbauern gegen meinen Mittelbauern. Ich schlage mit dem Springer zurück.',
        antwort: 'f3d4',
      },
      {
        aufgabe: 'Entwickle deinen Springer nach f6 und greif meinen Bauern auf e4 an.',
        zuege: ['g8f6'],
        hinweis: 'Dein Springer auf g8 will ins Spiel. Zieh ihn nach f6.',
        erklaerung: 'Sehr gut! Dein Springer greift e4 an, und ich muss ihn decken. Du hast eine gesunde Stellung.',
        antwort: 'b1c3',
      },
    ],
    abschluss:
      'Bei Sizilianisch hast du am Ende einen Mittelbauern mehr als Weiß. Dafür ist Weiß schneller entwickelt. Bring deine Figuren zügig heraus und rochiere.',
  },
]

export function findeUebung(key: string): Uebung | undefined {
  return UEBUNGEN.find((u) => u.key === key)
}
