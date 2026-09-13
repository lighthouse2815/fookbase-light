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
  refreshToken: string
  refreshTokenExpiresAt: string
}

export interface TwoFactorChallengeResponse { twoFactorRequired: true; challenge: string; expiresAtUtc: string }
export type LoginResponse = AuthenticationResponse | TwoFactorChallengeResponse
export interface AuthSessionInfo { sessionId: string; device: string | null; createdAtUtc: string; lastSeenAtUtc: string; expiresAtUtc: string; isCurrent: boolean }
export interface SecurityState { twoFactorEnabled: boolean; recoveryCodesRemaining: number; activeSessionCount: number }
export interface TwoFactorSetup { sharedKey: string; otpauthUri: string }
export interface RecoveryCodes { recoveryCodes: string[] }

export interface Credentials {
  email: string
  password: string
}

export interface RegistrationDetails extends Credentials {
  username: string
}

export interface ChangePasswordDetails {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

export interface ResetPasswordDetails {
  email: string
  token: string
  password: string
  confirmPassword: string
}

const jsonBody = (value: unknown) => ({ body: JSON.stringify(value) })

export const authApi = {
  register: (details: RegistrationDetails) =>
    apiRequest<AuthenticationResponse>('/api/auth/register', {
      method: 'POST',
      ...jsonBody(details),
    }),
  login: (credentials: Credentials) =>
    apiRequest<LoginResponse>('/api/auth/login', {
      method: 'POST',
      ...jsonBody(credentials),
    }),
  refresh: (refreshToken: string) =>
    apiRequest<AuthenticationResponse>('/api/auth/refresh', {
      method: 'POST',
      ...jsonBody({ refreshToken }),
    }),
  verifyTwoFactor: (challenge: string, code: string) => apiRequest<AuthenticationResponse>('/api/auth/2fa/verify', { method: 'POST', ...jsonBody({ challenge, code }) }),
  logout: (refreshToken: string) =>
    apiRequest<void>('/api/auth/logout', {
      method: 'POST',
      ...jsonBody({ refreshToken }),
    }),
  getCurrentUser: () => apiRequest<AuthenticatedUser>('/api/auth/me'),
  requestPasswordReset: (email: string) =>
    apiRequest<void>('/api/auth/password/forgot', {
      method: 'POST',
      ...jsonBody({ email }),
    }),
  resetPassword: (details: ResetPasswordDetails) =>
    apiRequest<void>('/api/auth/password/reset', {
      method: 'POST',
      ...jsonBody(details),
    }),
  changePassword: (details: ChangePasswordDetails) =>
    apiRequest<AuthenticationResponse>('/api/auth/password/change', {
      method: 'POST',
      ...jsonBody(details),
    }),
  verifyEmail: (email: string, token: string) =>
    apiRequest<void>('/api/auth/email/verify', {
      method: 'POST',
      ...jsonBody({ email, token }),
    }),
  resendEmailVerification: () =>
    apiRequest<void>('/api/auth/email/verification', { method: 'POST' }),
  sessions: () => apiRequest<AuthSessionInfo[]>('/api/auth/sessions'),
  revokeSession: (sessionId: string) => apiRequest<void>(`/api/auth/sessions/${sessionId}`, { method: 'DELETE' }),
  revokeOtherSessions: () => apiRequest<void>('/api/auth/sessions/revoke-others', { method: 'POST' }),
  security: () => apiRequest<SecurityState>('/api/auth/security'),
  setupTwoFactor: () => apiRequest<TwoFactorSetup>('/api/auth/2fa/setup', { method: 'POST' }),
  enableTwoFactor: (code: string) => apiRequest<RecoveryCodes>('/api/auth/2fa/enable', { method: 'POST', ...jsonBody({ code }) }),
  regenerateRecoveryCodes: () => apiRequest<RecoveryCodes>('/api/auth/2fa/recovery-codes/regenerate', { method: 'POST' }),
  disableTwoFactor: (currentPassword: string) => apiRequest<void>('/api/auth/2fa/disable', { method: 'POST', ...jsonBody({ currentPassword }) }),
}
