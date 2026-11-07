import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Brett } from '../brett/Brett'
import { de, t } from '../i18n/de'
import { schachFeld, sichtbarerSpruch } from './anzeige'
import { BottomSheet, Schalter } from '../components/BottomSheet'
import { Dialog } from '../components/Dialog'
import { useEinstellungen } from './einstellungen'
import { Endeblatt } from './Endeblatt'
import { knoepfe as knoepfeFuer, type KnopfArt } from './knoepfe'
import { EigeneZeile, Knopfleiste, Kopfzeile, Spruchblase, Statuszeile, Uhr, type StatusArt } from './teile'
import { fuelle } from './text'
import { aktuellerTipp, naechsterTippSchritt, tippMarkierung, tippText, zeigePfeil } from './tipp'
import { usePartie } from './usePartie'
import { ungedeckteFiguren, warnungsText } from './warnung'
import './partie.css'

type Offen = 'keins' | 'menue' | 'aufgeben' | 'eloTipp' | 'eloZuruecknehmen'

/** Partie-Bildschirm, Layout `Fest` (SCHACH-SPEC 10.1, 10.3). */
export function Partie({ id, neuePartiePfad = '/neu' }: { id: string; neuePartiePfad?: string }) {
  const navigate = useNavigate()
  const [einstellungen, setzeEinstellung] = useEinstellungen()
  const p = usePartie(id, einstellungen.toeneUndVibration)
  const [offen, setOffen] = useState<Offen>('keins')
  const [endeGeschlossen, setEndeGeschlossen] = useState(false)

  const z = p.zustand
  const anzeige = p.anzeige
  const spielerAmZug = z !== null && anzeige !== null && z.status === 'Offen' && z.amZug && !anzeige.gegnerDenkt
  const eingabeAktiv = spielerAmZug && !p.beschaeftigt && !p.offline
  const fen = anzeige?.fen ?? null

  const warnungen = useMemo(
    () => (fen !== null && z !== null && spielerAmZug && einstellungen.vorUngedecktenWarnen ? ungedeckteFiguren(fen, z.spielerFarbe) : []),
    [fen, z, spielerAmZug, einstellungen.vorUngedecktenWarnen],
  )
  const schach = useMemo(() => (fen !== null ? schachFeld(fen) : null), [fen])

  if (z === null || anzeige === null) {
    return (
      <div className="partie partie--leer">
        {p.lade === 'laedt' ? (
          <p className="text-muted">{t('partie.laedt')}</p>
        ) : (
          <>
            <p>{p.lade === 'nichtGefunden' ? t('partie.nichtGefunden') : t('allgemein.fehler')}</p>
            <Link className="knopf" to="/">
              {t('allgemein.zurStartseite')}
            </Link>
          </>
        )}
      </div>
    )
  }

  const gast = p.gast === true
  const tipp = spielerAmZug ? aktuellerTipp(p.tippStand, z.halbzug) : null
  const markierung = tippMarkierung(tipp)
  const endeSichtbar = z.status === 'Beendet' && !anzeige.gegnerDenkt
  const spruch = sichtbarerSpruch(z.sprueche, anzeige.halbzug)

  // Statuszeile, Vorrang: Keine Verbindung > Hinweis > Tipp > Warnung > Schach! > denkt nach.
  let status: { art: StatusArt; text: string } = { art: 'leer', text: '' }
  if (p.offline) status = { art: 'offline', text: de.status.keineVerbindung }
  else if (p.hinweis !== null) status = { art: 'hinweis', text: p.hinweis }
  else if (tipp !== null) status = { art: 'tipp', text: tippText(tipp) }
  else if (warnungen[0] !== undefined) status = { art: 'warnung', text: warnungsText(warnungen[0]) }
  else if (spielerAmZug && schach !== null) status = { art: 'schach', text: de.status.schach }
  else if (z.status === 'Offen' && (anzeige.gegnerDenkt || p.limitWartet)) {
    status = { art: 'denkt', text: fuelle(de.status.denktNach, { name: z.gegner.name }) }
  }

  const leiste = knoepfeFuer(z, {
    gast,
    gesperrt: p.beschaeftigt || p.offline || anzeige.gegnerDenkt,
    tippFertig: tipp?.stufe === 2,
  })

  // Einmalige Elo-Frage: nur Spieler, nur solange die Partie zaehlt (10.3). Danach zaehlt sie nicht.
  const mussFragen = !gast && z.zaehltFuerElo

  const aufKnopf = (art: KnopfArt) => {
    if (art === 'aufgeben') setOffen('aufgeben')
    else if (art === 'remis') p.bieteRemis()
    else if (art === 'zuruecknehmen') {
      if (mussFragen) setOffen('eloZuruecknehmen')
      else p.nimmZurueck()
    } else if (art === 'tipp') {
      const schritt = naechsterTippSchritt(p.tippStand, z.halbzug)
      if (schritt === 'pfeil' && tipp !== null) p.setTippStand(zeigePfeil(tipp))
      else if (schritt === 'abrufen') {
        if (mussFragen) setOffen('eloTipp')
        else p.holeTipp()
      }
    } else if (art === 'nachspielen') navigate(`/nachspielen/${z.id}`)
    else navigate(neuePartiePfad)
  }

  const aufgebenText = gast
    ? t('dialog.aufgebenGast')
    : z.zaehltFuerElo
      ? t('dialog.aufgebenZaehlt')
      : t('dialog.aufgebenZaehltNicht')

  const gegnerUhr =
    z.uhr !== null ? (
      <Uhr
        uhr={z.uhr}
        seite="Gegner"
        versatzMs={p.versatzMs}
        aktiv={z.status === 'Offen' && !spielerAmZug}
        label={fuelle(de.partie.uhrGegner, { name: z.gegner.name })}
      />
    ) : null
  const eigeneUhr =
    z.uhr !== null ? (
      <Uhr
        uhr={z.uhr}
        seite="Spieler"
        versatzMs={p.versatzMs}
        aktiv={spielerAmZug}
        label={t('partie.uhrEigene')}
      />
    ) : null

  return (
    <div className="partie">
      <Kopfzeile
        gegner={z.gegner}
        amZug={z.status === 'Offen' && !spielerAmZug}
        uhr={gegnerUhr}
        onMenue={() => setOffen('menue')}
      />
      <Spruchblase text={spruch?.text ?? null} />
      <div className="partie__brett">
        <Brett
          fen={anzeige.fen}
          orientierung={z.spielerFarbe}
          erlaubteZuege={eingabeAktiv ? z.erlaubteZuege : []}
          eingabeAktiv={eingabeAktiv}
          zeigeZiele={einstellungen.erlaubteZuegeAnzeigen}
          letzterZug={anzeige.letzterZug}
          schach={schach}
          warnungen={warnungen.map((w) => w.feld)}
          tippFeld={markierung.feld}
          tippPfeil={markierung.pfeil}
          onZug={p.ziehe}
        />
      </div>
      <Statuszeile art={status.art} text={status.text} />
      <EigeneZeile
        farbe={z.spielerFarbe}
        amZug={spielerAmZug}
        pille={gast ? t('partie.pilleGast') : z.zaehltFuerElo ? null : t('partie.pilleZaehltNicht')}
        uhr={eigeneUhr}
      />
      <Knopfleiste knoepfe={leiste} onKnopf={aufKnopf} />

      {offen === 'menue' && (
        <BottomSheet titel={t('menue.titel')} onSchliessen={() => setOffen('keins')}>
          <Schalter
            text={t('menue.erlaubteZuegeAnzeigen')}
            an={einstellungen.erlaubteZuegeAnzeigen}
            onWechsel={(an) => setzeEinstellung('erlaubteZuegeAnzeigen', an)}
          />
          <Schalter
            text={t('menue.vorUngedecktenWarnen')}
            an={einstellungen.vorUngedecktenWarnen}
            onWechsel={(an) => setzeEinstellung('vorUngedecktenWarnen', an)}
          />
          <Schalter
            text={t('menue.toeneUndVibration')}
            an={einstellungen.toeneUndVibration}
            onWechsel={(an) => setzeEinstellung('toeneUndVibration', an)}
          />
          <button type="button" className="sheet__zeile" onClick={() => navigate('/')}>
            {t('menue.partieVerlassen')}
          </button>
        </BottomSheet>
      )}

      {offen === 'aufgeben' && (
        <Dialog
          titel={t('dialog.aufgebenTitel')}
          onSchliessen={() => setOffen('keins')}
          knoepfe={
            <>
              <button type="button" className="knopf knopf--outline" onClick={() => setOffen('keins')}>
                {t('dialog.abbrechen')}
              </button>
              <button
                type="button"
                className="knopf knopf--gefahr"
                onClick={() => {
                  setOffen('keins')
                  p.gibAuf()
                }}
              >
                {t('dialog.aufgebenKnopf')}
              </button>
            </>
          }
        >
          {aufgebenText}
        </Dialog>
      )}

      {(offen === 'eloTipp' || offen === 'eloZuruecknehmen') && (
        <Dialog
          titel={t('dialog.eloTitel')}
          onSchliessen={() => setOffen('keins')}
          knoepfe={
            <>
              <button type="button" className="knopf knopf--outline" onClick={() => setOffen('keins')}>
                {t('dialog.abbrechen')}
              </button>
              <button
                type="button"
                className="knopf"
                onClick={() => {
                  setOffen('keins')
                  if (offen === 'eloTipp') p.holeTipp()
                  else p.nimmZurueck()
                }}
              >
                {offen === 'eloTipp' ? t('dialog.eloTippKnopf') : t('dialog.eloZuruecknehmenKnopf')}
              </button>
            </>
          }
        >
          {offen === 'eloTipp' ? t('dialog.eloTippText') : t('dialog.eloZuruecknehmenText')}
        </Dialog>
      )}

      {endeSichtbar && !endeGeschlossen && (
        <Endeblatt
          zustand={z}
          gast={gast}
          spruch={spruch}
          onSchliessen={() => setEndeGeschlossen(true)}
          onNeuePartie={() => navigate(neuePartiePfad)}
          onNachspielen={() => navigate(`/nachspielen/${z.id}`)}
        />
      )}
    </div>
  )
}
