import { useLocation, useNavigate } from 'react-router'

/** Zurueck = `navigate(-1)`, ohne Verlauf nach `/` (SCHACH-SPEC 10.1). */
export function useZurueck(): () => void {
  const navigate = useNavigate()
  const location = useLocation()
  // `default` ist der Schluessel des ersten Eintrags: es gibt keinen Verlauf in der App.
  return () => void (location.key === 'default' ? navigate('/') : navigate(-1))
}
