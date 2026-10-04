import type { AuthenticationResponse } from './auth'
import { clearSession, getSession, saveSession } from '../session'

const accessTokenStorageKey = 'fookbase.admin.accessToken'
let refreshPromise: Promise<AuthenticationResponse | null> | null = null

export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface ApiErrorBody {
  error?: { message?: string }
  detail?: string
  title?: string
}

export function getAccessToken() {
  return localStorage.getItem(accessTokenStorageKey)
}

export function saveAccessToken(accessToken: string) {
  localStorage.setItem(accessTokenStorageKey, accessToken)
}

export function clearAccessToken() {
  localStorage.removeItem(accessTokenStorageKey)
}

function createHeaders(path: string, init: RequestInit, accessToken: string | null) {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json')
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
  if (path.startsWith('/api/auth/')) headers.set('X-Fookbase-Auth-Transport', 'cookie:admin')
  return headers
}

async function requestFreshSession() {
  const session = getSession()
  if (!session) return null

  const response = await fetch(`${apiBaseUrl}/api/auth/refresh`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      'X-Fookbase-Auth-Transport': 'cookie:admin',
    },
    body: JSON.stringify(session.refreshToken ? { refreshToken: session.refreshToken } : {}),
  })
  const current = getSession()
  // Ignore a response for a session that has since signed out or changed.
  if (!current || current.user.id !== session.user.id || current.accessToken !== session.accessToken) {
    return current?.user.id === session.user.id ? current : null
  }
  if (response.status === 401 || response.status === 403) {
    clearSession()
    return null
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as ApiErrorBody | null
    throw new ApiError(problem?.error?.message ?? problem?.detail ?? problem?.title ?? 'Không thể làm mới phiên đăng nhập. Vui lòng thử lại.', response.status)
  }

  const { data: nextSession } = await response.json() as { data: AuthenticationResponse }
  // Reading the response body may take long enough for the user to sign out.
  if (getSession()?.accessToken !== session.accessToken) return null
  saveSession(nextSession)
  return nextSession
}

export function refreshAuthSession() {
  if (!refreshPromise) {
    refreshPromise = requestFreshSession().finally(() => { refreshPromise = null })
  }
  return refreshPromise
}

function canRetryWithRefresh(path: string, accessToken: string | null) {
  return accessToken !== null &&
    path !== '/api/auth/login' &&
    path !== '/api/auth/refresh'
}

export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const accessToken = getAccessToken()

  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      credentials: 'include',
      headers: createHeaders(path, init, accessToken),
    })
    if (response.status === 401 && canRetryWithRefresh(path, accessToken)) {
      const refreshedAccessToken = (await refreshAuthSession())?.accessToken
      if (refreshedAccessToken) {
        response = await fetch(`${apiBaseUrl}${path}`, {
          ...init,
          credentials: 'include',
          headers: createHeaders(path, init, refreshedAccessToken),
        })
      }
    }
  } catch (error) {
    if (init.signal?.aborted) throw error
    if (error instanceof ApiError) throw error
    throw new ApiError('Không thể kết nối đến máy chủ.', 0)
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null) as ApiErrorBody | null
    throw new ApiError(problem?.error?.message ?? problem?.detail ?? problem?.title ?? 'Yêu cầu không thành công.', response.status)
  }

  if (response.status === 204) return undefined as T
  const body = await response.json()
  return path.startsWith('/api/auth/') ? body.data as T : body as T
}
