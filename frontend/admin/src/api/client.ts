import type { AuthenticationResponse } from './auth'
import { clearSession, getSession, saveSession } from '../session'

const accessTokenStorageKey = 'fookbase.admin.accessToken'
let refreshPromise: Promise<string | null> | null = null

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

function createHeaders(init: RequestInit, accessToken: string | null) {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json')
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
  return headers
}

async function refreshAccessToken() {
  const session = getSession()
  if (!session) return null

  try {
    const response = await fetch(`${apiBaseUrl}/api/auth/refresh`, {
      method: 'POST',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: session.refreshToken }),
    })
    if (!response.ok) throw new Error('Refresh token is invalid.')

    const { data: nextSession } = await response.json() as { data: AuthenticationResponse }
    saveSession(nextSession)
    return nextSession.accessToken
  } catch {
    clearSession()
    return null
  }
}

function getRefreshedAccessToken() {
  if (!refreshPromise) {
    refreshPromise = refreshAccessToken().finally(() => { refreshPromise = null })
  }

  return refreshPromise
}

function canRetryWithRefresh(path: string, accessToken: string | null) {
  return accessToken !== null &&
    path !== '/api/auth/login' &&
    path !== '/api/auth/register' &&
    path !== '/api/auth/refresh'
}

export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const accessToken = getAccessToken()

  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      headers: createHeaders(init, accessToken),
    })
    if (response.status === 401 && canRetryWithRefresh(path, accessToken)) {
      const refreshedAccessToken = await getRefreshedAccessToken()
      if (refreshedAccessToken) {
        response = await fetch(`${apiBaseUrl}${path}`, {
          ...init,
          headers: createHeaders(init, refreshedAccessToken),
        })
      }
    }
  } catch {
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
