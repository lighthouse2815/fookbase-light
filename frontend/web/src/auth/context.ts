import { createContext } from 'react'
import type { ChangePasswordDetails, Credentials, RegistrationDetails } from '../api/auth'
import type { AuthSession } from './session'

export interface AuthContextValue {
  session: AuthSession | null
  signIn: (credentials: Credentials) => Promise<void>
  signUp: (details: RegistrationDetails) => Promise<void>
  changePassword: (details: ChangePasswordDetails) => Promise<void>
  signOut: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
