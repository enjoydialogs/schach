import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { BottomSheet } from './BottomSheet'
import { Dialog } from './Dialog'

function MitDialog({ onSchliessen }: { onSchliessen?: () => void }) {
  const [offen, setOffen] = useState(false)
  return (
    <>
      <button type="button" onClick={() => setOffen(true)}>
        Oeffnen
      </button>
      {offen && (
        <Dialog
          titel="Partie aufgeben?"
          onSchliessen={() => {
            onSchliessen?.()
            setOffen(false)
          }}
          knoepfe={
            <>
              <button type="button">Aufgeben</button>
              <button type="button">Abbrechen</button>
            </>
          }
        >
          Text
        </Dialog>
      )}
    </>
  )
}

describe('Dialog', () => {
  it('nimmt beim Oeffnen den Fokus und gibt ihn beim Schliessen zurueck', async () => {
    const user = userEvent.setup()
    render(<MitDialog />)
    const ausloeser = screen.getByRole('button', { name: 'Oeffnen' })

    await user.click(ausloeser)
    expect(screen.getByRole('dialog')).toHaveFocus()

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(ausloeser).toHaveFocus()
  })

  it('haelt den Tab-Fokus im Dialog', async () => {
    const user = userEvent.setup()
    render(<MitDialog />)
    await user.click(screen.getByRole('button', { name: 'Oeffnen' }))

    await user.tab()
    expect(screen.getByRole('button', { name: 'Aufgeben' })).toHaveFocus()
    await user.tab()
    expect(screen.getByRole('button', { name: 'Abbrechen' })).toHaveFocus()
    await user.tab()
    expect(screen.getByRole('button', { name: 'Aufgeben' })).toHaveFocus()
  })

  it('schliesst bei Escape nur die oberste Ueberlage', async () => {
    const user = userEvent.setup()
    const sheetZu = vi.fn()
    const dialogZu = vi.fn()
    render(
      <>
        <BottomSheet titel="Menue" onSchliessen={sheetZu}>
          Inhalt
        </BottomSheet>
        <Dialog titel="Sicher?" onSchliessen={dialogZu} knoepfe={<button type="button">OK</button>}>
          Text
        </Dialog>
      </>,
    )

    await user.keyboard('{Escape}')
    expect(dialogZu).toHaveBeenCalledTimes(1)
    expect(sheetZu).not.toHaveBeenCalled()
  })
})
