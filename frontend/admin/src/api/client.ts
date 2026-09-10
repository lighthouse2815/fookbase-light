const accessTokenStorageKey = 'fookbase.admin.accessToken'

export const apiBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''

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

export function getAccessToken() {
  return localStorage.getItem(accessTokenStorageKey)
}

export function saveAccessToken(accessToken: string) {
  localStorage.setItem(accessTokenStorageKey, accessToken)
}

export function clearAccessToken() {
  localStorage.removeItem(accessTokenStorageKey)
}

export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json')

  const accessToken = getAccessToken()
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)

  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers })
  } catch {
    throw new ApiError('Không thể kết nối đến máy chủ.', 0)
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null) as ProblemDetails | null
    throw new ApiError(problem?.detail ?? problem?.title ?? 'Yêu cầu không thành công.', response.status)
  }

  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}
