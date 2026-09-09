import { useState } from 'react'
import { authApi } from '../api/auth'
import type { Credentials, RegistrationDetails } from '../api/auth'
import { clearAuthSession, getAuthSession, saveAuthSession } from './session'
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

  return (
    <AuthContext.Provider value={{ session, signIn, signUp, signOut }}>
      {children}
    </AuthContext.Provider>
  )
}
