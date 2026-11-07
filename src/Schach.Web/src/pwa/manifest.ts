// Web-App-Manifest (SCHACH-SPEC 10.13). vite.config.ts reicht es an vite-plugin-pwa weiter, das
// daraus `manifest.webmanifest` schreibt und den `<link rel="manifest">` in index.html setzt.
// Die Icons erzeugt `node scripts/icons.mjs` nach public/icons/ (nie public/assets/).

import type { ManifestOptions } from 'vite-plugin-pwa'

export const FARBE = '#F6F4EE'

export const manifest = {
  id: '/',
  name: 'Schach',
  short_name: 'Schach',
  lang: 'de',
  dir: 'ltr',
  start_url: '/',
  scope: '/',
  display: 'standalone',
  orientation: 'portrait',
  background_color: FARBE,
  theme_color: FARBE,
  icons: [
    { src: '/icons/icon-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
    { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
    { src: '/icons/icon-512-maskable.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
  ],
} satisfies Partial<ManifestOptions>
