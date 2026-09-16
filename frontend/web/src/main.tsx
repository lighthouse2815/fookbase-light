import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

const preloadErrorReloadKey = 'fookbase:preload-error-reloaded'

window.addEventListener('vite:preloadError', (event) => {
  event.preventDefault()

  // A deployment can replace Vite's hashed chunks while a browser is still
  // running the previous entry bundle. Reload once to fetch the new manifest.
  if (sessionStorage.getItem(preloadErrorReloadKey)) return

  sessionStorage.setItem(preloadErrorReloadKey, 'true')
  window.location.reload()
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
