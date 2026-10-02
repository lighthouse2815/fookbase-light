import { apiRequest } from './client'

export interface AuthenticatedUser {
  id: string
  email: string
  username: string
  emailConfirmed: boolean
  roles: string[]
}

export interface AuthenticationResponse {
  user: AuthenticatedUser
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken?: string
  refreshTokenExpiresAt: string
}

const jsonBody = (value: unknown) => ({ body: JSON.stringify(value) })

export const authApi = {
  login: (identifier: string, password: string) => apiRequest<AuthenticationResponse>('/api/auth/login', {
    method: 'POST',
    ...jsonBody({ identifier, password }),
  }),
  refresh: (refreshToken?: string) => apiRequest<AuthenticationResponse>('/api/auth/refresh', {
    method: 'POST',
    ...jsonBody(refreshToken ? { refreshToken } : {}),
  }),
  logout: (refreshToken?: string) => apiRequest<void>('/api/auth/logout', {
    method: 'POST',
    ...jsonBody(refreshToken ? { refreshToken } : {}),
  }),
  getCurrentUser: () => apiRequest<AuthenticatedUser>('/api/auth/me'),
}
