/** Ersetzt `{schluessel}` im Text, z. B. `fuelle('Elo {elo}', { elo: 812 })`. Unbekannte bleiben stehen. */
export function fuelle(text: string, werte: Record<string, string | number>): string {
  return text.replace(/\{(\w+)\}/g, (ganz, name: string) => (name in werte ? String(werte[name]) : ganz))
}
