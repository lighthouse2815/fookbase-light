import { useEffect, useState } from 'react'
import AdminDashboard from './AdminDashboard'
import { authApi } from './api/auth'
import type { AuthenticationResponse } from './api/auth'
import { ApiError } from './api/client'
import LoginPage from './LoginPage'
import { clearSession, getSession, saveSession } from './session'

export default function App() {
  const [session, setSession] = useState<AuthenticationResponse | null>(() => getSession())
  const [isCheckingSession, setIsCheckingSession] = useState(Boolean(session))

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
    if (!session) return

    let active = true
    void authApi.getCurrentUser()
      .then((user) => {
        if (!user.roles.includes('Admin')) throw new ApiError('Tài khoản này không còn quyền quản trị.', 403)
        if (active && JSON.stringify(user) !== JSON.stringify(session.user)) {
          applySession({ ...session, user })
        }
      })
      .catch(async (requestError: unknown) => {
        if (!(requestError instanceof ApiError) || requestError.status !== 401) {
          if (active) {
            clearSession()
            setSession(null)
          }
          return
        }

        try {
          const refreshed = await authApi.refresh(session.refreshToken)
          if (!refreshed.user.roles.includes('Admin')) throw new ApiError('Tài khoản này không còn quyền quản trị.', 403)
          if (active) applySession(refreshed)
        } catch {
          if (active) {
            clearSession()
            setSession(null)
          }
        }
      })
      .finally(() => {
        if (active) setIsCheckingSession(false)
      })

    return () => { active = false }
  }, [session])

  if (isCheckingSession) return <main className="flex min-h-screen items-center justify-center text-sm text-text-muted">Đang xác thực phiên quản trị...</main>
  if (!session) return <LoginPage onSignIn={signIn} />
  return <AdminDashboard username={session.user.username} onSignOut={signOut} />
}
