import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router/dom'
// Schrift selbst gehostet, kein Fremd-Host (SCHACH-SPEC 12.3).
import '@fontsource/inter/400.css'
import '@fontsource/inter/600.css'
import '@fontsource/inter/700.css'
import './styles/palette.css'
import './styles/base.css'
import { createRouter } from './router'

// S3b: Der MSW-Fake bedient die ganze App im Bau-Modus `prototyp` und mit `VITE_FAKE=1` im
// Dev-Server (alle Bildschirme ohne Backend). Im Produktionsbau faellt der Zweig samt Import weg.
const FAKE_FUER_ALLES =
  import.meta.env.MODE === 'prototyp' || (import.meta.env.DEV && import.meta.env.VITE_FAKE === '1')

async function starte() {
  if (FAKE_FUER_ALLES) await (await import('./fake/starten')).starteFake()
  const router = createRouter()
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <RouterProvider router={router} />
    </StrictMode>,
  )
}

void starte()
