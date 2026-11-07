import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { api, ApiFehler } from '../api/client'
import type { AnmeldungAnfrage, AnmeldungAntwort, BestaetigenAnfrage, BestaetigenAntwort } from '../api/types'
import { Ladeanzeige } from '../components/Ladeanzeige'
import { Seite } from '../components/Seite'
import { anmeldungId, gastPartieId } from '../components/SpeicherSchluessel'
import { ladeSitzung, useSitzung } from '../components/sitzung'
import { de, t } from '../i18n/de'
import { fuelle } from '../partie/text'
import { linkToken, nurZiffern, restSekunden } from './linkCode'

type Phase = 'form' | 'gesendet' | 'link'

function sendeFehler(e: unknown): string {
  if (e instanceof ApiFehler && e.fehler === 'EmailUngueltig') return de.anmelden.emailUngueltig
  if (e instanceof ApiFehler && e.fehler === 'MailNichtGesendet') return de.anmelden.mailNichtGesendet
  if (e instanceof ApiFehler && e.status === 429) return de.anmelden.zuViele
  return de.allgemein.fehler
}

/**
 * Anmelden (`/anmelden`, SCHACH-SPEC 9.5, 10.10). Mit Fragment `#t=` die Link-Landung: erst die
 * Sitzung pruefen, dann der Knopf „Jetzt anmelden“ (nie beim Laden absenden). Nach der Anfrage
 * der Code-Weg mit `anmeldungId` im sessionStorage.
 */
