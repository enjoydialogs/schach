/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Commit-SHA des Baus, gesetzt vom Web-Image (Dockerfile, S0c). Fehlt lokal. */
  readonly VITE_COMMIT_SHA?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
