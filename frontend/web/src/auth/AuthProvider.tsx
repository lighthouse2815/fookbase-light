import { useEffect, useState } from 'react'
import { authApi } from '../api/auth'
import type { ChangePasswordDetails, Credentials, RegistrationDetails } from '../api/auth'
import { ApiError } from '../api/client'
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
    if (!session) return

    let isActive = true
    const timeoutId = window.setTimeout(() => {
      void authApi.getCurrentUser()
        .then((user) => {
          if (isActive && JSON.stringify(user) !== JSON.stringify(session.user)) {
            applySession({ ...session, user })
          }
        })
        .catch(async (error: unknown) => {
          if (!(error instanceof ApiError) || error.status !== 401) return

          try {
            const refreshedSession = await authApi.refresh(session.refreshToken)
            if (isActive) applySession(refreshedSession)
          } catch {
            if (isActive) {
              clearAuthSession()
              setSession(null)
            }
          }
        })
    }, 0)

    return () => {
      isActive = false
      window.clearTimeout(timeoutId)
    }
  }, [session])

  return (
    <AuthContext.Provider value={{ session, signIn, signUp, changePassword, signOut }}>
      {children}
    </AuthContext.Provider>
  )
}
