/** Oeffentliches Repo; der Quellcode-Link zeigt auf den gebauten Commit (SCHACH-SPEC 13.6). */
export const REPO_URL = 'https://github.com/enjoydialogs/schach'

/** 7-stelliger Commit-SHA aus `VITE_COMMIT_SHA`, sonst `null` (lokaler Bau). */
export function kurzSha(sha: string | undefined): string | null {
  const wert = sha?.trim()
  return wert !== undefined && /^[0-9a-f]{7,40}$/i.test(wert) ? wert.slice(0, 7).toLowerCase() : null
}

/** Ziel des Quellcode-Links: der Commit, ohne SHA das Repo. */
export function quellcodeUrl(sha: string | null): string {
  return sha !== null ? `${REPO_URL}/tree/${sha}` : REPO_URL
}
