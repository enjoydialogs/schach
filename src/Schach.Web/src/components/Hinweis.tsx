import { useHinweisText } from './hinweisSpeicher'
import './bausteine.css'

/** `Hinweis` (Toast, SCHACH-SPEC 10.14): eine kurze Meldung unten, z. B. nach Abmelden. */
export function Hinweis() {
  const text = useHinweisText()
  if (text === null) return null
  return (
    <div className="hinweis" role="status">
      {text}
    </div>
  )
}