export function Anmelden() {
  const navigate = useNavigate()
  const { hash } = useLocation()
  const sitzung = useSitzung()
  const token = linkToken(hash)
  const [phase, setPhase] = useState<Phase>(() =>
    token !== null && token !== '' ? 'link' : anmeldungId.lies() !== null ? 'gesendet' : 'form',
  )
  const [email, setEmail] = useState('')
  const [gesendetAn, setGesendetAn] = useState<string | null>(null)
  const [gesendetAm, setGesendetAm] = useState<number | null>(null)
  const [code, setCode] = useState('')
  const [fehler, setFehler] = useState<string | null>(token === '' ? de.anmelden.linkUnvollstaendig : null)
  const [beschaeftigt, setBeschaeftigt] = useState(false)
  const [jetzt, setJetzt] = useState(() => Date.now())

  const rest = gesendetAm === null ? 0 : restSekunden(gesendetAm, jetzt)

  // Countdown fuer „Erneut senden“.
  useEffect(() => {
    if (rest <= 0) return
    const takt = setInterval(() => setJetzt(Date.now()), 1_000)
    return () => clearInterval(takt)
  }, [rest])

  // Wieder sichtbar (z. B. nach dem Link in der Mail-App): Sitzung erneut pruefen (9.5).
  useEffect(() => {
    const sichtbar = () => {
      if (document.visibilityState !== 'hidden') void ladeSitzung()
    }
    document.addEventListener('visibilitychange', sichtbar)
    return () => document.removeEventListener('visibilitychange', sichtbar)
  }, [])

  // Waehrend des Code-Wegs anderswo angemeldet: weiter zur Startseite.
  useEffect(() => {
    if (phase === 'gesendet' && sitzung.status === 'spieler') {
      anmeldungId.setze(null)
      navigate('/', { replace: true })
    }
  }, [phase, sitzung.status, navigate])

  const sende = async (adresse: string) => {
    setBeschaeftigt(true)
    setFehler(null)
    try {
      const antwort = await api.post<AnmeldungAntwort>('/auth/anmeldung', { email: adresse } satisfies AnmeldungAnfrage)
      anmeldungId.setze(antwort.anmeldungId)
      setGesendetAn(adresse)
      setGesendetAm(Date.now())
      setJetzt(Date.now())
      setCode('')
      setPhase('gesendet')
    } catch (e) {
      setFehler(sendeFehler(e))
    } finally {
      setBeschaeftigt(false)
    }
  }

  const bestaetige = async (anfrage: BestaetigenAnfrage, ungueltig: () => void) => {
    setBeschaeftigt(true)
    setFehler(null)
    try {
      await api.post<BestaetigenAntwort>('/auth/anmeldung/bestaetigen', anfrage)
      anmeldungId.setze(null)
      // Beim Anmelden wird eine Gast-Partie verworfen (8.8).
      gastPartieId.setze(null)
      await ladeSitzung()
      navigate('/', { replace: true })
    } catch (e) {
      if (e instanceof ApiFehler && e.status === 422) ungueltig()
      else setFehler(e instanceof ApiFehler && e.status === 429 ? de.anmelden.zuViele : de.allgemein.fehler)
      setBeschaeftigt(false)
    }
  }

  const absenden = (e: FormEvent) => {
    e.preventDefault()
    void sende(email.trim())
  }

  const codeAbsenden = (e: FormEvent) => {
    e.preventDefault()
    const id = anmeldungId.lies()
    if (id === null) {
      setPhase('form')
      return
    }
    void bestaetige({ anmeldungId: id, code: nurZiffern(code) }, () => setFehler(de.anmelden.codeUngueltig))
  }

  const andereAdresse = () => {
    anmeldungId.setze(null)
    setPhase('form')
    setFehler(null)
    setGesendetAm(null)
  }

  let inhalt
  if ((phase === 'link' && sitzung.status === 'laedt') || (sitzung.status === 'laedt' && phase === 'form')) {
    inhalt = <Ladeanzeige />
  } else if (sitzung.status === 'spieler' && phase !== 'gesendet') {
    inhalt = (
      <div className="leer">
        <p>{t('anmelden.schonAngemeldet')}</p>
        <Link className="knopf" to="/">
          {t('allgemein.zurStartseite')}
        </Link>
      </div>
    )
  } else if (phase === 'link') {
    inhalt = (
      <>
        <p>{t('anmelden.linkText')}</p>
        {fehler !== null && (
          <p className="fehlertext" role="alert">
            {fehler}
          </p>
        )}
        <button
          type="button"
          className="knopf knopf--breit"
          disabled={beschaeftigt}
          onClick={() =>
            void bestaetige({ token: token ?? '' }, () => {
              setPhase('form')
              setFehler(de.anmelden.linkUngueltig)
            })
          }
        >
          {t('anmelden.jetztAnmelden')}
        </button>
      </>
    )
  } else if (phase === 'gesendet') {
    inhalt = (
      <>
        <p>{t('anmelden.gesendet')}</p>
        {fehler !== null && (
          <p className="fehlertext" role="alert">
            {fehler}
          </p>
        )}
        <form onSubmit={codeAbsenden} noValidate>
          <label className="feld feld--code">
            {t('anmelden.code')}
            <input
              type="text"
              inputMode="numeric"
              autoComplete="one-time-code"
              maxLength={7}
              value={code}
              onChange={(e) => setCode(e.target.value)}
            />
          </label>
          <button type="submit" className="knopf knopf--breit" disabled={beschaeftigt || nurZiffern(code).length !== 6}>
            {t('anmelden.codeAnmelden')}
          </button>
        </form>
        <div className="anmelden__weitere">
          {gesendetAn !== null && (
            <button
              type="button"
              className="knopf knopf--text"
              disabled={beschaeftigt || rest > 0}
              onClick={() => void sende(gesendetAn)}
            >
              {rest > 0 ? fuelle(de.anmelden.erneutSendenIn, { sekunden: rest }) : t('anmelden.erneutSenden')}
            </button>
          )}
          <button type="button" className="knopf knopf--text" onClick={andereAdresse}>
            {t('anmelden.andereAdresse')}
          </button>
        </div>
      </>
    )
  } else {
    inhalt = (
      <>
        <p>{t('anmelden.text')}</p>
        {gastPartieId.lies() !== null && <p className="karte karte--gast">{t('anmelden.gastPartieEndet')}</p>}
        <form onSubmit={absenden} noValidate>
          <label className="feld">
            {t('anmelden.email')}
            <input
              type="email"
              inputMode="email"
              autoComplete="email"
              maxLength={254}
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </label>
          {fehler !== null && (
            <p className="fehlertext" role="alert">
              {fehler}
            </p>
          )}
          <button type="submit" className="knopf knopf--breit" disabled={beschaeftigt || email.trim() === ''}>
            {t('anmelden.senden')}
          </button>
        </form>
        <div className="anmelden__weitere">
          <Link to="/datenschutz">{t('anmelden.datenschutz')}</Link>
          <Link to="/">{t('anmelden.alsGast')}</Link>
        </div>
      </>
    )
  }

  return <Seite titel={t('anmelden.titel')}>{inhalt}</Seite>
}
