/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Schach.Api, Profil "http" (Properties/launchSettings.json).
const apiProxy = process.env.SCHACH_API_PROXY ?? 'http://localhost:5252'

export default defineConfig({
  plugins: [react()],
  build: {
    // Schriften nie als data:-URI einbetten: die CSP setzt font-src 'self' (SecurityHeaders.cs).
    // Alles andere behaelt Vites Standardschwelle.
    assetsInlineLimit: (filePath) => (/\.(woff2?|ttf|otf|eot)$/i.test(filePath) ? false : undefined),
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
