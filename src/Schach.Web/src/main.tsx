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

const router = createRouter()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
)
