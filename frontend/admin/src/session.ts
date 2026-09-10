import type { AuthenticationResponse } from './api/auth'
import { clearAccessToken, saveAccessToken } from './api/client'

const sessionStorageKey = 'fookbase.admin.session'
export const adminSessionChangedEvent = 'fookbase.admin-session-changed'

export function getSession(): AuthenticationResponse | null {
  const serialized = localStorage.getItem(sessionStorageKey)
  if (!serialized) return null

  try {
    return JSON.parse(serialized) as AuthenticationResponse
  } catch {
    clearSession()
    return null
  }
}

export function saveSession(session: AuthenticationResponse) {
  saveAccessToken(session.accessToken)
  localStorage.setItem(sessionStorageKey, JSON.stringify(session))
  window.dispatchEvent(new Event(adminSessionChangedEvent))
}

export function clearSession() {
  clearAccessToken()
  localStorage.removeItem(sessionStorageKey)
  window.dispatchEvent(new Event(adminSessionChangedEvent))
}
