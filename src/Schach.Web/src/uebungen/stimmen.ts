import type { Persoenlichkeit } from '../api/types'
import type { Uebung } from './katalog'

// Stimmen der Uebungen (SCHACH-SPEC 10.16): dieselben Erklaerungen wie im Katalog, aber im Ton der
// Persoenlichkeit des zufaelligen Gegners (4.2, 4.3). Feste Texte, kein LLM. Schachinhalt, Felder und
// angekuendigte Gegnerzuege bleiben wie im neutralen Text; `uebungen.test.ts` prueft das. Die
// Aufgabe in der Statuszeile bleibt neutral. Neue Uebungen brauchen hier alle sechs Stimmen.
//
// Ton je Persoenlichkeit (Aggressiv und Lehrerhaft decken je zwei Gegner ab, darum ihr gemeinsamer Kern):
// - Aggressiv (Finn, Maren): kurz, angriffslustig, selbstbewusst.
// - Vorsichtig (Theo): ruhig, nuechtern, „sicher ist sicher“, lobt sachlich.
// - Lehrerhaft (Oscar, Konrad): warm und geordnet, „Merk dir:“, ohne Spott.
// - Verspielt (Nele): locker, „ups“, neugierig, lacht ueber eigene Fehler.
// - Eitel (Sementa): Buehne und Applaus, lobt sich selbst, charmant.
// - Gemuetlich (Ilse): bodenstaendig, „mei“, „gell“, „a bisserl“, „passt scho“.
// Fuer alle gilt die harte Grenze aus 4.2: nichts ueber die Person, keine Beleidigung, Du-Form.

export interface Stimme {
  /** Erster Satz des Gegners. */
  einleitung: string
  /** Je Schritt in Katalog-Reihenfolge: Hinweis nach einem anderen Zug, Erklaerung nach dem richtigen. */
  schritte: readonly { hinweis: string; erklaerung: string }[]
  /** Satz am Ende. */
  abschluss: string
}

export const PERSOENLICHKEITEN: readonly Persoenlichkeit[] = [
  'Aggressiv',
  'Vorsichtig',
  'Lehrerhaft',
  'Verspielt',
  'Eitel',
  'Gemuetlich',
]

