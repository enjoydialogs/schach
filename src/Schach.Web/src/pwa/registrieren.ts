import { registerSW } from 'virtual:pwa-register'
import { fangeInstallPrompt } from './install'
import { starteUpdates } from './update'

// Einziger Import von `virtual:pwa-register` (vite-plugin-pwa). Manuelle Registrierung aus dem
// App-Bundle statt eines Inline-Skripts in index.html: die CSP erlaubt nur `script-src 'self'`.
// Im Dev-Server ist der SW aus (devOptions nicht gesetzt), `registerSW` tut dann nichts.

export function startePwa(): void {
  fangeInstallPrompt()
  starteUpdates(registerSW)
}
