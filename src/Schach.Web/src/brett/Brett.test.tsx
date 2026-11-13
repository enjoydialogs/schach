import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, vi } from 'vitest'
import { Brett, type BrettProps } from './Brett'

const START = 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1'
const UMWANDLUNG = '4k3/P7/8/8/8/8/8/4K3 w - - 0 1'

function feld(name: string): HTMLElement {
  const element = document.querySelector<HTMLElement>(`[data-square="${name}"]`)
  if (!element) throw new Error(`Feld ${name} fehlt`)
  return element
}

/** Ebene mit den squareStyles (react-chessboard legt sie auf das innere div des Felds). */
function ebene(name: string): CSSStyleDeclaration {
  const innen = feld(name).querySelector<HTMLElement>(':scope > div:last-child')
  if (!innen) throw new Error(`Ebene ${name} fehlt`)
  return innen.style
}

function zeige(teil: Partial<BrettProps> = {}) {
  const onZug = vi.fn()
  const props: BrettProps = {
    fen: START,
    orientierung: 'Weiss',
    erlaubteZuege: ['e2e4', 'e2e3', 'g1f3', 'g1h3'],
    eingabeAktiv: true,
    zeigeZiele: true,
    onZug,
    ...teil,
  }
  const ansicht = render(<Brett {...props} />)
  return { onZug, ansicht, props }
}

afterEach(() => vi.restoreAllMocks())

