import { createContext } from 'react'
import type { ChangePasswordDetails, Credentials, LoginResponse, RegistrationDetails } from '../api/auth'
import type { AuthSession } from './session'

export interface AuthContextValue {
  session: AuthSession | null
  signIn: (credentials: Credentials) => Promise<LoginResponse>
  completeGoogleSignIn: (code: string) => Promise<LoginResponse>
  linkGoogleSignIn: (code: string, password: string) => Promise<LoginResponse>
  completeTwoFactor: (challenge: string, code: string) => Promise<void>
  signUp: (details: RegistrationDetails) => Promise<void>
  changePassword: (details: ChangePasswordDetails) => Promise<void>
  signOut: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
