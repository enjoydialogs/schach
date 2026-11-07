/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'
import { manifest } from './src/pwa/manifest.ts'

// Schach.Api, Profil "http" (Properties/launchSettings.json).
const apiProxy = process.env.SCHACH_API_PROXY ?? 'http://localhost:5252'

export default defineConfig({
  plugins: [
    react(),
    // PWA (SCHACH-SPEC 10.13, S3d): generateSW, Update nur nach Zustimmung ("prompt"). Registriert
    // wird von Hand in src/pwa/registrieren.ts, weil die CSP keine Inline-Skripte erlaubt.
    VitePWA({
      strategies: 'generateSW',
      registerType: 'prompt',
      injectRegister: false,
      manifest,
      includeManifestIcons: true,
      workbox: {
        // App-Shell und Inter (nur woff2). Figuren (figuren.tsx) und Rechtstexte stecken im JS;
        // Manifest und Icons nimmt das Plugin selbst auf (includeManifestIcons).
        globPatterns: ['**/*.{js,css,html,woff2,svg}'],
        navigateFallback: '/index.html',
        // /api/ cacht der Service Worker nie; beendete Partien verwaltet die App (partienCache.ts).
        navigateFallbackDenylist: [/^\/api\//],
        cleanupOutdatedCaches: true,
      },
    }),
  ],
  build: {
    // Schriften nie als data:-URI einbetten: die CSP setzt font-src 'self' (SecurityHeaders.cs).
    // Alles andere behaelt Vites Standardschwelle.
    assetsInlineLimit: (filePath) => (/\.(woff2?|ttf|otf|eot)$/i.test(filePath) ? false : undefined),
    rolldownOptions: {
      output: {
        // Kein Chunk ueber 500 kB: React und Router getrennt vom Brett (react-chessboard, chess.js).
        codeSplitting: {
          groups: [
            { name: 'react', test: /node_modules[\\/](react|react-dom|scheduler)[\\/]/ },
            { name: 'router', test: /node_modules[\\/](react-router|cookie|set-cookie-parser)[\\/]/ },
            { name: 'brett', test: /node_modules[\\/](react-chessboard|chess\.js|@dnd-kit)[\\/]/ },
          ],
        },
      },
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': apiProxy,
    },
  },
  // Gleicher Port wie dev, damit `adb reverse tcp:5173 tcp:5173` auch fuer `vite preview` gilt.
  preview: {
    port: 5173,
    strictPort: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
  },
})
