import { useEffect, useState } from 'react'

/** `true`, solange der Browser eine Verbindung meldet (`navigator.onLine` plus Ereignisse). */
export function useOnline(): boolean {
  const [online, setOnline] = useState(() => typeof navigator === 'undefined' || navigator.onLine !== false)
  useEffect(() => {
    const an = () => setOnline(true)
    const aus = () => setOnline(false)
    window.addEventListener('online', an)
    window.addEventListener('offline', aus)
    return () => {
      window.removeEventListener('online', an)
      window.removeEventListener('offline', aus)
    }
  }, [])
  return online
}
