import { useEffect, useState } from 'react'
import { authApi } from '../api/auth'
import type { ChangePasswordDetails, Credentials, RegistrationDetails } from '../api/auth'
import { authSessionChangedEvent, clearAuthSession, getAuthSession, saveAuthSession } from './session'
import type { AuthSession } from './session'
import { AuthContext } from './context'

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(() => getAuthSession())

  const applySession = (nextSession: AuthSession) => {
    saveAuthSession(nextSession)
    setSession(nextSession)
  }

  const signIn = async (credentials: Credentials) => {
    applySession(await authApi.login(credentials))
  }

  const signUp = async (details: RegistrationDetails) => {
    applySession(await authApi.register(details))
  }

  const changePassword = async (details: ChangePasswordDetails) => {
    applySession(await authApi.changePassword(details))
  }

  const signOut = async () => {
    try {
      if (session) {
        await authApi.logout(session.refreshToken)
      }
    } finally {
      clearAuthSession()
      setSession(null)
    }
  }

  useEffect(() => {
    const synchronizeSession = () => setSession(getAuthSession())
    window.addEventListener(authSessionChangedEvent, synchronizeSession)
    return () => window.removeEventListener(authSessionChangedEvent, synchronizeSession)
  }, [])

  useEffect(() => {
    if (!session) return

    const refreshAt = new Date(session.accessTokenExpiresAt).getTime() - 60_000
    const delay = Math.max(0, refreshAt - Date.now())
    const timeoutId = window.setTimeout(() => {
      void authApi.refresh(session.refreshToken)
        .then(applySession)
        .catch(() => {
          clearAuthSession()
          setSession(null)
        })
    }, delay)

    return () => window.clearTimeout(timeoutId)
  }, [session])

  return (
    <AuthContext.Provider value={{ session, signIn, signUp, changePassword, signOut }}>
      {children}
    </AuthContext.Provider>
  )
}
