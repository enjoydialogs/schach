import { createBrowserRouter, type RouteObject } from 'react-router'
import { NichtGefunden } from './pages/NichtGefunden'
import { Start } from './pages/Start'
import { Datenschutz } from './recht/Datenschutz'
import { Impressum } from './recht/Impressum'

// Routen nach SCHACH-SPEC 10.2. Append-Register (15.1 Regel 5): neue Routen vor `*` anhaengen.
// Offen: /neu, /partie/:id, /partien, /nachspielen/:id, /profil, /anmelden (S3a, S3b, S2a),
// /impressum, /datenschutz (S3c).
export const routes: RouteObject[] = [
  { path: '/', element: <Start /> },
  { path: '/impressum', element: <Impressum /> },
  { path: '/datenschutz', element: <Datenschutz /> },
  { path: '*', element: <NichtGefunden /> },
]

export function createRouter() {
  return createBrowserRouter(routes)
}
