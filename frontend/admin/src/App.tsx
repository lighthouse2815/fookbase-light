import { useEffect, useRef, useState } from 'react'
import AdminDashboard from './AdminDashboard'
import { authApi } from './api/auth'
import type { AuthenticationResponse } from './api/auth'
import { ApiError, refreshAuthSession } from './api/client'
import LoginPage from './LoginPage'
import { usePreferences } from './preferences'
import { adminSessionChangedEvent, clearSession, getSession, saveSession } from './session'

export default function App() {
  const { t, language } = usePreferences()
  const [session, setSession] = useState<AuthenticationResponse | null>(() => getSession())
  const [refreshFailed, setRefreshFailed] = useState(false)
  const [isRetrying, setIsRetrying] = useState(false)
  const retryRefreshRef = useRef<() => void>(() => undefined)

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

  const signIn = async (identifier: string, password: string) => {
    const nextSession = await authApi.login(identifier, password)
    if (!nextSession.user.roles.includes('Admin')) {
      throw new ApiError(t('adminRequired'), 403)
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
      setRefreshFailed(false)
    }
    window.addEventListener(adminSessionChangedEvent, synchronizeSession)
    return () => window.removeEventListener(adminSessionChangedEvent, synchronizeSession)
  }, [])

  useEffect(() => {
    if (!session) return

    const refreshAt = new Date(session.accessTokenExpiresAt).getTime() - 60_000
    const delay = Math.max(0, refreshAt - Date.now())
    let active = true
    let timeoutId: number
    const refresh = async () => {
      window.clearTimeout(timeoutId)
      setIsRetrying(true)
      try {
        const refreshedSession = await refreshAuthSession()
        if (active && refreshedSession) {
          if (!refreshedSession.user.roles.includes('Admin')) {
            clearSession()
            setSession(null)
            return
          }
          applySession(refreshedSession)
          setRefreshFailed(false)
        }
      } catch {
        if (!active) return
        setRefreshFailed(true)
        timeoutId = window.setTimeout(() => void refresh(), 30_000)
      } finally { setIsRetrying(false) }
    }
    const online = () => { if (Date.now() >= refreshAt) void refresh() }
    retryRefreshRef.current = () => void refresh()
    timeoutId = window.setTimeout(() => void refresh(), delay)
    window.addEventListener('online', online)
    return () => {
      active = false
      window.clearTimeout(timeoutId)
      window.removeEventListener('online', online)
      retryRefreshRef.current = () => undefined
    }
  }, [session])

  if (!session) return <LoginPage onSignIn={signIn} />
  return <><AdminDashboard username={session.user.username} onSignOut={signOut} />
    {refreshFailed && <div role="status" className="fixed bottom-4 left-4 z-50 flex max-w-[calc(100vw-2rem)] items-center gap-3 rounded-xl border border-border bg-surface p-4 text-sm text-text shadow-xl">
      <span>{language === 'vi' ? 'Chưa thể làm mới phiên đăng nhập. Đang thử kết nối lại.' : 'Unable to refresh your session. Reconnecting.'}</span>
      <button type="button" disabled={isRetrying} onClick={() => retryRefreshRef.current()} className="shrink-0 rounded-lg px-3 py-2 font-semibold text-primary focus-visible:outline-2 focus-visible:outline-primary disabled:opacity-60">{t('retry')}</button>
    </div>}
  </>
}
