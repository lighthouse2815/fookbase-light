import { apiRequest } from './client'

export interface AuthenticatedUser {
  id: string
  email: string | null
  phoneNumber: string | null
  username: string
  emailConfirmed: boolean
  phoneNumberConfirmed: boolean
  roles: string[]
}

export interface AuthenticationResponse {
  user: AuthenticatedUser
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken?: string
  refreshTokenExpiresAt: string
}

export interface TwoFactorChallengeResponse { twoFactorRequired: true; challenge: string; expiresAtUtc: string }
export type LoginResponse = AuthenticationResponse | TwoFactorChallengeResponse
export interface ExternalProviders { google: boolean }
export interface GoogleCompletionRequest { code: string; client: 'web' }
export interface GoogleLinkRequest extends GoogleCompletionRequest { password: string }
export interface AuthSessionInfo { sessionId: string; device: string | null; createdAtUtc: string; lastSeenAtUtc: string; expiresAtUtc: string; isCurrent: boolean }
export interface SecurityState { twoFactorEnabled: boolean; recoveryCodesRemaining: number; activeSessionCount: number }
export interface TwoFactorSetup { sharedKey: string; otpauthUri: string }
export interface RecoveryCodes { recoveryCodes: string[] }

export interface Credentials {
  identifier: string
  password: string
}

export type RegistrationGender = 'female' | 'male' | 'other' | 'preferNotToSay'

export interface RegistrationDetails {
  firstName: string
  lastName: string
  dateOfBirth: string
  gender: RegistrationGender
  contact: string
  password: string
}

export interface RegistrationChallenge {
  challengeId: string
  expiresAtUtc: string
  resendAvailableAtUtc: string
}

export interface ChangePasswordDetails {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

export interface EmailResetPasswordDetails {
  email: string
  token: string
  password: string
  confirmPassword: string
}

export interface PhoneResetPasswordDetails {
  identifier: string
  code: string
  password: string
  confirmPassword: string
}

export type ResetPasswordDetails = EmailResetPasswordDetails | PhoneResetPasswordDetails

const jsonBody = (value: unknown) => ({ body: JSON.stringify(value) })

export const authApi = {
  providers: () => apiRequest<ExternalProviders>('/api/auth/providers'),
  completeGoogle: (code: string) =>
    apiRequest<LoginResponse>('/api/auth/google/exchange', {
      method: 'POST',
      ...jsonBody({ code, client: 'web' } satisfies GoogleCompletionRequest),
    }),
  linkGoogle: (code: string, password: string) =>
    apiRequest<LoginResponse>('/api/auth/google/link', {
      method: 'POST',
      ...jsonBody({ code, password, client: 'web' } satisfies GoogleLinkRequest),
    }),
  startRegistration: (details: RegistrationDetails) =>
    apiRequest<RegistrationChallenge>('/api/auth/registration/start', {
      method: 'POST',
      ...jsonBody(details),
    }),
  resendRegistration: (challengeId: string) =>
    apiRequest<RegistrationChallenge>('/api/auth/registration/resend', {
      method: 'POST',
      ...jsonBody({ challengeId }),
    }),
  verifyRegistration: (challengeId: string, code: string) =>
    apiRequest<AuthenticationResponse>('/api/auth/registration/verify', {
      method: 'POST',
      ...jsonBody({ challengeId, code }),
    }),
  login: (credentials: Credentials) =>
    apiRequest<LoginResponse>('/api/auth/login', {
      method: 'POST',
      ...jsonBody(credentials),
    }),
  refresh: (refreshToken?: string) =>
    apiRequest<AuthenticationResponse>('/api/auth/refresh', {
      method: 'POST',
      ...jsonBody(refreshToken ? { refreshToken } : {}),
    }),
  verifyTwoFactor: (challenge: string, code: string) => apiRequest<AuthenticationResponse>('/api/auth/2fa/verify', { method: 'POST', ...jsonBody({ challenge, code }) }),
  logout: (refreshToken?: string) =>
    apiRequest<void>('/api/auth/logout', {
      method: 'POST',
      ...jsonBody(refreshToken ? { refreshToken } : {}),
    }),
  getCurrentUser: () => apiRequest<AuthenticatedUser>('/api/auth/me'),
  requestPasswordReset: (identifier: string) =>
    apiRequest<void>('/api/auth/password/forgot', {
      method: 'POST',
      ...jsonBody({ identifier }),
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
