import { apiRequest } from './client'

export interface AuthenticatedUser {
  id: string
  email: string
  username: string
}

export interface AuthenticationResponse {
  user: AuthenticatedUser
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
}

export interface Credentials {
  email: string
  password: string
}

export interface RegistrationDetails extends Credentials {
  username: string
}

const jsonBody = (value: unknown) => ({ body: JSON.stringify(value) })

export const authApi = {
  register: (details: RegistrationDetails) =>
    apiRequest<AuthenticationResponse>('/api/auth/register', {
      method: 'POST',
      ...jsonBody(details),
    }),
  login: (credentials: Credentials) =>
    apiRequest<AuthenticationResponse>('/api/auth/login', {
      method: 'POST',
      ...jsonBody(credentials),
    }),
  refresh: (refreshToken: string) =>
    apiRequest<AuthenticationResponse>('/api/auth/refresh', {
      method: 'POST',
      ...jsonBody({ refreshToken }),
    }),
  logout: (refreshToken: string) =>
    apiRequest<void>('/api/auth/logout', {
      method: 'POST',
      ...jsonBody({ refreshToken }),
    }),
  getCurrentUser: () => apiRequest<AuthenticatedUser>('/api/auth/me'),
}
