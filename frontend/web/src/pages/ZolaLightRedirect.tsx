import { useEffect } from 'react'

export default function ZolaLightRedirect() {
  useEffect(() => {
    window.location.replace(import.meta.env.VITE_ZOLA_LIGHT_URL ?? 'http://localhost:5175')
  }, [])

  return <main className="min-h-screen grid place-items-center text-text-muted">Đang mở Zola Light…</main>
}
