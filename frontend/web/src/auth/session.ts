import type { AuthenticationResponse } from '../api/auth'

const accessTokenKey = 'fookbase.accessToken'
const sessionKey = 'fookbase.session'
export const authSessionChangedEvent = 'fookbase.auth-session-changed'

export type AuthSession = AuthenticationResponse

export function getAuthSession(): AuthSession | null {
  const serializedSession = localStorage.getItem(sessionKey)
  if (!serializedSession) return null

  try {
    return JSON.parse(serializedSession) as AuthSession
  } catch {
    clearAuthSession()
    return null
  }
}

export function saveAuthSession(session: AuthSession) {
  localStorage.setItem(accessTokenKey, session.accessToken)
  localStorage.setItem(sessionKey, JSON.stringify(session))
  window.dispatchEvent(new Event(authSessionChangedEvent))
}

export function clearAuthSession() {
  localStorage.removeItem(accessTokenKey)
  localStorage.removeItem(sessionKey)
  window.dispatchEvent(new Event(authSessionChangedEvent))
}
