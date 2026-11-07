import { createBrowserRouter, type RouteObject } from 'react-router'
import { NichtGefunden } from './pages/NichtGefunden'
import { Datenschutz } from './recht/Datenschutz'
import { Impressum } from './recht/Impressum'
import { PartieSeite } from './partie/PartieSeite'
// S3b (SCHACH-SPEC 10.2): Start ersetzt den Platzhalter aus pages/; die uebrigen Bildschirme.
import { Anmelden } from './anmelden/Anmelden'
import { RequireSpieler } from './components/RequireSpieler'
import { NeuePartie } from './gegner/NeuePartie'
import { Nachspielen } from './nachspielen/Nachspielen'
import { Partienliste } from './partien/Partienliste'
import { Profil } from './profil/Profil'
import { Start } from './start/Start'

// Routen nach SCHACH-SPEC 10.2. Append-Register (15.1 Regel 5): neue Routen vor `*` anhaengen.
// Offen: /neu, /partie/:id, /partien, /nachspielen/:id, /profil, /anmelden (S3a, S3b, S2a),
// /impressum, /datenschutz (S3c).
export const routes: RouteObject[] = [
  { path: '/', element: <Start /> },
  { path: '/impressum', element: <Impressum /> },
  { path: '/datenschutz', element: <Datenschutz /> },
  { path: '/partie/:id', element: <PartieSeite /> },
  // S3a: Dev-Route gegen den MSW-Fake, nur in `npm run dev` und im Bau-Modus `prototyp`.
  ...(import.meta.env.DEV || import.meta.env.MODE === 'prototyp'
    ? [{ path: '/prototyp', lazy: async () => ({ Component: (await import('./fake/Prototyp')).Prototyp }) }]
    : []),
  // S3b
  { path: '/neu', element: <NeuePartie /> },
  { path: '/partien', element: <RequireSpieler><Partienliste /></RequireSpieler> },
  { path: '/nachspielen/:id', element: <RequireSpieler><Nachspielen /></RequireSpieler> },
  { path: '/profil', element: <RequireSpieler><Profil /></RequireSpieler> },
  { path: '/anmelden', element: <Anmelden /> },
  { path: '*', element: <NichtGefunden /> },
]

export function createRouter() {
  return createBrowserRouter(routes)
}
