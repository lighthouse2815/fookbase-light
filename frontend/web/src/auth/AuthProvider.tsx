import { useEffect, useRef, useState } from 'react'
import { authApi } from '../api/auth'
import type { ChangePasswordDetails, Credentials, RegistrationChallenge, RegistrationDetails } from '../api/auth'
import { authSessionChangedEvent, clearAuthSession, getAuthSession, saveAuthSession } from './session'
import type { AuthSession } from './session'
import { AuthContext } from './context'
import { refreshAuthSession } from '../api/client'
import { usePreferences } from '../preferences'

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(() => getAuthSession())
  const [isOnline, setIsOnline] = useState(() => navigator.onLine !== false)
  const [refreshFailed, setRefreshFailed] = useState(false)
  const [isRetrying, setIsRetrying] = useState(false)
  const retryRefreshRef = useRef<() => void>(() => undefined)
  const { t } = usePreferences()

  const applySession = (nextSession: AuthSession) => {
    saveAuthSession(nextSession)
    setSession(nextSession)
  }

  const signIn = async (credentials: Credentials) => {
    const response = await authApi.login(credentials)
    if (!('twoFactorRequired' in response)) applySession(response)
    return response
  }

  const completeGoogleSignIn = async (code: string) => {
    const response = await authApi.completeGoogle(code)
    if (!('twoFactorRequired' in response)) applySession(response)
    return response
  }

  const linkGoogleSignIn = async (code: string, password: string) => {
    const response = await authApi.linkGoogle(code, password)
    if (!('twoFactorRequired' in response)) applySession(response)
    return response
  }

  const completeTwoFactor = async (challenge: string, code: string) => applySession(await authApi.verifyTwoFactor(challenge, code))

  const signUp = (details: RegistrationDetails): Promise<RegistrationChallenge> => authApi.startRegistration(details)

  const completeRegistration = async (challengeId: string, code: string) => {
    applySession(await authApi.verifyRegistration(challengeId, code))
  }

  const resendRegistration = (challengeId: string): Promise<RegistrationChallenge> =>
    authApi.resendRegistration(challengeId)

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
    const synchronizeSession = () => {
      setSession(getAuthSession())
      setRefreshFailed(false)
    }
    window.addEventListener(authSessionChangedEvent, synchronizeSession)
    return () => window.removeEventListener(authSessionChangedEvent, synchronizeSession)
  }, [])

  useEffect(() => {
    if (!session) return

    const refreshAt = new Date(session.accessTokenExpiresAt).getTime() - 60_000
    const delay = Math.max(0, refreshAt - Date.now())
    let active = true
    let failed = false
    let timeoutId: number
    const refresh = async () => {
      window.clearTimeout(timeoutId)
      setIsRetrying(true)
      try {
        await refreshAuthSession()
        if (active) setRefreshFailed(false)
      } catch {
        if (!active) return
        failed = true
        setRefreshFailed(true)
        timeoutId = window.setTimeout(() => void refresh(), 30_000)
      } finally {
        setIsRetrying(false)
      }
    }
    const online = () => {
      setIsOnline(true)
      if (failed || Date.now() >= refreshAt) void refresh()
    }
    const offline = () => setIsOnline(false)
    retryRefreshRef.current = () => void refresh()
    timeoutId = window.setTimeout(() => void refresh(), delay)
    window.addEventListener('online', online)
    window.addEventListener('offline', offline)
    return () => {
      active = false
      window.clearTimeout(timeoutId)
      window.removeEventListener('online', online)
      window.removeEventListener('offline', offline)
      retryRefreshRef.current = () => undefined
    }
  }, [session])

  return (
    <AuthContext.Provider value={{ session, signIn, completeGoogleSignIn, linkGoogleSignIn, completeTwoFactor, signUp, completeRegistration, resendRegistration, changePassword, signOut }}>
      {children}
      {session && (!isOnline || refreshFailed) && <div role="status" className="fixed bottom-4 left-4 z-[90] flex max-w-[calc(100vw-2rem)] items-center gap-3 rounded-xl border border-border bg-surface px-4 py-3 text-sm text-text shadow-xl">
        <span>{t(!isOnline ? 'offlineSessionNotice' : 'sessionRefreshFailed')}</span>
        {isOnline && <button type="button" disabled={isRetrying} onClick={() => retryRefreshRef.current()} className="shrink-0 rounded-lg px-3 py-2 font-semibold text-primary focus-visible:outline-2 focus-visible:outline-primary disabled:opacity-60">{t(isRetrying ? 'pleaseWait' : 'retry')}</button>}
      </div>}
    </AuthContext.Provider>
  )
}
