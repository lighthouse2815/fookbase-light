import { useEffect, useState } from 'react'
import AdminDashboard from './AdminDashboard'
import { authApi } from './api/auth'
import type { AuthenticationResponse } from './api/auth'
import { ApiError } from './api/client'
import LoginPage from './LoginPage'
import { adminSessionChangedEvent, clearSession, getSession, saveSession } from './session'

export default function App() {
  const [session, setSession] = useState<AuthenticationResponse | null>(() => getSession())

  const applySession = (nextSession: AuthenticationResponse) => {
    saveSession(nextSession)
    setSession(nextSession)
  }

  const signOut = async () => {
    try {
      if (session) await authApi.logout(session.refreshToken)
    } finally {
      clearSession()
      setSession(null)
    }
  }

  const signIn = async (email: string, password: string) => {
    const nextSession = await authApi.login(email, password)
    if (!nextSession.user.roles.includes('Admin')) {
      throw new ApiError('Tài khoản này không có quyền quản trị.', 403)
    }

    applySession(nextSession)
  }

  useEffect(() => {
    const synchronizeSession = () => {
      const nextSession = getSession()
      if (nextSession && !nextSession.user.roles.includes('Admin')) {
        clearSession()
        setSession(null)
        return
      }

      setSession(nextSession)
    }
    window.addEventListener(adminSessionChangedEvent, synchronizeSession)
    return () => window.removeEventListener(adminSessionChangedEvent, synchronizeSession)
  }, [])

  useEffect(() => {
    if (!session) return

    const refreshAt = new Date(session.accessTokenExpiresAt).getTime() - 60_000
    const delay = Math.max(0, refreshAt - Date.now())
    const timeoutId = window.setTimeout(() => {
      void authApi.refresh(session.refreshToken)
        .then((refreshedSession) => {
          if (!refreshedSession.user.roles.includes('Admin')) {
            clearSession()
            setSession(null)
            return
          }

          applySession(refreshedSession)
        })
        .catch(() => {
          clearSession()
          setSession(null)
        })
    }, delay)

    return () => window.clearTimeout(timeoutId)
  }, [session])

  if (!session) return <LoginPage onSignIn={signIn} />
  return <AdminDashboard username={session.user.username} onSignOut={signOut} />
}
