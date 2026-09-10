import type { AuthenticationResponse } from './auth'
import { clearAuthSession, getAuthSession, saveAuthSession } from '../auth/session'

export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''
const accessTokenStorageKey = 'fookbase.accessToken'
let refreshPromise: Promise<string | null> | null = null

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

interface ProblemDetails {
  detail?: string
  title?: string
}

function getAccessToken() {
  return localStorage.getItem(accessTokenStorageKey)
}

function createHeaders(init: RequestInit, accessToken: string | null) {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (init.body && !(init.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }
  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`)
  }

  return headers
}

async function refreshAccessToken() {
  const session = getAuthSession()
  if (!session) return null

  try {
    const response = await fetch(`${apiBaseUrl}/api/auth/refresh`, {
      method: 'POST',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: session.refreshToken }),
    })
    if (!response.ok) throw new Error('Refresh token is invalid.')

    const nextSession = await response.json() as AuthenticationResponse
    saveAuthSession(nextSession)
    return nextSession.accessToken
  } catch {
    clearAuthSession()
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

export async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
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
    const problem = await response.json().catch(() => null) as ProblemDetails | null
    throw new ApiError(
      problem?.detail ?? problem?.title ?? 'Yêu cầu không thành công.',
      response.status,
    )
  }

  if (response.status === 204) {
    return undefined as T
  }

  return response.json() as Promise<T>
}
