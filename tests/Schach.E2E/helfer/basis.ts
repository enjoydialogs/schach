// localhost statt 127.0.0.1: das Sitzungs-Cookie ist `Secure`, Chromium nimmt es ueber http nur
// auf localhost an.
export const BASIS_URL = process.env.E2E_BASE_URL ?? 'http://localhost:8080'