describe('Brett', () => {
  it('rendert 64 Felder mit Cburnett-Figuren', () => {
    zeige()
    expect(document.querySelectorAll('[data-square]')).toHaveLength(64)
    expect(feld('e1').querySelector('svg')).not.toBeNull()
  })

  it('zieht per Tippen-Tippen', () => {
    const { onZug } = zeige()
    fireEvent.click(feld('e2'))
    fireEvent.click(feld('e4'))
    expect(onZug).toHaveBeenCalledWith('e2e4')
  })

  it('zeigt nach der Auswahl Punkte auf den Zielen aus der Serverliste', () => {
    zeige()
    fireEvent.click(feld('g1'))
    expect(ebene('g1').backgroundImage).toContain('--overlay-auswahl')
    expect(ebene('f3').backgroundImage).toContain('--overlay-ziel')
    expect(ebene('h3').backgroundImage).toContain('--overlay-ziel')
    expect(ebene('e4').backgroundImage).toBe('')
  })

  it('zeigt ohne Lernhilfe keine Punkte, waehlt aber aus', () => {
    zeige({ zeigeZiele: false })
    fireEvent.click(feld('g1'))
    expect(ebene('g1').backgroundImage).toContain('--overlay-auswahl')
    expect(ebene('f3').backgroundImage).toBe('')
  })

  it('ignoriert Tippen ohne Eingabe', () => {
    const { onZug } = zeige({ eingabeAktiv: false })
    fireEvent.click(feld('e2'))
    fireEvent.click(feld('e4'))
    expect(onZug).not.toHaveBeenCalled()
    expect(ebene('e2').backgroundImage).toBe('')
  })

  it('waehlt keine gegnerische Figur', () => {
    zeige()
    fireEvent.click(feld('e7'))
    expect(ebene('e7').backgroundImage).toBe('')
  })

  it('verwirft die Auswahl, wenn sich die Stellung aendert', () => {
    // jsdom kennt keine Layout-Masse; die Zug-Animation braucht eine Feldbreite.
    vi.spyOn(Element.prototype, 'getBoundingClientRect').mockReturnValue(new DOMRect(0, 0, 48, 48))
    const { ansicht, props } = zeige()
    fireEvent.click(feld('g1'))
    ansicht.rerender(<Brett {...props} fen="rnbqkbnr/pppppppp/8/8/8/5N2/PPPPPPPP/RNBQKB1R w KQkq - 0 1" />)
    expect(ebene('g1').backgroundImage).toBe('')
  })

  it('oeffnet die Umwandlung und zieht mit der gewaehlten Figur', () => {
    const { onZug } = zeige({ fen: UMWANDLUNG, erlaubteZuege: ['a7a8q', 'a7a8r', 'a7a8b', 'a7a8n'] })
    fireEvent.click(feld('a7'))
    fireEvent.click(feld('a8'))
    expect(onZug).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Springer' }))
    expect(onZug).toHaveBeenCalledWith('a7a8n')
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('bricht die Umwandlung beim Tippen daneben ab', () => {
    const { onZug } = zeige({ fen: UMWANDLUNG, erlaubteZuege: ['a7a8q', 'a7a8r', 'a7a8b', 'a7a8n'] })
    fireEvent.click(feld('a7'))
    fireEvent.click(feld('a8'))
    expect(screen.getByRole('dialog', { name: 'Umwandeln in' })).toBeInTheDocument()
    fireEvent.click(screen.getByTestId('umwandlung-hintergrund'))
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(onZug).not.toHaveBeenCalled()
  })

  it('markiert letzten Zug, Schach, Warnung und Tipp-Ring', () => {
    zeige({ letzterZug: { von: 'e7', nach: 'e5' }, schach: 'e1', warnungen: ['b1'], tippFeld: 'g1' })
    expect(ebene('e5').backgroundImage).toContain('--overlay-letzter-zug')
    expect(ebene('e1').backgroundImage).toContain('radial-gradient')
    expect(ebene('b1').backgroundImage).toContain('--overlay-warnung')
    expect(ebene('g1').boxShadow).toBe('var(--tipp-ring)')
  })

  describe('Tastatur', () => {
    const brett = () => screen.getByRole('application', { name: /Schachbrett/ })
    const ansage = () => screen.getByTestId('brett-ansage').textContent

    it('ist genau ein Tab-Stopp, die Figuren nicht', async () => {
      const user = userEvent.setup()
      zeige()
      await user.tab()
      expect(brett()).toHaveFocus()
      await user.tab()
      expect(brett().contains(document.activeElement)).toBe(false)
    })

    it('waehlt und zieht mit Pfeilen und Enter, beginnend am eigenen Koenig', async () => {
      const user = userEvent.setup()
      const { onZug } = zeige()
      await user.tab()
      expect(ansage()).toBe('e1, weißer König')
      expect(ebene('e1').boxShadow).toBe('var(--overlay-cursor)')
      await user.keyboard('{ArrowUp}')
      expect(ansage()).toBe('e2, weißer Bauer')
      await user.keyboard('{Enter}')
      expect(ansage()).toBe('Bauer e2 gewählt')
      expect(ebene('e2').backgroundImage).toContain('--overlay-auswahl')
      await user.keyboard('{ArrowUp}{ArrowUp}')
      expect(ansage()).toBe('e4, leer, Zug möglich')
      await user.keyboard(' ')
      expect(onZug).toHaveBeenCalledWith('e2e4')
      expect(ansage()).toBe('Zug e2 nach e4')
    })

    it('spiegelt die Pfeile, wenn Schwarz unten sitzt', async () => {
      const user = userEvent.setup()
      const { onZug } = zeige({
        fen: 'rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1',
        orientierung: 'Schwarz',
        erlaubteZuege: ['e7e5', 'e7e6'],
      })
      await user.tab()
      expect(ansage()).toBe('e8, schwarzer König')
      await user.keyboard('{ArrowRight}')
      expect(ansage()).toBe('d8, schwarze Dame')
      await user.keyboard('{ArrowLeft}{ArrowUp}')
      expect(ansage()).toBe('e7, schwarzer Bauer')
      await user.keyboard('{Enter}{ArrowUp}{ArrowUp}{Enter}')
      expect(onZug).toHaveBeenCalledWith('e7e5')
    })

    it('hebt die Auswahl mit Escape auf', async () => {
      const user = userEvent.setup()
      const { onZug } = zeige()
      await user.tab()
      await user.keyboard('{ArrowUp}{Enter}{Escape}')
      expect(ansage()).toBe('Auswahl aufgehoben')
      expect(ebene('e2').backgroundImage).toBe('')
      await user.keyboard('{ArrowUp}{ArrowUp}{Enter}')
      expect(onZug).not.toHaveBeenCalled()
    })

    it('liest ohne Eingabe vor, waehlt aber nichts', async () => {
      const user = userEvent.setup()
      zeige({ eingabeAktiv: false })
      await user.tab()
      await user.keyboard('{ArrowUp}{Enter}')
      expect(ansage()).toBe('Gerade kein Zug möglich')
      expect(ebene('e2').backgroundImage).toBe('')
    })

    it('fuehrt die Umwandlung per Tastatur bis zum Zug', async () => {
      const user = userEvent.setup()
      const { onZug } = zeige({ fen: UMWANDLUNG, erlaubteZuege: ['a7a8q', 'a7a8r', 'a7a8b', 'a7a8n'] })
      await user.tab()
      // e1 -> a1 -> a7
      await user.keyboard('{Home}{ArrowUp>6/}{Enter}{ArrowUp}{Enter}')
      expect(screen.getByRole('dialog', { name: 'Umwandeln in' })).toBeInTheDocument()
      await user.tab()
      expect(screen.getByRole('button', { name: 'Dame' })).toHaveFocus()
      await user.keyboard('{Enter}')
      expect(onZug).toHaveBeenCalledWith('a7a8q')
      expect(brett()).toHaveFocus()
    })

    it('zeigt den Cursor nach Tippen nicht', async () => {
      const user = userEvent.setup()
      zeige()
      await user.tab()
      expect(ebene('e1').boxShadow).toBe('var(--overlay-cursor)')
      await user.click(feld('e2'))
      expect(ebene('e1').boxShadow).toBe('')
      expect(ebene('e2').boxShadow).toBe('')
    })
  })
})