export const STIMMEN: Readonly<Record<string, Readonly<Record<Persoenlichkeit, Stimme>>>> = {
  'en-passant': {
    Aggressiv: {
      einleitung: 'Mein Bauer stürmt zwei Felder vor, direkt neben deinen. Er will einfach an dir vorbei. Frech, oder?',
      schritte: [
        {
          hinweis: 'Nicht der! Jetzt oder nie: Du darfst en passant schlagen. Zieh deinen Bauern schräg nach d6, hinter meinen.',
          erklaerung: 'Zack, erwischt! Du ziehst schräg auf das übersprungene Feld, und mein Bauer fliegt vom Brett. Das geht nur im Zug direkt danach.',
        },
      ],
      abschluss:
        'En passant geht nur sofort, nachdem ein Bauer zwei Felder gezogen ist und neben deinem landet. Wer einen Zug zögert, verpasst den Angriff.',
    },
    Vorsichtig: {
      einleitung: 'Mein Bauer ist zwei Felder vorgezogen und steht jetzt neben deinem. Ich hoffe, er kommt so sicher an dir vorbei.',
      schritte: [
        {
          hinweis: 'Möglich, aber lieber nicht. Jetzt, und nur jetzt, darfst du en passant schlagen. Zieh deinen Bauern schräg nach d6, hinter meinen.',
          erklaerung: 'Richtig. Du ziehst schräg auf das übersprungene Feld und nimmst meinen Bauern vom Brett. Diese Chance gibt es genau einen Zug lang.',
        },
      ],
      abschluss:
        'Zur Sicherheit noch einmal: En passant gilt nur, wenn ein Bauer zwei Felder zieht und neben deinem landet. Einen Zug später ist es vorbei.',
    },
    Lehrerhaft: {
      einleitung: 'Jetzt lernst du eine besondere Regel. Mein Bauer ist gerade zwei Felder vorgezogen und steht nun direkt neben deinem.',
      schritte: [
        {
          hinweis: 'Fast. Diese Regel gilt nur in genau diesem Moment. Zieh deinen Bauern schräg nach d6, auf das Feld hinter meinem Bauern.',
          erklaerung: 'Richtig! Du ziehst auf das Feld, das mein Bauer übersprungen hat, und nimmst ihn vom Brett. Das geht nur im Zug direkt danach.',
        },
      ],
      abschluss:
        'Merk dir zwei Bedingungen: Ein Bauer zieht zwei Felder und landet neben deinem. Dann darfst du en passant schlagen, aber nur sofort.',
    },
    Verspielt: {
      einleitung: 'Ups, mein Bauer ist zwei Felder vorgesprungen und steht jetzt neben deinem. Mal gucken, ob er so an dir vorbeikommt.',
      schritte: [
        {
          hinweis: 'Hm, auch ein Zug. Aber jetzt gibt es einen kleinen Trick: en passant! Zieh deinen Bauern schräg nach d6, hinter meinen.',
          erklaerung: 'Ha, erwischt! Du springst schräg auf das Feld, das mein Bauer übersprungen hat, und er ist weg. Das klappt nur im Zug direkt danach.',
        },
      ],
      abschluss:
        'En passant ist ein Trick mit Ablaufdatum: Ein Bauer zieht zwei Felder bis neben deinen, und du schlägst sofort. Einen Zug später ist es zu spät.',
    },
    Eitel: {
      einleitung: 'Mein Bauer schreitet zwei Felder vor, ganz elegant, direkt neben deinen. Er glaubt, er kommt so an dir vorbei.',
      schritte: [
        {
          hinweis: 'Ein Zug ohne Glanz. Jetzt geht etwas viel Eleganteres: en passant. Zieh deinen Bauern schräg nach d6, hinter meinen.',
          erklaerung: 'Bravo, mit Stil! Du ziehst schräg auf das übersprungene Feld und nimmst meinen Bauern vom Brett. Das geht nur im Zug direkt danach.',
        },
      ],
      abschluss:
        'En passant gilt nur, wenn ein Bauer zwei Felder zieht und neben deinem landet. Der Auftritt dauert genau einen Zug, dann fällt der Vorhang.',
    },
    Gemuetlich: {
      einleitung: 'Mei, mein Bauer ist zwei Felder auf einmal gelaufen und steht jetzt neben deinem. Er meint, er kommt so an dir vorbei.',
      schritte: [
        {
          hinweis: 'Ach, das geht schon, aber jetzt gibt es was Besonderes: en passant. Zieh deinen Bauern schräg nach d6, hinter meinen.',
          erklaerung: 'Passt scho! Du ziehst schräg auf das Feld, das mein Bauer übersprungen hat, und nimmst ihn weg. Das geht nur im Zug direkt danach.',
        },
      ],
      abschluss:
        'En passant gilt nur, wenn ein Bauer zwei Felder zieht und neben deinem landet. Wartest du a bisserl, ist die Gelegenheit vorbei.',
    },
  },
  rochade: {
    Aggressiv: {
      einleitung: 'Rochade: König und Turm ziehen in einem Zug. Der einzige Zug mit zwei Figuren auf einmal. Danach kann der Angriff losgehen.',
      schritte: [
        {
          hinweis: 'Nicht lange rumziehen. Der König geht zwei Felder nach rechts, nach g1, und der Turm springt über ihn.',
          erklaerung: 'Stark! König sicher hinter den Bauern, Turm auf f1 kampfbereit. Jetzt komme ich mit der langen Rochade.',
        },
      ],
      abschluss:
        'Rochieren darfst du nur, wenn König und Turm noch nie gezogen haben, alles dazwischen frei ist und dein König weder im Schach steht noch durch ein angegriffenes Feld zieht.',
    },
    Vorsichtig: {
      einleitung: 'Die Rochade bringt den König in Sicherheit. König und Turm ziehen dabei in einem Zug, als einziger Zug mit zwei Figuren.',
      schritte: [
        {
          hinweis: 'Lieber erst den König sichern. Zieh ihn zwei Felder nach rechts, nach g1. Der Turm springt dann von allein über ihn.',
          erklaerung: 'Gut so. Der König steht sicher hinter seinen Bauern, der Turm auf f1 ist bereit. Jetzt zeige ich dir die lange Rochade.',
        },
      ],
      abschluss:
        'Prüf vor der Rochade drei Dinge: König und Turm haben noch nie gezogen, dazwischen ist alles frei, und der König steht nicht im Schach und zieht über kein angegriffenes Feld.',
    },
    Lehrerhaft: {
      einleitung: 'Heute lernst du die Rochade. König und Turm ziehen dabei in einem Zug. Das ist der einzige Zug mit zwei Figuren gleichzeitig.',
      schritte: [
        {
          hinweis: 'Fast. Bei der Rochade ziehst du immer mit dem König: zwei Felder nach rechts, nach g1. Der Turm springt dann über ihn.',
          erklaerung: 'Sehr schön! Erstens steht dein König sicher hinter den Bauern, zweitens ist der Turm auf f1 bereit. Jetzt zeige ich dir die lange Rochade.',
        },
      ],
      abschluss:
        'Drei Bedingungen gelten: König und Turm haben noch nie gezogen, die Felder dazwischen sind frei, und der König steht nicht im Schach und zieht durch kein angegriffenes Feld.',
    },
    Verspielt: {
      einleitung: 'Jetzt kommt mein Lieblingszug: die Rochade. König und Turm ziehen zusammen, als einziger Zug mit zwei Figuren auf einmal.',
      schritte: [
        {
          hinweis: 'Auch witzig, aber probier mal den König: zwei Felder nach rechts, nach g1. Der Turm hüpft dann von allein über ihn.',
          erklaerung: 'Ha, schön! Der König sitzt gemütlich hinter seinen Bauern, und der Turm wartet auf f1. Jetzt zeig ich dir die lange Rochade.',
        },
      ],
      abschluss:
        'Die Rochade klappt nur, wenn König und Turm noch nie gezogen haben, dazwischen alles frei ist und dein König weder im Schach steht noch durch ein angegriffenes Feld zieht.',
    },
    Eitel: {
      einleitung: 'Die Rochade ist ein Doppelauftritt: König und Turm ziehen in einem Zug. Kein anderer Zug holt zwei Figuren zugleich auf die Bühne.',
      schritte: [
        {
          hinweis: 'Nicht doch. Bei der Rochade zieht der König: zwei Felder nach rechts, nach g1. Der Turm springt dann über ihn.',
          erklaerung: 'Bravo! Der König thront sicher hinter seinen Bauern, der Turm steht auf f1 bereit. Und jetzt meine lange Rochade, bitte Applaus.',
        },
      ],
      abschluss:
        'Die Rochade gibt es nur, wenn König und Turm noch nie gezogen haben, die Felder dazwischen frei sind und dein König weder im Schach steht noch durch ein angegriffenes Feld zieht.',
    },
    Gemuetlich: {
      einleitung: 'Jetzt kommt die Rochade. Da ziehen König und Turm in einem Zug. Das ist der einzige Zug, wo zwei Figuren auf einmal ziehen.',
      schritte: [
        {
          hinweis: 'Ach, nimm lieber den König. Zwei Felder nach rechts, nach g1, dann springt der Turm von allein drüber.',
          erklaerung: 'Schön! Der König sitzt sicher hinter seinen Bauern wie hinterm Ofen, und der Turm ist auf f1 bereit. Jetzt zeig ich dir die lange Rochade.',
        },
      ],
      abschluss:
        'Rochieren darfst du nur, wenn König und Turm noch nie gezogen haben, nix dazwischen steht und dein König nicht im Schach steht oder durch ein angegriffenes Feld zieht.',
    },
  },
  umwandlung: {
    Aggressiv: {
      einleitung: 'Dein Bauer ist fast durch. Erreicht ein Bauer die letzte Reihe, wird er zu einer neuen Figur. Hol dir die stärkste!',
      schritte: [
        {
          hinweis: 'Erlaubt, aber zu brav. Die Dame ist fast immer die stärkste Wahl. Noch einmal!',
          erklaerung: 'Eine neue Dame, das gibt Druck! Du darfst sogar zwei Damen haben, auch wenn die erste noch steht.',
        },
      ],
      abschluss:
        'Du wählst zwischen Dame, Turm, Läufer und Springer. Fast immer die Dame, nur selten bringt ein Springer mehr Angriff.',
    },
    Vorsichtig: {
      einleitung: 'Dein Bauer ist fast am Ziel. Erreicht ein Bauer die letzte Reihe, wird er zu einer anderen Figur. Das gefällt mir gar nicht.',
      schritte: [
        {
          hinweis: 'Erlaubt, aber nicht die sicherste Wahl. Die Dame ist fast immer am stärksten. Versuch es noch einmal.',
          erklaerung: 'Richtig, eine neue Dame. Du darfst auch eine zweite Dame haben, wenn die erste noch steht. Das sind acht Punkte mehr Material.',
        },
      ],
      abschluss:
        'Du kannst Dame, Turm, Läufer oder Springer wählen. Meistens ist die Dame richtig, nur in seltenen Fällen der Springer.',
    },
    Lehrerhaft: {
      einleitung: 'Merk dir diese Regel: Kommt ein Bauer auf der letzten Reihe an, wird er zu einer anderen Figur. Dein Bauer ist fast am Ziel.',
      schritte: [
        {
          hinweis: 'Ein erlaubter Zug, aber nicht der beste. Die Dame ist fast immer die stärkste Wahl. Versuch es noch einmal.',
          erklaerung: 'Sehr gut, eine neue Dame! Übrigens darfst du auch eine zweite Dame haben, selbst wenn die erste noch steht.',
        },
      ],
      abschluss:
        'Zur Wahl stehen Dame, Turm, Läufer und Springer. In fast jeder Stellung nimmst du die Dame, nur selten ist ein Springer besser.',
    },
    Verspielt: {
      einleitung: 'Ui, dein Bauer ist fast am Ziel. Kommt ein Bauer auf der letzten Reihe an, verwandelt er sich in eine andere Figur. Wie Zauberei!',
      schritte: [
        {
          hinweis: 'Lustige Wahl! Aber die Dame ist fast immer die stärkste. Probier es noch einmal.',
          erklaerung: 'Eine neue Dame! Du darfst sogar zwei Damen haben, auch wenn die erste noch steht. Verrückt, oder?',
        },
      ],
      abschluss:
        'Du kannst Dame, Turm, Läufer oder Springer nehmen. Meistens nimmst du die Dame, nur ganz selten ist ein Springer die schlauere Überraschung.',
    },
    Eitel: {
      einleitung: 'Dein Bauer steht kurz vor seinem großen Auftritt. Erreicht ein Bauer die letzte Reihe, verwandelt er sich in eine andere Figur.',
      schritte: [
        {
          hinweis: 'Erlaubt, aber wenig glanzvoll. Die Dame ist fast immer die stärkste Wahl. Versuch es noch einmal.',
          erklaerung: 'Bravo, eine neue Dame! Zwei Damen sind erlaubt, auch wenn die erste noch steht. Sehr elegant.',
        },
      ],
      abschluss:
        'Zur Auswahl stehen Dame, Turm, Läufer und Springer. Meistens bekommt die Dame die Rolle, nur selten ist ein Springer die bessere Besetzung.',
    },
    Gemuetlich: {
      einleitung: 'Schau, dein Bauer ist fast am Ziel. Kommt ein Bauer auf der letzten Reihe an, wird er zu einer anderen Figur. Schön, gell?',
      schritte: [
        {
          hinweis: 'Mei, das geht auch, aber die Dame ist fast immer die beste Wahl. Probier es noch einmal.',
          erklaerung: 'Ja schau, eine neue Dame! Du darfst sogar eine zweite Dame haben, auch wenn die erste noch steht.',
        },
      ],
      abschluss:
        'Du kannst Dame, Turm, Läufer oder Springer nehmen. Meistens nimmst du die Dame, nur ganz selten ist ein Springer a bisserl besser.',
    },
  },
  grundreihenmatt: {
    Aggressiv: {
      einleitung: 'Mein König auf g8 steckt hinter drei Bauern fest und hat kein Fluchtfeld nach vorn. Ein Angriff auf der Grundreihe wäre bitter für mich.',
      schritte: [
        {
          hinweis: 'Da geht mehr. Schau auf die achte Reihe: Dein Turm kann bis ganz nach hinten durchziehen.',
          erklaerung: 'Matt, volle Wucht! Mein König kommt nicht nach vorn, seine eigenen Bauern blockieren ihn, und dein Turm deckt die Reihe.',
        },
      ],
      abschluss:
        'Gib deinem König früh ein Luftloch, etwa mit dem Bauern h2 nach h3. Dann läuft ein Turmangriff auf der Grundreihe ins Leere.',
    },
    Vorsichtig: {
      einleitung: 'Mein König auf g8 steht hinter drei Bauern und hat kein Fluchtfeld nach vorn. Das ist nicht sicher, das weiß ich.',
      schritte: [
        {
          hinweis: 'Schau genau hin: die achte Reihe. Dein Turm kann bis ganz nach hinten ziehen.',
          erklaerung: 'Matt. Mein König kann nicht nach vorn, weil seine eigenen Bauern im Weg stehen, und dein Turm deckt die Reihe. Das hätte ich absichern müssen.',
        },
      ],
      abschluss:
        'Sicher ist sicher: Gib deinem König früh ein Luftloch, etwa mit dem Bauern h2 nach h3. Dann kann ihn kein Turm auf der Grundreihe matt setzen.',
    },
    Lehrerhaft: {
      einleitung: 'Schau dir meinen König auf g8 genau an. Vor ihm stehen drei Bauern, er hat kein Fluchtfeld nach vorn. Das Bild kommt in Partien oft vor.',
      schritte: [
        {
          hinweis: 'Gute Idee, aber denk an die achte Reihe. Dein Turm kann bis ganz nach hinten ziehen.',
          erklaerung: 'Matt! Mein König kann nicht nach vorn, weil seine eigenen Bauern im Weg stehen, und dein Turm deckt die ganze Reihe.',
        },
      ],
      abschluss:
        'Merk dir für deine Partien: Gib dem König früh ein Luftloch, zum Beispiel mit dem Bauern h2 nach h3. Dann ist die Grundreihe kein Problem.',
    },
    Verspielt: {
      einleitung: 'Ups, mein König auf g8 hat sich hinter drei Bauern eingesperrt. Nach vorn kann er nicht flüchten. Mal sehen, ob du das merkst.',
      schritte: [
        {
          hinweis: 'Knapp daneben. Schau mal auf die achte Reihe. Dein Turm kann bis ganz nach hinten sausen.',
          erklaerung: 'Matt! Selbst eingesperrt, typisch ich. Mein König kommt nicht nach vorn, weil seine eigenen Bauern im Weg sind, und dein Turm deckt die Reihe.',
        },
      ],
      abschluss:
        'Gib deinem König früh ein Luftloch, zum Beispiel mit dem Bauern h2 nach h3. Dann kann ihn kein Turm auf der Grundreihe überraschen.',
    },
    Eitel: {
      einleitung: 'Mein König auf g8 sitzt hinter drei Bauern wie in einer Loge, aber ohne Notausgang nach vorn. Ein Schönheitsfehler, zugegeben.',
      schritte: [
        {
          hinweis: 'Nicht ganz brillant. Schau auf die achte Reihe: Dein Turm kann bis ganz nach hinten ziehen.',
          erklaerung: 'Matt! Ein starker Schlussakt. Mein König kann nicht nach vorn, weil seine eigenen Bauern im Weg stehen, und dein Turm deckt die Reihe.',
        },
      ],
      abschluss:
        'Gib deinem König früh ein Luftloch, zum Beispiel mit dem Bauern h2 nach h3. Dann blamiert ihn kein Turm auf der Grundreihe.',
    },
    Gemuetlich: {
      einleitung: 'Schau dir meinen König auf g8 an. Vor ihm stehen drei Bauern, und nach vorn kann er nirgends hin. Mei, das ist gefährlich für mich.',
      schritte: [
        {
          hinweis: 'Ach, schau lieber auf die achte Reihe. Dein Turm kann bis ganz nach hinten ziehen.',
          erklaerung: 'Matt! Na gut, mein König kann nicht nach vorn, weil seine eigenen Bauern im Weg stehen, und dein Turm deckt die Reihe.',
        },
      ],
      abschluss:
        'Gib deinem König früh ein Luftloch, zum Beispiel mit dem Bauern h2 nach h3. Dann hat er a bisserl Platz, und kein Turm setzt ihn auf der Grundreihe matt.',
    },
  },
  treppenmatt: {
    Aggressiv: {
      einleitung: 'Zwei Türme gegen meinen König allein, das wird eine Jagd. Die Türme gehen wie eine Treppe: einer sperrt eine Reihe, der andere gibt Schach.',
      schritte: [
        {
          hinweis: 'Erst einsperren, dann zuschlagen. Zieh den Turm von b2 nach b6 und sperr die ganze Reihe.',
          erklaerung: 'Gut, mein König kommt nicht mehr nach unten. Er hängt auf der siebten oder achten Reihe fest.',
        },
        {
          hinweis: 'Jetzt der andere Turm. Schach auf der siebten Reihe, von a7 aus!',
          erklaerung: 'Schach! Mein König muss auf die letzte Reihe, die sechste sperrt dein anderer Turm.',
        },
        {
          hinweis: 'Letzter Schlag: Der Turm auf b6 geht auf die achte Reihe und gibt Schach.',
          erklaerung: 'Matt! Ein Turm deckt die siebte Reihe, der andere gibt auf der achten Schach. Jagd vorbei.',
        },
      ],
      abschluss:
        'Halte deine Türme weit genug vom König weg, sonst schlägt er einen. Kommt er zu nah, ziehst du den Turm ans andere Ende der Reihe und jagst weiter.',
    },
    Vorsichtig: {
      einleitung: 'Mit zwei Türmen setzt du einen König allein sicher matt. Die Türme arbeiten wie eine Treppe: einer sperrt eine Reihe, der andere gibt Schach.',
      schritte: [
        {
          hinweis: 'Schritt für Schritt. Erst sperrt ein Turm eine ganze Reihe: Zieh den Turm von b2 nach b6.',
          erklaerung: 'Gut. Mein König kann nicht mehr nach unten und bleibt auf der siebten oder achten Reihe.',
        },
        {
          hinweis: 'Jetzt der andere Turm, in Ruhe. Er gibt Schach auf der siebten Reihe, von a7 aus.',
          erklaerung: 'Schach. Mein König muss auf die letzte Reihe, denn die sechste sperrt dein anderer Turm.',
        },
        {
          hinweis: 'Der Turm auf b6 steht weit genug weg. Er zieht jetzt auf die achte Reihe und gibt Schach.',
          erklaerung: 'Matt. Ein Turm deckt die siebte Reihe, der andere gibt Schach auf der achten. Sauber gemacht.',
        },
      ],
      abschluss:
        'Halte deine Türme weit genug vom König weg, sonst schlägt er einen. Kommt er zu nah, ziehst du den Turm in aller Ruhe ans andere Ende der Reihe.',
    },
    Lehrerhaft: {
      einleitung: 'Jetzt lernst du das Treppenmatt: Mit zwei Türmen setzt du einen König allein matt. Ein Turm sperrt eine Reihe, der andere gibt Schach.',
      schritte: [
        {
          hinweis: 'Erstens: Ein Turm sperrt eine ganze Reihe. Zieh den Turm von b2 nach b6.',
          erklaerung: 'Richtig. Jetzt kann mein König nicht mehr nach unten. Er muss auf der siebten oder achten Reihe bleiben.',
        },
        {
          hinweis: 'Zweitens: Der andere Turm gibt Schach auf der siebten Reihe, von a7 aus.',
          erklaerung: 'Schach! Mein König hat nur noch die letzte Reihe, denn die sechste sperrt dein anderer Turm.',
        },
        {
          hinweis: 'Drittens: Der Turm auf b6 zieht auf die achte Reihe und gibt Schach.',
          erklaerung: 'Matt! Ein Turm deckt die siebte Reihe, der andere gibt Schach auf der achten. Genau so geht die Treppe.',
        },
      ],
      abschluss:
        'Wichtig: Halte die Türme weit genug vom König weg, sonst schlägt er einen. Kommt er zu nah, ziehst du den Turm ans andere Ende der Reihe.',
    },
    Verspielt: {
      einleitung: 'Zwei Türme gegen meinen König allein, oje. Die Türme laufen wie eine Treppe: einer sperrt eine Reihe, der andere gibt Schach.',
      schritte: [
        {
          hinweis: 'Hm, nicht ganz. Ein Turm soll erst mal eine ganze Reihe sperren. Zieh den Turm von b2 nach b6.',
          erklaerung: 'Oha, jetzt komm ich nicht mehr nach unten. Mein König muss auf der siebten oder achten Reihe bleiben.',
        },
        {
          hinweis: 'Jetzt darf der andere Turm mitspielen. Er gibt Schach auf der siebten Reihe, von a7 aus.',
          erklaerung: 'Schach! Na toll, mein König muss auf die letzte Reihe, denn die sechste sperrt dein anderer Turm.',
        },
        {
          hinweis: 'Fast geschafft. Der Turm auf b6 kann jetzt auf die achte Reihe hüpfen und Schach geben.',
          erklaerung: 'Matt! Ganz oben auf der Treppe ist Schluss. Ein Turm deckt die siebte Reihe, der andere gibt Schach auf der achten.',
        },
      ],
      abschluss:
        'Lass deine Türme nicht zu nah an den König, sonst schnappt er sich einen. Kommt er zu nah, ziehst du den Turm einfach ans andere Ende der Reihe.',
    },
    Eitel: {
      einleitung: 'Das Treppenmatt ist eine Choreografie für zwei Türme: einer sperrt eine Reihe, der andere gibt Schach. So fällt ein König allein.',
      schritte: [
        {
          hinweis: 'Nicht ganz. Ein Turm soll zuerst eine ganze Reihe sperren: Zieh den Turm von b2 nach b6.',
          erklaerung: 'Schön gesetzt. Mein König kann nicht mehr nach unten und muss auf der siebten oder achten Reihe bleiben.',
        },
        {
          hinweis: 'Jetzt glänzt der zweite Turm: Er gibt Schach auf der siebten Reihe, von a7 aus.',
          erklaerung: 'Schach! Mein König tritt ab auf die letzte Reihe, denn die sechste sperrt dein anderer Turm.',
        },
        {
          hinweis: 'Das Finale: Der Turm auf b6 zieht auf die achte Reihe und gibt Schach.',
          erklaerung: 'Matt, Vorhang! Ein Turm deckt die siebte Reihe, der andere gibt Schach auf der achten.',
        },
      ],
      abschluss:
        'Halte deine Türme weit genug vom König weg, sonst schlägt er einen. Kommt er zu nah, wechselt der Turm einfach ans andere Ende der Reihe.',
    },
    Gemuetlich: {
      einleitung: 'Mit zwei Türmen setzt du einen König allein matt, ganz gemütlich. Die gehen wie eine Treppe: einer sperrt eine Reihe, der andere gibt Schach.',
      schritte: [
        {
          hinweis: 'Ach, erst sperrt ein Turm eine ganze Reihe. Zieh den Turm von b2 nach b6.',
          erklaerung: 'Na gut, jetzt kann mein König nicht mehr nach unten. Er muss auf der siebten oder achten Reihe bleiben.',
        },
        {
          hinweis: 'Jetzt ist der andere Turm dran. Der gibt Schach auf der siebten Reihe, von a7 aus.',
          erklaerung: 'Schach! Jetzt muss mein König auf die letzte Reihe, denn die sechste sperrt dein anderer Turm.',
        },
        {
          hinweis: 'Jetzt noch der Turm auf b6: auf die achte Reihe und Schach geben, dann passt das.',
          erklaerung: 'Matt! Ein Turm deckt die siebte Reihe, der andere gibt Schach auf der achten. Schön Schritt für Schritt.',
        },
      ],
      abschluss:
        'Lass deine Türme nicht zu nah an den König, sonst schlägt er einen. Kommt er zu nah, ziehst du den Turm halt ans andere Ende der Reihe.',
    },
  },
  springergabel: {
    Aggressiv: {
      einleitung: 'Ein Springer kann zwei Figuren auf einmal angreifen, die Gabel. Richtig gefährlich wird sie, wenn der König dabei ist.',
      schritte: [
        {
          hinweis: 'Da steckt mehr drin. Such das Feld, von dem dein Springer g8 und c8 zugleich trifft. Probier e7.',
          erklaerung: 'Gabel, autsch! Mein König steht im Schach und muss weg. Meine Dame auf c8 ist verloren.',
        },
        {
          hinweis: 'Hol dir die Beute: Dein Springer greift noch meine Dame auf c8 an. Schlag sie.',
          erklaerung: 'Die Dame gehört dir. Ein Springerzug gegen die stärkste Figur, so gewinnt man Partien.',
        },
      ],
      abschluss:
        'Schau bei jedem Zug, ob dein Springer zwei Figuren gleichzeitig angreifen kann. Und denk daran, dass ich dasselbe mit dir vorhabe.',
    },
    Vorsichtig: {
      einleitung: 'Ein Springer kann zwei Figuren auf einmal angreifen. Das heißt Gabel. Am gefährlichsten ist sie, wenn der König dabei ist.',
      schritte: [
        {
          hinweis: 'In Ruhe suchen: ein Feld, von dem dein Springer g8 und c8 zugleich erreicht. Probier e7.',
          erklaerung: 'Gabel. Mein König steht im Schach und muss weg, meine Dame auf c8 kann ich nicht mehr retten. Das hätte ich prüfen sollen.',
        },
        {
          hinweis: 'Dein Springer greift noch meine Dame auf c8 an, und niemand deckt sie. Schlag sie.',
          erklaerung: 'Die Dame gehört dir. Neun Punkte für einen Springerzug, das ist eine gute Rechnung.',
        },
      ],
      abschluss:
        'Prüf bei jedem Zug, ob dein Springer zwei Figuren gleichzeitig angreifen kann. Und prüf genauso, ob dir dasselbe passieren kann.',
    },
    Lehrerhaft: {
      einleitung: 'Heute geht es um die Gabel: Ein Springer greift zwei Figuren auf einmal an. Am stärksten ist sie, wenn eine davon der König ist.',
      schritte: [
        {
          hinweis: 'Fast. Suche ein Feld, von dem dein Springer g8 und c8 zugleich erreicht. Probier e7.',
          erklaerung: 'Gabel! Merk dir: Mein König steht im Schach und muss zuerst weg. Darum kann ich meine Dame auf c8 nicht mehr retten.',
        },
        {
          hinweis: 'Jetzt kommt der Lohn: Dein Springer greift noch meine Dame auf c8 an. Schlag sie.',
          erklaerung: 'Sehr gut, die Dame gehört dir. Für einen Springerzug hast du die stärkste Figur bekommen.',
        },
      ],
      abschluss:
        'Schau bei jedem Zug, ob dein Springer zwei Figuren gleichzeitig angreifen kann. Und prüf auch, ob dir dasselbe passieren kann.',
    },
    Verspielt: {
      einleitung: 'Springer sind meine Lieblinge, die greifen zwei Figuren auf einmal an. Das heißt Gabel. Am fiesesten ist sie, wenn der König dabei ist.',
      schritte: [
        {
          hinweis: 'Hm, nicht ganz. Such ein Feld, von dem dein Springer g8 und c8 zugleich erreicht. Probier mal e7.',
          erklaerung: 'Gabel, oh nein! Mein König steht im Schach und muss weg. Tschüss, Dame auf c8.',
        },
        {
          hinweis: 'Na los, dein Springer greift noch meine Dame auf c8 an. Schlag sie.',
          erklaerung: 'Die Dame gehört dir. Ein kleiner Springer schnappt sich die stärkste Figur, herrlich.',
        },
      ],
      abschluss:
        'Schau bei jedem Zug, ob dein Springer zwei Figuren gleichzeitig angreifen kann. Und guck, dass dir nicht dasselbe passiert.',
    },
    Eitel: {
      einleitung: 'Die Gabel ist der eleganteste Trick des Springers: zwei Figuren auf einmal angreifen. Am schönsten, wenn der König mitspielt.',
      schritte: [
        {
          hinweis: 'Noch nicht brillant. Suche ein Feld, von dem dein Springer g8 und c8 zugleich erreicht. Probier e7.',
          erklaerung: 'Gabel, zugegeben, brillant. Mein König steht im Schach und muss weg. Meine Dame auf c8 verlässt die Bühne.',
        },
        {
          hinweis: 'Mein König ist weg, jetzt bleibt die Dame: Dein Springer greift sie auf c8 an. Schlag sie.',
          erklaerung: 'Die Dame gehört dir. Für einen Springer die Hauptdarstellerin, das gibt Applaus.',
        },
      ],
      abschluss:
        'Schau bei jedem Zug, ob dein Springer zwei Figuren gleichzeitig angreifen kann, so wie ich. Und achte darauf, dass dir nicht dasselbe passiert.',
    },
    Gemuetlich: {
      einleitung: 'Ein Springer kann zwei Figuren auf einmal angreifen. Das heißt Gabel, gell. Am stärksten ist sie, wenn der König dabei ist.',
      schritte: [
        {
          hinweis: 'Ach, such lieber ein Feld, von dem dein Springer g8 und c8 zugleich erreicht. Probier e7.',
          erklaerung: 'Gabel! Ach je, mein König steht im Schach und muss weg. Meine Dame auf c8 kann ich nicht mehr retten.',
        },
        {
          hinweis: 'Dein Springer greift noch meine Dame auf c8 an. Schlag sie ruhig.',
          erklaerung: 'Na gut, die Dame gehört dir. Für einen Springer die stärkste Figur, nicht schlecht.',
        },
      ],
      abschluss:
        'Schau bei jedem Zug, ob dein Springer zwei Figuren gleichzeitig angreifen kann. Und schau, dass dir nicht dasselbe passiert.',
    },
  },
  italienisch: {
    Aggressiv: {
      einleitung: 'In der Eröffnung zählt Tempo: Mitte besetzen, Springer und Läufer raus, König in Sicherheit. Dann kann der Angriff kommen.',
      schritte: [
        {
          hinweis: 'Zu langsam. Rein in die Mitte: Zieh den Bauern von e2 nach e4.',
          erklaerung: 'Gut, dein Bauer beherrscht die Mitte und macht den Weg frei für Läufer und Dame. Ich halte sofort dagegen, mit demselben Zug.',
        },
        {
          hinweis: 'Springer zuerst, die greifen an. Zieh den Springer von g1 nach f3.',
          erklaerung: 'Stark, dein Springer greift meinen Bauern auf e5 an. Ich decke ihn mit meinem Springer.',
        },
        {
          hinweis: 'Der Läufer auf f1 will in den Kampf. Zieh ihn nach c4.',
          erklaerung: 'Die Italienische Partie. Dein Läufer zielt auf f7, den schwächsten Punkt neben meinem König. Gut gezielt.',
        },
        {
          hinweis: 'Jetzt ist der Weg frei. Rochade, König nach g1, dann kann es losgehen.',
          erklaerung: 'Perfekt! Mitte besetzt, zwei Figuren raus, König sicher. Jetzt bist du bereit für den Angriff.',
        },
      ],
      abschluss:
        'Für jede Eröffnung gilt: Mitte, Figuren raus, Rochade. Nicht zu früh mit der Dame los und nicht zweimal mit derselben Figur ziehen.',
    },
    Vorsichtig: {
      einleitung: 'In der Eröffnung legst du die Grundlage: Mitte besetzen, Springer und Läufer ins Spiel bringen und den König in Sicherheit bringen.',
      schritte: [
        {
          hinweis: 'Ein solider Anfang ist ein Bauer in der Mitte. Zieh den Bauern von e2 nach e4.',
          erklaerung: 'Gut. Dein Bauer kontrolliert die Mitte und macht Platz für Läufer und Dame. Ich mache vorsichtshalber dasselbe.',
        },
        {
          hinweis: 'Springer kommen zuerst ins Spiel, das ist bewährt. Zieh den Springer von g1 nach f3.',
          erklaerung: 'Richtig, dein Springer greift meinen Bauern auf e5 an. Ich decke ihn sofort mit meinem Springer.',
        },
        {
          hinweis: 'Der Läufer auf f1 steht noch zu Hause. Zieh ihn nach c4.',
          erklaerung: 'Das ist die Italienische Partie. Dein Läufer zielt auf f7, den schwächsten Punkt neben meinem König. Den behalte ich im Blick.',
        },
        {
          hinweis: 'Jetzt ist der Weg für die Rochade frei. Zieh den König nach g1, dann steht er geschützt.',
          erklaerung: 'Gut gemacht. Mitte besetzt, zwei Figuren entwickelt, König sicher. So sieht eine solide Eröffnung aus.',
        },
      ],
      abschluss:
        'Merk dir für jede Eröffnung: Mitte, Figuren raus, Rochade. Lieber nicht zu früh mit der Dame und nicht zweimal mit derselben Figur ziehen.',
    },
    Lehrerhaft: {
      einleitung: 'Die Eröffnung hat drei Ziele: Erstens die Mitte besetzen, zweitens Springer und Läufer entwickeln, drittens den König sichern.',
      schritte: [
        {
          hinweis: 'Erstens die Mitte. Fang mit einem Bauern an: Zieh den Bauern von e2 nach e4.',
          erklaerung: 'Richtig. Dein Bauer kontrolliert die Mitte und macht Platz für Läufer und Dame. Ich mache dasselbe, das ist ein guter Grundsatz.',
        },
        {
          hinweis: 'Zweitens: Springer kommen vor den Läufern ins Spiel. Zieh den Springer von g1 nach f3.',
          erklaerung: 'Sehr gut, dein Springer greift meinen Bauern auf e5 an. Ich decke ihn mit meinem Springer.',
        },
        {
          hinweis: 'Jetzt der Läufer: Der auf f1 will ins Spiel. Zieh ihn nach c4.',
          erklaerung: 'Diese Eröffnung heißt Italienische Partie. Dein Läufer zielt auf f7, den schwächsten Punkt neben meinem König.',
        },
        {
          hinweis: 'Drittens der König. Der Weg für die Rochade ist frei: Zieh den König nach g1.',
          erklaerung: 'Ausgezeichnet! Mitte besetzt, zwei Figuren entwickelt, König sicher. So sieht eine gute Eröffnung aus.',
        },
      ],
      abschluss:
        'Für jede Eröffnung gilt dieselbe Reihenfolge: Mitte, Figuren raus, Rochade. Zieh nicht zu früh mit der Dame und nicht zweimal mit derselben Figur.',
    },
    Verspielt: {
      einleitung: 'Eröffnung, juhu! Drei Ziele: die Mitte besetzen, Springer und Läufer rausholen und den König in Sicherheit bringen. Heute bin ich brav.',
      schritte: [
        {
          hinweis: 'Hm, lieber erst in die Mitte. Zieh den Bauern von e2 nach e4.',
          erklaerung: 'Schön, dein Bauer hat die Mitte und macht Platz für Läufer und Dame. Ich mach einfach dasselbe, ganz ohne Gambit.',
        },
        {
          hinweis: 'Springer kommen zuerst raus. Zieh den Springer von g1 nach f3.',
          erklaerung: 'Oha, dein Springer greift meinen Bauern auf e5 an. Ich decke ihn schnell mit meinem Springer.',
        },
        {
          hinweis: 'Der Läufer auf f1 langweilt sich. Zieh ihn nach c4.',
          erklaerung: 'Das ist die Italienische Partie. Dein Läufer zielt auf f7, den schwächsten Punkt neben meinem König. Das wird spannend.',
        },
        {
          hinweis: 'Jetzt ist der Weg frei für die Rochade. Ab mit dem König nach g1.',
          erklaerung: 'Perfekt! Mitte besetzt, zwei Figuren draußen, König sicher. So geht eine gute Eröffnung, sogar bei mir.',
        },
      ],
      abschluss:
        'Für jede Eröffnung: Mitte, Figuren raus, Rochade. Nicht zu früh mit der Dame losflitzen und nicht zweimal mit derselben Figur ziehen.',
    },
    Eitel: {
      einleitung: 'Die Eröffnung ist der erste Akt mit drei Aufgaben: die Mitte besetzen, Springer und Läufer auf die Bühne holen und den König sichern.',
      schritte: [
        {
          hinweis: 'Kein guter Auftakt. Zuerst ein Bauer in die Mitte: Zieh den Bauern von e2 nach e4.',
          erklaerung: 'Gut, dein Bauer beherrscht die Mitte und macht Platz für Läufer und Dame. Ich spiele dasselbe, nur eleganter.',
        },
        {
          hinweis: 'Springer zuerst, das hat Stil. Zieh den Springer von g1 nach f3.',
          erklaerung: 'Hübsch, dein Springer greift meinen Bauern auf e5 an. Ich decke ihn mit meinem Springer, ganz souverän.',
        },
        {
          hinweis: 'Der Läufer auf f1 steht noch untätig herum. Zieh ihn nach c4.',
          erklaerung: 'Die Italienische Partie, ein Klassiker. Dein Läufer zielt auf f7, den schwächsten Punkt neben meinem König.',
        },
        {
          hinweis: 'Jetzt ist die Bühne frei für die Rochade. Zieh den König nach g1.',
          erklaerung: 'Perfekt! Mitte besetzt, zwei Figuren entwickelt, König sicher. Fast so elegant wie bei mir.',
        },
      ],
      abschluss:
        'Für jede Eröffnung gilt: Mitte, Figuren raus, Rochade. Die Dame tritt nicht zu früh auf, und keine Figur zieht zweimal.',
    },
    Gemuetlich: {
      einleitung: 'Bei der Eröffnung merk ich mir drei Sachen: die Mitte besetzen, Springer und Läufer rausbringen und den König in Sicherheit bringen.',
      schritte: [
        {
          hinweis: 'Ach, fang lieber mit einem Bauern in der Mitte an. Zieh den Bauern von e2 nach e4.',
          erklaerung: 'Passt scho, dein Bauer kontrolliert jetzt die Mitte und macht Platz für Läufer und Dame. Ich mache dasselbe.',
        },
        {
          hinweis: 'Springer kommen zuerst ins Spiel. Zieh den Springer von g1 nach f3, in aller Ruhe.',
          erklaerung: 'Schön, dein Springer greift meinen Bauern auf e5 an. Na gut, ich decke ihn mit meinem Springer.',
        },
        {
          hinweis: 'Der Läufer auf f1 will auch mitspielen. Zieh ihn nach c4.',
          erklaerung: 'Das ist die Italienische Partie. Dein Läufer zielt auf f7, den schwächsten Punkt neben meinem König. Da muss ich aufpassen.',
        },
        {
          hinweis: 'Jetzt ist der Weg frei für die Rochade. Zieh den König nach g1, dann hat er seine Ruhe.',
          erklaerung: 'Wunderbar! Mitte besetzt, zwei Figuren entwickelt, König sicher. So sieht eine gute Eröffnung aus.',
        },
      ],
      abschluss:
        'Merk dir für jede Eröffnung: Mitte, Figuren raus, Rochade. Nicht zu früh mit der Dame und nicht zweimal mit derselben Figur, dann passt das.',
    },
  },
  'schaefermatt-abwehren': {
    Aggressiv: {
      einleitung: 'Heute spielst du Schwarz, und ich greife sofort an: Schäfermatt, mein Lieblingstrick. Mal sehen, ob du ihn durchschaust.',
      schritte: [
        {
          hinweis: 'Nicht so zaghaft. Stell dich mir in der Mitte entgegen: Zieh den Bauern von e7 nach e5.',
          erklaerung: 'Gut. Jetzt kommt mein Läufer nach c4, mit Blick auf dein Feld f7. Der Angriff läuft.',
        },
        {
          hinweis: 'Erst die Figuren raus: Bring einen Springer ins Spiel, nach c6.',
          erklaerung: 'Ordentlich. Jetzt wird es scharf: Meine Dame stürmt nach h5. Dame und Läufer greifen beide f7 an.',
        },
        {
          hinweis: 'Ich drohe Dame schlägt f7, matt! Zwei gute Abwehren: Bauer g7 nach g6 greift meine Dame an, Dame d8 nach e7 deckt f7.',
          erklaerung: 'Abgewehrt, schade! Das Matt auf f7 klappt so nicht. Dann greife ich eben noch einmal von f3 aus an.',
        },
        {
          hinweis: 'Der Springer auf g8 kann sich vor f7 stellen. Zieh ihn nach f6, schnell.',
          erklaerung: 'Stark verteidigt! Dein Springer sperrt die Linie und ist dabei noch entwickelt. Mein Angriff ist verpufft.',
        },
      ],
      abschluss:
        'Kommt die Dame früh raus, schau sofort auf f7 (als Weiß auf f2). Deck das Feld oder versperr den Weg, und hol dabei deine Figuren raus.',
    },
    Vorsichtig: {
      einleitung: 'Heute spielst du Schwarz. Ich probiere ausnahmsweise etwas Riskantes, das Schäfermatt. Ob du es durchschaust?',
      schritte: [
        {
          hinweis: 'Erst die Mitte sichern. Zieh den Bauern von e7 nach e5.',
          erklaerung: 'Gut. Jetzt bringe ich meinen Läufer nach c4. Er zielt auf dein Feld f7, das nur dein König deckt.',
        },
        {
          hinweis: 'Bring zuerst einen Springer ins Spiel, nach c6. Das ist solide.',
          erklaerung: 'Ordentlich. Jetzt kommt meine Dame nach h5. Zwei Angreifer auf f7, nur ein Verteidiger.',
        },
        {
          hinweis: 'Ich drohe Dame schlägt f7, matt. Zwei sichere Abwehren: Bauer g7 nach g6 greift meine Dame an, Dame d8 nach e7 deckt f7.',
          erklaerung: 'Abgewehrt. Das Matt auf f7 geht so nicht mehr. Ich versuche es noch einmal von f3 aus, wenn auch ungern.',
        },
        {
          hinweis: 'Der Springer auf g8 kann sich sicher vor f7 stellen. Zieh ihn nach f6.',
          erklaerung: 'Sehr solide. Dein Springer sperrt die Linie und ist gleichzeitig entwickelt. Mein Trick hat nicht funktioniert, wie befürchtet.',
        },
      ],
      abschluss:
        'Kommt die Dame früh heraus, prüf immer f7 (als Weiß f2). Deck das Feld oder versperr den Weg, und entwickle dabei deine Figuren.',
    },
    Lehrerhaft: {
      einleitung: 'Heute spielst du Schwarz. Ich zeige dir einen bekannten Trick, das Schäfermatt, damit du ihn später sofort erkennst.',
      schritte: [
        {
          hinweis: 'Erstens die Mitte: Stell dich mir entgegen und zieh den Bauern von e7 nach e5.',
          erklaerung: 'Richtig. Jetzt bringe ich meinen Läufer nach c4. Achte darauf, dass er auf dein Feld f7 zielt.',
        },
        {
          hinweis: 'Zweitens die Figuren: Bring zuerst einen Springer ins Spiel, nach c6.',
          erklaerung: 'Gut. Jetzt kommt meine Dame nach h5. Merk dir das Muster: Dame und Läufer greifen beide f7 an.',
        },
        {
          hinweis: 'Ich drohe Dame schlägt f7, matt. Es gibt zwei gute Abwehren: Bauer g7 nach g6 greift meine Dame an, Dame d8 nach e7 deckt f7.',
          erklaerung: 'Abgewehrt! Das Matt auf f7 klappt jetzt nicht mehr. Ich versuche es noch einmal von f3 aus.',
        },
        {
          hinweis: 'Der Springer auf g8 kann sich vor f7 stellen. Zieh ihn nach f6, dann verteidigt und entwickelt er zugleich.',
          erklaerung: 'Sehr gut! Dein Springer sperrt die Linie und ist gleichzeitig entwickelt. Abwehr und Entwicklung in einem Zug, so soll es sein.',
        },
      ],
      abschluss:
        'Kommt die Dame früh heraus, achte auf f7 (als Weiß auf f2). Deck das Feld oder versperr den Weg, und entwickle dabei deine Figuren.',
    },
    Verspielt: {
      einleitung: 'Heute spielst du Schwarz. Ich probier einen frechen Trick, das Schäfermatt. Mal gucken, ob du mich erwischst.',
      schritte: [
        {
          hinweis: 'Hm, lieber in die Mitte. Zieh den Bauern von e7 nach e5.',
          erklaerung: 'Gut. Jetzt flitzt mein Läufer nach c4. Er schielt auf dein Feld f7.',
        },
        {
          hinweis: 'Hol erst mal einen Springer raus, nach c6.',
          erklaerung: 'Ordentlich. Jetzt kommt der Spaß: Meine Dame hüpft nach h5. Dame und Läufer greifen beide f7 an.',
        },
        {
          hinweis: 'Uiuiui, ich drohe Dame schlägt f7, matt! Zwei gute Abwehren: Bauer g7 nach g6 greift meine Dame an, Dame d8 nach e7 deckt f7.',
          erklaerung: 'Ups, abgewehrt! Das Matt auf f7 klappt so nicht. Ich probier es frech noch einmal von f3 aus.',
        },
        {
          hinweis: 'Schnell, der Springer auf g8 kann sich vor f7 stellen. Zieh ihn nach f6.',
          erklaerung: 'Ha, erwischt! Dein Springer sperrt die Linie und ist gleich noch entwickelt. Mein Trick ist geplatzt.',
        },
      ],
      abschluss:
        'Kommt die Dame früh raus, guck immer auf f7 (als Weiß auf f2). Deck das Feld oder versperr den Weg, und hol dabei deine Figuren raus.',
    },
    Eitel: {
      einleitung: 'Heute spielst du Schwarz, und ich gebe meine Paradenummer: das Schäfermatt. Durchschaust du den Trick?',
      schritte: [
        {
          hinweis: 'Zu bescheiden. Stell dich mir in der Mitte entgegen: Zieh den Bauern von e7 nach e5.',
          erklaerung: 'Gut. Jetzt zieht mein Läufer elegant nach c4 und zielt auf dein Feld f7.',
        },
        {
          hinweis: 'Bring zuerst einen Springer ins Spiel, nach c6. Schlicht, aber richtig.',
          erklaerung: 'Ordentlich. Und jetzt mein Star: Die Dame erscheint auf h5. Dame und Läufer greifen beide f7 an.',
        },
        {
          hinweis: 'Ich drohe Dame schlägt f7, matt, ein Glanzstück! Zwei gute Abwehren: Bauer g7 nach g6 greift meine Dame an, Dame d8 nach e7 deckt f7.',
          erklaerung: 'Abgewehrt? Unerhört! Das Matt auf f7 klappt so nicht. Dann eben eine Zugabe von f3 aus.',
        },
        {
          hinweis: 'Der Springer auf g8 kann sich als Schutzschild vor f7 stellen. Zieh ihn nach f6.',
          erklaerung: 'Sehr gut! Dein Springer sperrt die Linie und ist gleichzeitig entwickelt. Meine Nummer ist durchgefallen, aber mit Stil.',
        },
      ],
      abschluss:
        'Kommt die Dame früh, schau sofort auf f7 (als Weiß auf f2). Deck das Feld oder versperr den Weg, und entwickle dabei deine Figuren.',
    },
    Gemuetlich: {
      einleitung: 'Heute spielst du Schwarz. Ich probier einen frechen Trick, das Schäfermatt, aber nur zum Üben, gell. Schauen wir, ob du ihn durchschaust.',
      schritte: [
        {
          hinweis: 'Ach, stell dich mir lieber in der Mitte entgegen. Zieh den Bauern von e7 nach e5.',
          erklaerung: 'Gut so. Jetzt bringe ich meinen Läufer nach c4, der zielt auf dein Feld f7. A bisserl frech von mir.',
        },
        {
          hinweis: 'Bring zuerst einen Springer ins Spiel, nach c6, in aller Ruhe.',
          erklaerung: 'Schön. Jetzt kommt meine Dame nach h5, und Dame und Läufer greifen beide f7 an.',
        },
        {
          hinweis: 'Ich drohe Dame schlägt f7, matt! Zwei gute Abwehren hab ich für dich: Bauer g7 nach g6 greift meine Dame an, Dame d8 nach e7 deckt f7.',
          erklaerung: 'Abgewehrt, passt scho! Das Matt auf f7 klappt so nicht. Ich versuche es noch einmal von f3 aus.',
        },
        {
          hinweis: 'Der Springer auf g8 kann sich schön vor f7 stellen. Zieh ihn nach f6.',
          erklaerung: 'Wunderbar! Dein Springer sperrt die Linie und ist gleich gut entwickelt. Mein Trick hat nicht geklappt, na gut.',
        },
      ],
      abschluss:
        'Kommt die Dame früh heraus, schau immer auf f7 (als Weiß auf f2). Deck das Feld oder versperr den Weg, und hol dabei deine Figuren raus.',
    },
  },
  'schach-abwehren': {
    Aggressiv: {
      einleitung: 'Schach! Mein Turm greift deinen König direkt an. Jetzt musst du reagieren: König wegziehen, eine Figur dazwischenstellen oder den Angreifer schlagen.',
      schritte: [
        {
          hinweis: 'Dein König ist gerettet, aber das ist reine Verteidigung. Dein Springer auf d3 kann meinen Turm schlagen.',
          erklaerung: 'Autsch, mein Turm ist weg! Du wehrst das Schach ab und gewinnst noch Material. Schlagen ist oft die beste Abwehr.',
        },
      ],
      abschluss:
        'Im Schach sind nur Züge erlaubt, die das Schach beenden. Schau zuerst, ob du den Angreifer schlagen kannst, dann ist der Angriff vorbei.',
    },
    Vorsichtig: {
      einleitung: 'Schach. Mein Turm greift deinen König an. Im Schach hast du drei Möglichkeiten: König wegziehen, eine Figur dazwischenstellen oder den Angreifer schlagen.',
      schritte: [
        {
          hinweis: 'Das ist sicher, aber es gibt einen besseren Zug: Dein Springer auf d3 kann meinen Turm schlagen.',
          erklaerung: 'Richtig. Schach abgewehrt und meinen Turm gewonnen, fünf Punkte. Schlagen ist oft die beste Abwehr.',
        },
      ],
      abschluss:
        'Im Schach sind nur Züge erlaubt, die das Schach beenden. Prüf in Ruhe alle drei Möglichkeiten und fang mit dem Schlagen an.',
    },
    Lehrerhaft: {
      einleitung: 'Schach! Mein Turm greift deinen König an. Merk dir die drei Abwehren: König wegziehen, eine Figur dazwischenstellen oder den Angreifer schlagen.',
      schritte: [
        {
          hinweis: 'Das rettet deinen König. Es gibt aber eine bessere Abwehr: Dein Springer auf d3 kann meinen Turm schlagen.',
          erklaerung: 'Sehr gut! Du hast das Schach abgewehrt und dabei meinen Turm gewonnen. Die beste Abwehr ist oft das Schlagen.',
        },
      ],
      abschluss:
        'Im Schach sind nur Züge erlaubt, die das Schach beenden. Geh die drei Abwehren der Reihe nach durch und prüf zuerst das Schlagen.',
    },
    Verspielt: {
      einleitung: 'Schach! Mein Turm ist frech zu deinem König gesaust. Jetzt musst du was tun: König wegziehen, eine Figur dazwischenstellen oder den Angreifer schlagen.',
      schritte: [
        {
          hinweis: 'Klappt, aber da geht noch was Lustigeres: Dein Springer auf d3 kann meinen Turm schlagen.',
          erklaerung: 'Oh, mein Turm ist weg! Schach abgewehrt und noch was gewonnen. Schlagen ist oft die beste Abwehr.',
        },
      ],
      abschluss:
        'Im Schach sind nur Züge erlaubt, die das Schach beenden. Guck immer zuerst, ob du den Angreifer schnappen kannst.',
    },
    Eitel: {
      einleitung: 'Schach! Mein Turm greift deinen König an, ein eleganter Zug. Jetzt musst du handeln: König wegziehen, eine Figur dazwischenstellen oder den Angreifer schlagen.',
      schritte: [
        {
          hinweis: 'Gerettet, aber ohne Glanz. Dein Springer auf d3 kann meinen Turm schlagen.',
          erklaerung: 'Mein schöner Turm! Du hast das Schach abgewehrt und ihn dabei gewonnen. Schlagen ist oft die beste Abwehr.',
        },
      ],
      abschluss:
        'Im Schach sind nur Züge erlaubt, die das Schach beenden. Prüf immer zuerst, ob du den Angreifer schlagen kannst. Das ist die eleganteste Antwort.',
    },
    Gemuetlich: {
      einleitung: 'Schach! Mein Turm greift deinen König an. Jetzt musst du was tun: König wegziehen, eine Figur dazwischenstellen oder den Angreifer schlagen.',
      schritte: [
        {
          hinweis: 'Ach, das rettet deinen König schon. Aber schau, dein Springer auf d3 kann meinen Turm schlagen.',
          erklaerung: 'Na sowas, jetzt ist mein Turm weg. Du hast das Schach abgewehrt und ihn gleich mitgenommen. Schlagen ist oft die beste Abwehr.',
        },
      ],
      abschluss:
        'Steht dein König im Schach, darfst du nur Züge machen, die das Schach beenden. Schau immer zuerst, ob du den Angreifer schlagen kannst.',
    },
  },
  'patt-vermeiden': {
    Aggressiv: {
      einleitung: 'Ich habe nur noch meinen König, aber unterschätz ihn nicht. Kann ich nicht ziehen und stehe nicht im Schach, ist es Patt, und die Partie endet Remis.',
      schritte: [
        {
          hinweis: 'Nicht so schnell. Kann mein König nach deinem Zug noch ziehen? Ohne Schach und ohne freies Feld ist es Patt. Gib Schach auf der h-Linie.',
          erklaerung: 'Matt, sauber zu Ende gebracht! Mein König steht im Schach, und alle Felder um ihn decken deine Dame und dein König.',
        },
      ],
      abschluss:
        'Hätte deine Dame auf g6 gestanden, wäre es Patt gewesen: kein Schach, aber auch kein Zug für mich. Ein Sieg ist erst ein Sieg, wenn das Matt sitzt.',
    },
    Vorsichtig: {
      einleitung: 'Ich habe nur noch meinen König. Meine letzte Hoffnung ist ein Patt: Kann ich nicht mehr ziehen und stehe nicht im Schach, endet die Partie Remis.',
      schritte: [
        {
          hinweis: 'Vorsicht. Prüf nach deinem Zug, ob mein König noch ziehen kann. Ohne Schach und ohne freies Feld ist es Patt. Such ein Schach auf der h-Linie.',
          erklaerung: 'Matt. Mein König steht im Schach, und alle Felder um ihn decken deine Dame und dein König. Kein Patt, gut aufgepasst.',
        },
      ],
      abschluss:
        'Hätte deine Dame auf g6 gestanden, wäre es Patt gewesen: kein Schach, aber auch kein Zug für mich. Zähl vor jedem Zug kurz nach, wohin mein König noch kann.',
    },
    Lehrerhaft: {
      einleitung: 'Ich habe nur noch meinen König. Jetzt lernst du das Patt: Kann ich nicht mehr ziehen und stehe nicht im Schach, endet die Partie Remis.',
      schritte: [
        {
          hinweis: 'Stell dir nach jedem Zug zwei Fragen: Steht mein König im Schach? Hat er ein freies Feld? Zweimal nein heißt Patt. Gib Schach auf der h-Linie.',
          erklaerung: 'Matt! Mein König steht im Schach, und deine Dame und dein König decken alle Felder um ihn. So unterscheidest du Matt von Patt.',
        },
      ],
      abschluss:
        'Hätte deine Dame auf g6 gestanden, wäre es Patt gewesen: kein Schach, aber auch kein Zug für mich. Mit viel Material zählst du darum vor jedem Zug kurz nach.',
    },
    Verspielt: {
      einleitung: 'Ich habe nur noch meinen König. Mein letzter Trick heißt Patt: Kann ich nicht ziehen und stehe nicht im Schach, endet die Partie Remis.',
      schritte: [
        {
          hinweis: 'Achtung, Patt-Falle! Kann mein König nach deinem Zug noch irgendwohin? Ohne Schach und ohne Feld ist es Patt. Such ein Schach auf der h-Linie.',
          erklaerung: 'Matt, mein Trick ist geplatzt! Mein König steht im Schach, und alle Felder um ihn decken deine Dame und dein König.',
        },
      ],
      abschluss:
        'Hätte deine Dame auf g6 gestanden, wäre es Patt gewesen: kein Schach, aber auch kein Zug für mich. Mit viel Material lieber kurz nachzählen.',
    },
    Eitel: {
      einleitung: 'Ich habe nur noch meinen König, aber ein Patt wäre mein Rettungsanker: Kann ich nicht ziehen und stehe nicht im Schach, endet die Partie Remis.',
      schritte: [
        {
          hinweis: 'Riskant. Prüf nach deinem Zug: Kann mein König noch ziehen? Ohne Schach und ohne freies Feld ist es Patt. Ein Schach auf der h-Linie wäre stilvoller.',
          erklaerung: 'Matt, zugegeben, sauber gespielt. Mein König steht im Schach, und alle Felder um ihn decken deine Dame und dein König.',
        },
      ],
      abschluss:
        'Hätte deine Dame auf g6 gestanden, wäre es Patt gewesen: kein Schach, aber auch kein Zug für mich. Ein Remis aus Gewinnstellung ist der peinlichste Schluss.',
    },
    Gemuetlich: {
      einleitung: 'Ich habe nur noch meinen König. Aber schau: Kann ich nicht mehr ziehen und stehe nicht im Schach, ist es Patt. Dann endet die Partie Remis.',
      schritte: [
        {
          hinweis: 'Ach, schau nach deinem Zug: Kann mein König noch ziehen? Steht er nicht im Schach und hat kein freies Feld, ist es Patt. Such ein Schach auf der h-Linie.',
          erklaerung: 'Matt! Na gut. Mein König steht im Schach, und alle Felder um ihn decken deine Dame und dein König.',
        },
      ],
      abschluss:
        'Hätte deine Dame auf g6 gestanden, wäre es Patt gewesen: kein Schach, aber auch kein Zug für mich. Mit viel Material lohnt es sich, in Ruhe nachzuzählen.',
    },
  },
  'dame-und-koenig': {
    Aggressiv: {
      einleitung: 'Dame und König gegen meinen König allein. Die Dame jagt, aber für das Matt braucht sie deinen König als Helfer.',
      schritte: [
        {
          hinweis: 'Erst einsperren: Die Dame sperrt die siebte Reihe. Zieh sie nach a7.',
          erklaerung: 'Gut, mein König sitzt auf der letzten Reihe fest. Lass ihm aber ein Feld, sonst ist es Patt.',
        },
        {
          hinweis: 'Ran an den König! Deine Dame darf direkt neben ihn, wenn dein König sie deckt. Probier f7.',
          erklaerung: 'Matt! Ich kann deine Dame nicht schlagen, dein König deckt sie.',
        },
      ],
      abschluss:
        'Treib den König mit der Dame an den Rand, hol deinen König heran und schlag zu. Die Dame steht dabei oft direkt vor dem gegnerischen König, gedeckt vom eigenen.',
    },
    Vorsichtig: {
      einleitung: 'Mit Dame und König setzt du einen König allein matt. Die Dame schafft es nicht allein, dein König muss mithelfen.',
      schritte: [
        {
          hinweis: 'Erst die siebte Reihe sperren. Zieh die Dame nach a7.',
          erklaerung: 'Gut. Mein König kann nur noch auf der letzten Reihe hin und her. Achte darauf, dass er ein Feld behält, sonst ist es Patt.',
        },
        {
          hinweis: 'Deine Dame kann gefahrlos neben meinen König, weil dein König sie deckt. Probier f7.',
          erklaerung: 'Matt. Ich kann deine Dame nicht schlagen, weil dein König sie deckt. Gut abgesichert.',
        },
      ],
      abschluss:
        'Treib den König mit der Dame an den Rand, hol deinen König heran und setz erst dann matt. Die Dame steht oft direkt vor dem gegnerischen König, gedeckt vom eigenen.',
    },
    Lehrerhaft: {
      einleitung: 'Heute lernst du das Matt mit Dame und König. Die Dame kann das nicht allein, sie braucht die Hilfe deines Königs.',
      schritte: [
        {
          hinweis: 'Erstens sperrt die Dame die siebte Reihe. Zieh sie nach a7.',
          erklaerung: 'Richtig. Mein König kann nur noch auf der letzten Reihe hin und her. Wichtig: Er braucht noch ein Feld, sonst wäre es Patt.',
        },
        {
          hinweis: 'Zweitens: Deine Dame darf direkt neben meinen König, wenn dein König sie deckt. Probier f7.',
          erklaerung: 'Matt! Ich kann deine Dame nicht schlagen, weil dein König sie deckt. Genau so arbeiten die beiden zusammen.',
        },
      ],
      abschluss:
        'Das Rezept: König mit der Dame an den Rand treiben, den eigenen König heranholen, dann matt setzen. Die Dame steht oft direkt vor dem gegnerischen König, gedeckt vom eigenen.',
    },
    Verspielt: {
      einleitung: 'Dame und König gegen meinen König allein, das wird knapp für mich. Die Dame schafft es nicht allein, dein König muss mitspielen.',
      schritte: [
        {
          hinweis: 'Hm, erst mal die siebte Reihe sperren. Zieh die Dame nach a7.',
          erklaerung: 'Ups, jetzt kann mein König nur noch auf der letzten Reihe hin und her. Lass ihm noch ein Feld, sonst ist es Patt.',
        },
        {
          hinweis: 'Trau dich: Deine Dame darf direkt neben meinen König, wenn dein König sie deckt. Probier f7.',
          erklaerung: 'Matt! Ich würde deine Dame ja schlagen, aber dein König deckt sie.',
        },
      ],
      abschluss:
        'Treib den König mit der Dame an den Rand, hol deinen König dazu und setz dann matt. Die Dame steht dabei oft direkt vor dem gegnerischen König, gedeckt vom eigenen.',
    },
    Eitel: {
      einleitung: 'Mit Dame und König setzt du einen König allein matt. Selbst die Dame, die stärkste Figur, braucht dafür die Hilfe deines Königs.',
      schritte: [
        {
          hinweis: 'Nicht ganz elegant. Die Dame sperrt zuerst die siebte Reihe: Zieh sie nach a7.',
          erklaerung: 'Gut gesetzt. Mein König kann nur noch auf der letzten Reihe hin und her. Lass ihm ein Feld, sonst wäre es ein peinliches Patt.',
        },
        {
          hinweis: 'Deine Dame darf direkt neben meinen König, wenn dein König sie deckt. Ein mutiger Auftritt auf f7.',
          erklaerung: 'Matt! Ich kann deine Dame nicht schlagen, weil dein König sie deckt. Ein schönes Duett.',
        },
      ],
      abschluss:
        'Treib den König mit der Dame an den Rand und hol deinen König heran, dann folgt das Matt. Die Dame steht dabei oft direkt vor dem gegnerischen König, gedeckt vom eigenen.',
    },
    Gemuetlich: {
      einleitung: 'Mit Dame und König setzt du einen König allein matt. Die Dame schafft das nicht allein, dein König muss a bisserl mithelfen.',
      schritte: [
        {
          hinweis: 'Ach, erst sperrt die Dame die siebte Reihe. Zieh sie nach a7.',
          erklaerung: 'Gut. Mein König kann nur noch auf der letzten Reihe hin und her. Lass ihm aber ein Feld, sonst ist es Patt.',
        },
        {
          hinweis: 'Deine Dame darf ruhig direkt neben meinen König, wenn dein König sie deckt. Probier f7.',
          erklaerung: 'Matt! Na gut, ich kann deine Dame nicht schlagen, weil dein König sie deckt.',
        },
      ],
      abschluss:
        'Erst treibt die Dame den König an den Rand, dann kommt dein König dazu, und dann ist es matt. Die Dame steht dabei oft direkt vor dem gegnerischen König, gedeckt vom eigenen.',
    },
  },
  'ersticktes-matt': {
    Aggressiv: {
      einleitung: 'Mein König hockt in der Ecke, eingekeilt von Turm und Bauern. Eine Einladung für einen Springerangriff.',
      schritte: [
        {
          hinweis: 'Gezielter! Von welchem Feld trifft dein Springer h8? Schau auf f7.',
          erklaerung: 'Matt, mitten rein! Mein König kann nicht weg, weil seine eigenen Figuren alle Felder besetzen. Das ist ersticktes Matt.',
        },
      ],
      abschluss:
        'Springer springen über Figuren. Ein König, der von eigenen Figuren eingeklemmt ist, ist darum gegen einen Springer ein leichtes Ziel.',
    },
    Vorsichtig: {
      einleitung: 'Mein König steht in der Ecke, umringt von seinem Turm und seinen Bauern. Das wirkt sicher, ist es aber nicht: Ein Springer kann das ausnutzen.',
      schritte: [
        {
          hinweis: 'Rechne kurz nach: Von welchem Feld greift dein Springer h8 an? Probier f7.',
          erklaerung: 'Matt. Mein König kann nicht weg, weil seine eigenen Figuren alle Felder besetzen. Ersticktes Matt, zu viel Deckung kann schaden.',
        },
      ],
      abschluss:
        'Springer springen über Figuren. Ein König, der von eigenen Figuren eingeklemmt ist, ist gegen einen Springer darum nicht sicher.',
    },
    Lehrerhaft: {
      einleitung: 'Mein König steht in der Ecke, umringt von seinem Turm und seinen Bauern. Er hat sich selbst eingemauert. Genau das nutzt ein Springer aus.',
      schritte: [
        {
          hinweis: 'Denk an die Gangart des Springers: Von welchem Feld greift er h8 an? Schau auf f7.',
          erklaerung: 'Matt! Mein König kann nicht weg, weil seine eigenen Figuren alle Felder besetzen. Merk dir den Namen: ersticktes Matt.',
        },
      ],
      abschluss:
        'Der Grund: Springer springen über Figuren. Ein König, der von eigenen Figuren eingeklemmt ist, ist gegen einen Springer besonders gefährdet.',
    },
    Verspielt: {
      einleitung: 'Ups, mein König hat sich in der Ecke mit Turm und Bauern zugebaut. Ganz schön eng da. Ein Springer könnte das ausnutzen.',
      schritte: [
        {
          hinweis: 'Fast! Von welchem Feld erwischt dein Springer h8? Guck mal auf f7.',
          erklaerung: 'Matt! Mein König ist in seiner eigenen Burg erstickt, weil seine Figuren alle Felder besetzen. Das heißt ersticktes Matt.',
        },
      ],
      abschluss:
        'Springer springen einfach über alles drüber. Ein König, der von eigenen Figuren eingeklemmt ist, hat gegen einen Springer darum schlechte Karten.',
    },
    Eitel: {
      einleitung: 'Mein König residiert in der Ecke, umgeben von Turm und Bauern. Leider hat er sich dabei selbst eingemauert. Ein Springer kann das ausnutzen.',
      schritte: [
        {
          hinweis: 'Das geht schöner. Von welchem Feld greift dein Springer h8 an? Schau auf f7.',
          erklaerung: 'Matt! Das berühmteste Springermatt überhaupt. Mein König kann nicht weg, weil seine eigenen Figuren alle Felder besetzen. Ersticktes Matt.',
        },
      ],
      abschluss:
        'Springer springen über Figuren. Ein König, der von eigenen Figuren eingeklemmt ist, ist gegen einen Springer besonders gefährdet, so prächtig er auch wohnt.',
    },
    Gemuetlich: {
      einleitung: 'Mein König steht in der Ecke, umringt von seinem Turm und seinen Bauern. Mei, so eingemauert. Das kann ein Springer ausnutzen.',
      schritte: [
        {
          hinweis: 'Ach, von welchem Feld greift dein Springer h8 an? Schau mal auf f7.',
          erklaerung: 'Matt! Mein König kann nicht weg, weil seine eigenen Figuren alle Felder zustellen. Das nennt man ersticktes Matt, und es passiert mir nicht zum ersten Mal.',
        },
      ],
      abschluss:
        'Springer springen über Figuren. Darum ist ein König, der von eigenen Figuren eingeklemmt ist, gegen einen Springer ganz schön gefährdet.',
    },
  },
  fesselung: {
    Aggressiv: {
      einleitung: 'Meine Dame und mein König stehen auf derselben Linie. Greifst du die Dame dort an, darf sie nicht weg, sonst stünde mein König im Schach. Das ist eine Fesselung.',
      schritte: [
        {
          hinweis: 'Greif an! Stell deinen Turm auf die e-Linie, auf der meine Dame und mein König stehen.',
          erklaerung: 'Gefesselt! Meine Dame kann nicht zur Seite, sonst stünde mein König im Schach. Dann nehme ich wenigstens deinen Turm mit.',
        },
        {
          hinweis: 'Meine Dame steht direkt neben deinem König. Schlag zu.',
          erklaerung: 'Ein Turm für eine Dame, so gewinnt man Material.',
        },
      ],
      abschluss:
        'Achte auf Figuren, die mit dem König auf einer Linie stehen. Läufer, Turm oder Dame können sie fesseln, und gefesselte Figuren sind leichte Beute.',
    },
    Vorsichtig: {
      einleitung: 'Meine Dame steht mit meinem König auf einer Linie. Greifst du sie dort an, darf sie nicht weg, sonst stünde mein König im Schach. Das heißt Fesselung.',
      schritte: [
        {
          hinweis: 'Der Turm gehört auf die e-Linie, auf der meine Dame und mein König stehen.',
          erklaerung: 'Gefesselt. Meine Dame kann nicht zur Seite, sonst stünde mein König im Schach. Ich schlage deinen Turm, damit ich wenigstens etwas rette.',
        },
        {
          hinweis: 'Meine Dame steht direkt neben deinem König, und sie ist nicht gedeckt. Schlag sie.',
          erklaerung: 'Ein Turm gegen eine Dame, das sind vier Punkte Gewinn für dich.',
        },
      ],
      abschluss:
        'Prüf immer, welche Figuren mit dem König auf einer Linie stehen. Läufer, Turm oder Dame können sie fesseln. Gefesselte Figuren sind leichte Beute.',
    },
    Lehrerhaft: {
      einleitung: 'Heute lernst du die Fesselung. Meine Dame und mein König stehen auf derselben Linie. Greifst du die Dame dort an, darf sie nicht weg, sonst stünde mein König im Schach.',
      schritte: [
        {
          hinweis: 'Fast. Die Fesselung braucht die e-Linie, auf der meine Dame und mein König stehen. Stell deinen Turm dorthin.',
          erklaerung: 'Gefesselt! Meine Dame kann nicht zur Seite, sonst stünde mein König im Schach. Darum schlage ich deinen Turm, bevor ich sie ganz verliere.',
        },
        {
          hinweis: 'Jetzt steht meine Dame direkt neben deinem König. Schlag sie.',
          erklaerung: 'Richtig. Ein Turm gegen eine Dame, ein sehr guter Tausch für dich.',
        },
      ],
      abschluss:
        'Merk dir: Figuren auf einer Linie mit dem König sind gefährdet. Läufer, Turm oder Dame können sie fesseln, und gefesselte Figuren sind leichte Beute.',
    },
    Verspielt: {
      einleitung: 'Ups, meine Dame und mein König stehen auf derselben Linie. Greifst du die Dame dort an, darf sie nicht weg, sonst stünde mein König im Schach. Das heißt Fesselung.',
      schritte: [
        {
          hinweis: 'Hm, probier lieber die e-Linie, auf der meine Dame und mein König stehen. Stell deinen Turm dorthin.',
          erklaerung: 'Gefesselt, oje! Meine Dame kann nicht zur Seite, sonst stünde mein König im Schach. Dann schnappe ich mir wenigstens deinen Turm.',
        },
        {
          hinweis: 'Meine Dame steht jetzt direkt neben deinem König. Na los, schlag sie.',
          erklaerung: 'Ein Turm gegen eine Dame, guter Tausch für dich. Das war wohl nichts von mir.',
        },
      ],
      abschluss:
        'Guck immer, welche Figuren mit dem König auf einer Linie stehen. Läufer, Turm oder Dame können sie fesseln. Gefesselte Figuren sind leichte Beute.',
    },
    Eitel: {
      einleitung: 'Meine Dame und mein König stehen auf einer Linie, ein seltener Fehler. Greifst du die Dame dort an, darf sie nicht weg, sonst stünde mein König im Schach: Fesselung.',
      schritte: [
        {
          hinweis: 'Das geht eleganter. Stell deinen Turm auf die e-Linie, auf der meine Dame und mein König stehen.',
          erklaerung: 'Gefesselt! Meine Dame kann nicht zur Seite, sonst stünde mein König im Schach. Dann opfere ich sie wenigstens mit Würde für deinen Turm.',
        },
        {
          hinweis: 'Meine Dame steht direkt neben deinem König. Schlag sie, ich schaue nicht hin.',
          erklaerung: 'Ein Turm gegen eine Dame. Für dich ein glänzendes Geschäft, für mich ein schwarzer Tag.',
        },
      ],
      abschluss:
        'Achte auf Figuren, die mit dem König auf einer Linie stehen. Ein Läufer, Turm oder eine Dame kann sie fesseln. Gefesselte Figuren sind leichte Beute, sogar meine.',
    },
    Gemuetlich: {
      einleitung: 'Schau, meine Dame und mein König stehen auf derselben Linie. Greifst du die Dame dort an, darf sie nicht weg, sonst stünde mein König im Schach. Das heißt Fesselung.',
      schritte: [
        {
          hinweis: 'Ach, stell deinen Turm lieber auf die e-Linie, auf der meine Dame und mein König stehen.',
          erklaerung: 'Gefesselt! Meine Dame kann nicht zur Seite, sonst stünde mein König im Schach. Na, dann nehm ich halt deinen Turm, bevor ich sie ganz verliere.',
        },
        {
          hinweis: 'Jetzt steht meine Dame direkt neben deinem König. Nimm sie dir.',
          erklaerung: 'Ein Turm gegen eine Dame, da hast du ein gutes Geschäft gemacht.',
        },
      ],
      abschluss:
        'Schau auf Figuren, die mit dem König auf einer Linie stehen. Ein Läufer, Turm oder eine Dame kann sie fesseln, und dann sind sie leichte Beute.',
    },
  },
  spiess: {
    Aggressiv: {
      einleitung: 'Beim Spieß greifst du eine wertvolle Figur an, hinter der eine andere steht. Weicht die vordere aus, holst du dir die hintere. Mein König steht vor meiner Dame.',
      schritte: [
        {
          hinweis: 'Direkt drauf! Mein König und meine Dame stehen beide auf der e-Linie. Gib dort Schach.',
          erklaerung: 'Schach! Mein König muss weg von der Linie, und dann liegt meine Dame frei vor deinem Turm.',
        },
        {
          hinweis: 'Jetzt zuschlagen: Dein Turm sieht meine Dame auf e8.',
          erklaerung: 'Die Dame gehört dir. Ein Spieß, direkt durch.',
        },
      ],
      abschluss:
        'Der Spieß ist die umgekehrte Fesselung: Vorne steht die wertvollere Figur. Mit Schach klappt er fast immer, weil der König ausweichen muss.',
    },
    Vorsichtig: {
      einleitung: 'Beim Spieß greifst du eine wertvolle Figur an, hinter der eine andere steht. Weicht die vordere aus, fällt die hintere. Mein König steht leider vor meiner Dame.',
      schritte: [
        {
          hinweis: 'Mein König und meine Dame stehen beide auf der e-Linie. Ein Schach dort wäre der sichere Gewinn.',
          erklaerung: 'Schach. Mein König muss die Linie verlassen, und dann ist meine Dame frei für deinen Turm. Das hätte ich vermeiden sollen.',
        },
        {
          hinweis: 'Dein Turm sieht jetzt meine Dame auf e8, und sie ist ungeschützt. Schlag sie.',
          erklaerung: 'Die Dame gehört dir. Das war ein Spieß, neun Punkte für einen Turmzug.',
        },
      ],
      abschluss:
        'Der Spieß ist die umgekehrte Fesselung: Vorne steht die wertvollere Figur. Mit einem Schach ist er fast sicher, weil der König ausweichen muss.',
    },
    Lehrerhaft: {
      einleitung: 'Heute lernst du den Spieß: Du greifst eine wertvolle Figur an, hinter der eine andere steht. Weicht sie aus, fällt die hintere. Mein König steht vor meiner Dame.',
      schritte: [
        {
          hinweis: 'Schau auf die e-Linie: Dort stehen mein König und meine Dame. Gib dort Schach.',
          erklaerung: 'Schach! Mein König muss die Linie verlassen. Danach ist meine Dame frei für deinen Turm, das ist der Kern des Spießes.',
        },
        {
          hinweis: 'Jetzt der zweite Teil: Dein Turm sieht meine Dame auf e8. Schlag sie.',
          erklaerung: 'Sehr gut, die Dame gehört dir. Das war ein Spieß.',
        },
      ],
      abschluss:
        'Der Spieß ist die umgekehrte Fesselung: Vorne steht die wertvollere Figur. Er klappt fast immer mit einem Schach, weil der König ausweichen muss.',
    },
    Verspielt: {
      einleitung: 'Beim Spieß greifst du eine wertvolle Figur an, hinter der eine andere steht. Hüpft die vordere weg, ist die hintere dran. Mein König steht ausgerechnet vor meiner Dame.',
      schritte: [
        {
          hinweis: 'Hm, nicht ganz. Mein König und meine Dame stehen beide auf der e-Linie. Gib dort Schach.',
          erklaerung: 'Schach! Mein König muss von der Linie runter, und dann steht meine Dame ganz allein vor deinem Turm. Ups.',
        },
        {
          hinweis: 'Dein Turm guckt jetzt genau auf meine Dame auf e8. Schnapp sie dir.',
          erklaerung: 'Die Dame gehört dir. Das war ein Spieß, und ich bin voll reingelaufen.',
        },
      ],
      abschluss:
        'Der Spieß ist eine Fesselung andersherum: Vorne steht die wertvollere Figur. Mit Schach klappt er fast immer, weil der König weg muss.',
    },
    Eitel: {
      einleitung: 'Der Spieß ist elegant: Du greifst eine wertvolle Figur an, hinter der eine andere steht. Weicht die vordere aus, fällt die hintere. Mein König steht vor meiner Dame.',
      schritte: [
        {
          hinweis: 'Zu umständlich. Mein König und meine Dame stehen beide auf der e-Linie. Gib dort Schach.',
          erklaerung: 'Schach! Mein König muss die Linie verlassen, und meine Dame steht ohne Schutz vor deinem Turm. Wie unschön.',
        },
        {
          hinweis: 'Dein Turm sieht jetzt meine Dame auf e8. Nimm sie, wenn du dich traust.',
          erklaerung: 'Die Dame gehört dir. Ein Spieß, das muss ich neidlos anerkennen.',
        },
      ],
      abschluss:
        'Der Spieß ist die umgekehrte Fesselung: Vorne steht die wertvollere Figur. Mit einem Schach klappt er fast immer, weil auch ein König wie meiner ausweichen muss.',
    },
    Gemuetlich: {
      einleitung: 'Beim Spieß greifst du eine wertvolle Figur an, hinter der eine andere steht. Weicht die vordere aus, schlägst du die hintere. Und schau, mein König steht vor meiner Dame.',
      schritte: [
        {
          hinweis: 'Ach, mein König und meine Dame stehen beide auf der e-Linie. Gib dort Schach.',
          erklaerung: 'Schach! Mein König muss die Linie verlassen, und dann ist meine Dame frei für deinen Turm. Na gut.',
        },
        {
          hinweis: 'Dein Turm sieht jetzt meine Dame auf e8. Nimm sie.',
          erklaerung: 'Die Dame gehört dir. Das war ein Spieß, schön gemacht.',
        },
      ],
      abschluss:
        'Der Spieß ist die umgekehrte Fesselung: Vorne steht die wertvollere Figur. Mit einem Schach klappt er fast immer, weil der König halt ausweichen muss.',
    },
  },
  damengambit: {
    Aggressiv: {
      einleitung: 'Das Damengambit beginnt mit dem Damenbauern. Gambit heißt: Du gibst einen Bauern her und bekommst dafür die Mitte. Ein Angebot ganz nach meinem Geschmack.',
      schritte: [
        {
          hinweis: 'Heute zuerst der Damenbauer. Zieh ihn von d2 nach d4.',
          erklaerung: 'Gut, so packst du die Mitte auch. Ich halte genauso dagegen.',
        },
        {
          hinweis: 'Jetzt das Gambit! Zieh den Bauern von c2 nach c4, er greift meinen Bauern auf d5 an.',
          erklaerung: 'Damengambit. Ich nehme den Bauern nicht, ich stütze meinen Bauern auf d5 mit e6 und halte die Stellung.',
        },
        {
          hinweis: 'Springer raus, und zwar schnell. Zieh einen nach c3 oder f3.',
          erklaerung: 'Stark, dein Springer erhöht den Druck auf d5. Ich bringe sofort meinen Springer.',
        },
        {
          hinweis: 'Der Läufer auf c1 will mitkämpfen. Zieh ihn nach g5.',
          erklaerung: 'Druck ohne Ende! Dein Läufer greift meinen Springer an, der d5 schützt. Ich stelle meinen Läufer dazwischen.',
        },
      ],
      abschluss:
        'Im Damengambit kämpfst du mit c4 um die Mitte. Nimmt Schwarz den Bauern, holst du ihn meistens später zurück. Dann Figuren raus, rochieren und angreifen.',
    },
    Vorsichtig: {
      einleitung: 'Das Damengambit beginnt mit dem Damenbauern. Ein Gambit heißt: Du bietest einen Bauern an und bekommst dafür die Mitte. Ein überlegtes Risiko.',
      schritte: [
        {
          hinweis: 'Diesmal beginnst du mit dem Bauern vor der Dame. Zieh ihn von d2 nach d4.',
          erklaerung: 'Gut, auch so kontrollierst du die Mitte. Ich antworte mit demselben Zug, das ist solide.',
        },
        {
          hinweis: 'Jetzt kommt das Gambit. Zieh den Bauern von c2 nach c4, er greift meinen Bauern auf d5 an. Keine Sorge, du bekommst ihn meist zurück.',
          erklaerung: 'Das ist das Damengambit. Ich nehme den Bauern nicht, das ist mir zu unsicher. Ich stütze meinen Bauern auf d5 mit e6.',
        },
        {
          hinweis: 'Springer kommen früh ins Spiel, das hat sich bewährt. Zieh einen nach c3 oder f3.',
          erklaerung: 'Richtig, dein Springer erhöht den Druck auf d5. Ich bringe auch meinen Springer, zur Verstärkung.',
        },
        {
          hinweis: 'Der Läufer auf c1 ist noch nicht entwickelt. Zieh ihn nach g5.',
          erklaerung: 'Gut. Dein Läufer greift meinen Springer an, der d5 schützt. Ich stelle meinen Läufer dazwischen, sicher ist sicher.',
        },
      ],
      abschluss:
        'Im Damengambit kämpfst du mit c4 um die Mitte. Nimmt Schwarz den Bauern, holst du ihn meistens später zurück. Danach in Ruhe Figuren raus und rochieren.',
    },
    Lehrerhaft: {
      einleitung: 'Heute lernst du das Damengambit. Es beginnt mit dem Damenbauern. Gambit heißt: Du bietest einen Bauern an, damit du dafür die Mitte bekommst.',
      schritte: [
        {
          hinweis: 'Diesmal beginnt alles mit dem Bauern vor der Dame. Zieh ihn von d2 nach d4.',
          erklaerung: 'Richtig, auch so kontrollierst du die Mitte. Ich antworte mit demselben Zug.',
        },
        {
          hinweis: 'Jetzt folgt das eigentliche Gambit. Zieh den Bauern von c2 nach c4, er greift meinen Bauern auf d5 an.',
          erklaerung: 'Das ist das Damengambit. Ich nehme den Bauern nicht an, sondern stütze meinen Bauern auf d5 mit e6. So spielt man das abgelehnte Damengambit.',
        },
        {
          hinweis: 'Erinnerst du dich? Springer kommen früh ins Spiel. Zieh einen nach c3 oder f3.',
          erklaerung: 'Sehr gut, dein Springer erhöht den Druck auf d5. Ich bringe auch meinen Springer.',
        },
        {
          hinweis: 'Jetzt der Läufer auf c1. Zieh ihn nach g5.',
          erklaerung: 'Ausgezeichnet! Dein Läufer greift meinen Springer an, der d5 schützt. Ich stelle meinen Läufer dazwischen.',
        },
      ],
      abschluss:
        'Im Damengambit kämpfst du mit c4 um die Mitte. Nimmt Schwarz den Bauern, holst du ihn meistens später zurück. Danach gilt wie immer: Figuren raus und rochieren.',
    },
    Verspielt: {
      einleitung: 'Das Damengambit, eins meiner liebsten! Es beginnt mit dem Damenbauern. Gambit heißt: Du bietest einen Bauern an und bekommst dafür die Mitte.',
      schritte: [
        {
          hinweis: 'Hm, heute mal mit dem Bauern vor der Dame. Zieh ihn von d2 nach d4.',
          erklaerung: 'Gut, so geht die Mitte auch. Ich mach einfach dasselbe.',
        },
        {
          hinweis: 'Jetzt kommt der Trick, das Gambit. Zieh den Bauern von c2 nach c4, er greift meinen Bauern auf d5 an.',
          erklaerung: 'Das ist das Damengambit. Ich bin heute brav und nehme den Bauern nicht. Ich stütze meinen Bauern auf d5 mit e6.',
        },
        {
          hinweis: 'Springer zuerst raus. Zieh einen nach c3 oder f3.',
          erklaerung: 'Oha, dein Springer erhöht den Druck auf d5. Ich hole schnell meinen Springer.',
        },
        {
          hinweis: 'Der Läufer auf c1 zappelt schon. Zieh ihn nach g5.',
          erklaerung: 'Huch, dein Läufer greift meinen Springer an, der d5 schützt. Ich schiebe schnell meinen Läufer dazwischen.',
        },
      ],
      abschluss:
        'Im Damengambit kämpfst du mit c4 um die Mitte. Nimmt Schwarz den Bauern, holst du ihn meistens später zurück. Danach Figuren raus und ab in die Rochade.',
    },
    Eitel: {
      einleitung: 'Das Damengambit ist die vornehmste aller Eröffnungen. Es beginnt mit dem Damenbauern. Gambit heißt: Du bietest einen Bauern an und bekommst dafür die Mitte.',
      schritte: [
        {
          hinweis: 'Heute hat der Damenbauer den ersten Zug. Zieh ihn von d2 nach d4.',
          erklaerung: 'Gut, auch so kontrollierst du die Mitte. Ich antworte genauso, nur mit mehr Stil.',
        },
        {
          hinweis: 'Jetzt das Gambit, der feine Teil. Zieh den Bauern von c2 nach c4, er greift meinen Bauern auf d5 an.',
          erklaerung: 'Das ist das Damengambit. Ich nehme den Bauern nicht, das wäre zu gewöhnlich. Ich stütze meinen Bauern auf d5 mit e6.',
        },
        {
          hinweis: 'Springer kommen früh ins Spiel, das gehört sich so. Zieh einen nach c3 oder f3.',
          erklaerung: 'Hübsch, dein Springer erhöht den Druck auf d5. Ich bringe auch meinen Springer, natürlich eleganter.',
        },
        {
          hinweis: 'Der Läufer auf c1 wartet noch auf seinen Auftritt. Zieh ihn nach g5.',
          erklaerung: 'Sehr gut! Dein Läufer greift meinen Springer an, der d5 schützt. Ich stelle meinen Läufer dazwischen, souverän wie immer.',
        },
      ],
      abschluss:
        'Im Damengambit kämpfst du mit c4 um die Mitte. Nimmt Schwarz den Bauern, holst du ihn meistens später zurück. Danach Figuren raus und mit Stil rochieren.',
    },
    Gemuetlich: {
      einleitung: 'Das Damengambit beginnt mit dem Damenbauern. Ein Gambit heißt: Du bietest einen Bauern an, und dafür kriegst du die Mitte.',
      schritte: [
        {
          hinweis: 'Ach, diesmal fängst du mit dem Bauern vor der Dame an. Zieh ihn von d2 nach d4.',
          erklaerung: 'Gut, so kontrollierst du die Mitte auch. Ich mach es genauso.',
        },
        {
          hinweis: 'Jetzt kommt das Gambit. Zieh den Bauern von c2 nach c4, der greift meinen Bauern auf d5 an.',
          erklaerung: 'Das ist das Damengambit. Ich nehm den Bauern nicht, sondern stütze meinen Bauern auf d5 mit e6, ganz gemütlich.',
        },
        {
          hinweis: 'Springer kommen früh ins Spiel, gell. Zieh einen nach c3 oder f3.',
          erklaerung: 'Schön, dein Springer erhöht den Druck auf d5. Ich bring auch meinen Springer.',
        },
        {
          hinweis: 'Der Läufer auf c1 möcht auch raus. Zieh ihn nach g5.',
          erklaerung: 'Fein! Dein Läufer greift meinen Springer an, der d5 schützt. Ich stell meinen Läufer dazwischen.',
        },
      ],
      abschluss:
        'Im Damengambit kämpfst du mit c4 um die Mitte. Nimmt Schwarz den Bauern, holst du ihn meistens später zurück. Danach Figuren raus und rochieren, in aller Ruhe.',
    },
  },
  sizilianisch: {
    Aggressiv: {
      einleitung: 'Du spielst Schwarz, ich eröffne mit e4. Die Sizilianische Verteidigung ist die kämpferischste Antwort darauf: Du greifst die Mitte von der Seite an.',
      schritte: [
        {
          hinweis: 'Nicht e5, heute wird es schärfer. Zieh den Bauern von c7 nach c5, er kontrolliert d4.',
          erklaerung: 'Sizilianisch, jetzt wird gekämpft. Dein Bauer auf c5 hat das Feld d4 im Visier. Ich bringe meinen Springer.',
        },
        {
          hinweis: 'Ein kleiner Bauernzug mit Wirkung: d7 nach d6. Er öffnet den Weg für deinen Läufer auf c8.',
          erklaerung: 'Gut. Jetzt reiße ich die Mitte auf, mit d4.',
        },
        {
          hinweis: 'Mein Bauer auf d4 steht in Reichweite deines c-Bauern. Schlag zu.',
          erklaerung: 'Genau. Dein Randbauer gegen meinen Mittelbauern, guter Tausch. Ich schlage mit dem Springer zurück.',
        },
        {
          hinweis: 'Dein Springer auf g8 wartet auf seinen Einsatz. Zieh ihn nach f6.',
          erklaerung: 'Stark! Dein Springer greift e4 an, und ich muss ihn decken. Deine Stellung ist gesund und bissig.',
        },
      ],
      abschluss:
        'Bei Sizilianisch hast du am Ende einen Mittelbauern mehr als Weiß. Dafür ist Weiß schneller entwickelt. Hol deine Figuren zügig raus und rochiere.',
    },
    Vorsichtig: {
      einleitung: 'Du spielst Schwarz. Ich beginne mit e4. Die Sizilianische Verteidigung ist eine bewährte Antwort darauf: Du kämpfst von der Seite um die Mitte.',
      schritte: [
        {
          hinweis: 'Heute nicht e5. Zieh den Bauern von c7 nach c5, er kontrolliert d4.',
          erklaerung: 'Das ist Sizilianisch. Dein Bauer auf c5 überwacht das Feld d4. Ich bringe vorsichtig meinen Springer.',
        },
        {
          hinweis: 'Ein kleiner, solider Bauernzug: d7 nach d6. Er öffnet den Weg für deinen Läufer auf c8.',
          erklaerung: 'Gut. Jetzt öffne ich die Mitte mit d4, nach reiflicher Überlegung.',
        },
        {
          hinweis: 'Mein Bauer auf d4 steht in Reichweite deines c-Bauern. Schlag ihn, das ist sicher.',
          erklaerung: 'Richtig. Du tauschst deinen Randbauern gegen meinen Mittelbauern. Ich schlage mit dem Springer zurück.',
        },
        {
          hinweis: 'Dein Springer auf g8 ist noch nicht im Spiel. Zieh ihn nach f6.',
          erklaerung: 'Gut gemacht. Dein Springer greift e4 an, und ich muss ihn decken. Deine Stellung ist gesund und sicher.',
        },
      ],
      abschluss:
        'Bei Sizilianisch hast du am Ende einen Mittelbauern mehr als Weiß. Dafür ist Weiß schneller entwickelt. Bring deine Figuren zügig heraus und rochiere früh.',
    },
    Lehrerhaft: {
      einleitung: 'Du spielst Schwarz, ich beginne mit e4. Heute lernst du die Sizilianische Verteidigung: Du kämpfst von der Seite um die Mitte.',
      schritte: [
        {
          hinweis: 'Diesmal nicht e5. Der Bauer geht von c7 nach c5 und kontrolliert d4.',
          erklaerung: 'Richtig, das ist Sizilianisch. Dein Bauer auf c5 hält das Feld d4 im Blick. Ich bringe meinen Springer.',
        },
        {
          hinweis: 'Jetzt ein kleiner Bauernzug: d7 nach d6. Er öffnet den Weg für deinen Läufer auf c8.',
          erklaerung: 'Gut. Jetzt öffne ich die Mitte mit d4. Achte darauf, was dein c-Bauer jetzt kann.',
        },
        {
          hinweis: 'Dein c-Bauer kann meinen Bauern auf d4 schlagen. Schlag ihn.',
          erklaerung: 'Genau. Merk dir den Tausch: dein Randbauer gegen meinen Mittelbauern. Ich schlage mit dem Springer zurück.',
        },
        {
          hinweis: 'Jetzt entwickelst du den Springer auf g8. Zieh ihn nach f6.',
          erklaerung: 'Sehr gut! Dein Springer greift e4 an, und ich muss ihn decken. Deine Stellung ist gesund.',
        },
      ],
      abschluss:
        'Der Gedanke hinter Sizilianisch: Am Ende hast du einen Mittelbauern mehr als Weiß, dafür ist Weiß schneller entwickelt. Bring deine Figuren zügig heraus und rochiere.',
    },
    Verspielt: {
      einleitung: 'Du spielst Schwarz, ich fange mit e4 an. Die Sizilianische Verteidigung ist eine sehr beliebte Antwort darauf: Du kämpfst von der Seite um die Mitte. Wild!',
      schritte: [
        {
          hinweis: 'Hm, heute nicht e5. Zieh den Bauern von c7 nach c5, er kontrolliert d4.',
          erklaerung: 'Sizilianisch, juhu, jetzt wird es wild! Dein Bauer auf c5 schielt auf das Feld d4. Ich hole meinen Springer.',
        },
        {
          hinweis: 'Ein winziger Bauernzug: d7 nach d6. Er macht den Weg frei für deinen Läufer auf c8.',
          erklaerung: 'Gut. Jetzt mach ich die Mitte auf, mit d4.',
        },
        {
          hinweis: 'Mein Bauer auf d4 ist in Reichweite deines c-Bauern. Schnapp ihn dir.',
          erklaerung: 'Genau. Dein Randbauer gegen meinen Mittelbauern, cleverer Tausch. Ich schlage mit dem Springer zurück.',
        },
        {
          hinweis: 'Dein Springer auf g8 will endlich mitspielen. Zieh ihn nach f6.',
          erklaerung: 'Oha, dein Springer greift e4 an, und ich muss ihn decken. Deine Stellung ist gesund und macht Spaß.',
        },
      ],
      abschluss:
        'Bei Sizilianisch hast du am Ende einen Mittelbauern mehr als Weiß. Dafür ist Weiß schneller entwickelt. Also Figuren flott raus und rochieren.',
    },
    Eitel: {
      einleitung: 'Du spielst Schwarz, ich eröffne natürlich mit e4. Die Sizilianische Verteidigung ist eine sehr beliebte Antwort darauf: Du kämpfst von der Seite um die Mitte.',
      schritte: [
        {
          hinweis: 'Nicht e5, das sieht man zu oft. Zieh den Bauern von c7 nach c5, er kontrolliert d4.',
          erklaerung: 'Sizilianisch, eine Eröffnung mit Charakter. Dein Bauer auf c5 hält das Feld d4 im Blick. Ich bringe meinen Springer.',
        },
        {
          hinweis: 'Ein kleiner, feiner Bauernzug: d7 nach d6. Er öffnet den Weg für deinen Läufer auf c8.',
          erklaerung: 'Gut. Jetzt öffne ich die Mitte mit d4, mit großer Geste.',
        },
        {
          hinweis: 'Mein Bauer auf d4 steht in Reichweite deines c-Bauern. Nimm ihn, ich gönne ihn dir.',
          erklaerung: 'Genau. Du tauschst deinen Randbauern gegen meinen Mittelbauern. Ich schlage mit dem Springer zurück, formvollendet.',
        },
        {
          hinweis: 'Dein Springer auf g8 will auch glänzen. Zieh ihn nach f6.',
          erklaerung: 'Sehr gut! Dein Springer greift e4 an, und ich muss ihn decken. Eine gesunde Stellung, fast so schön wie meine.',
        },
      ],
      abschluss:
        'Bei Sizilianisch hast du am Ende einen Mittelbauern mehr als Weiß. Dafür ist Weiß schneller entwickelt. Bring deine Figuren zügig heraus und rochiere mit Stil.',
    },
    Gemuetlich: {
      einleitung: 'Du spielst Schwarz. Ich fang mit e4 an. Die Sizilianische Verteidigung ist eine sehr beliebte Antwort darauf: Du kämpfst von der Seite um die Mitte.',
      schritte: [
        {
          hinweis: 'Ach, diesmal nicht e5. Zieh den Bauern von c7 nach c5, der kontrolliert d4.',
          erklaerung: 'Das ist Sizilianisch. Dein Bauer auf c5 hat das Feld d4 im Blick. Ich bring meinen Springer.',
        },
        {
          hinweis: 'Ein kleiner Bauernzug: d7 nach d6. Der macht den Weg frei für deinen Läufer auf c8.',
          erklaerung: 'Schön. Jetzt öffne ich die Mitte mit d4, mal schauen.',
        },
        {
          hinweis: 'Mein Bauer auf d4 steht in Reichweite deines c-Bauern. Nimm ihn ruhig.',
          erklaerung: 'Genau so. Du tauschst deinen Randbauern gegen meinen Mittelbauern. Ich schlag mit dem Springer zurück.',
        },
        {
          hinweis: 'Jetzt noch der Springer auf g8, der kommt nach f6.',
          erklaerung: 'Wunderbar! Dein Springer greift e4 an, und ich muss ihn decken. Du stehst gesund da, passt scho.',
        },
      ],
      abschluss:
        'Bei Sizilianisch hast du am Ende einen Mittelbauern mehr als Weiß. Dafür ist Weiß schneller entwickelt. Bring deine Figuren zügig raus und rochiere, dann passt das.',
    },
  },
}

/**
 * Die Uebung im Ton der Persoenlichkeit. Fehlt die Stimme oder passt sie nicht zum Katalog (andere
 * Schrittzahl), bleibt der neutrale Text.
 */
export function mitStimme(uebung: Uebung, persoenlichkeit: Persoenlichkeit): Uebung {
  const stimme = STIMMEN[uebung.key]?.[persoenlichkeit]
  if (stimme === undefined || stimme.schritte.length !== uebung.schritte.length) return uebung
  return {
    ...uebung,
    einleitung: stimme.einleitung,
    abschluss: stimme.abschluss,
    schritte: uebung.schritte.map((schritt, i) => ({ ...schritt, ...stimme.schritte[i] })),
  }
}
