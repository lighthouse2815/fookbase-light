import { getApp, getApps, initializeApp } from 'firebase/app'
import { getAuth, RecaptchaVerifier, signInWithPhoneNumber, type ConfirmationResult } from 'firebase/auth'

export type PhoneConfirmation = ConfirmationResult

let verifier: RecaptchaVerifier | null = null

function normalizeVietnamesePhoneNumber(phoneNumber: string): string {
  const compact = phoneNumber.trim().replace(/[.\s()-]/g, '')
  if (/^0\d{9}$/.test(compact)) return `+84${compact.slice(1)}`
  if (/^84\d{9}$/.test(compact)) return `+${compact}`
  return compact
}

function getFirebaseAuth() {
  const apiKey = import.meta.env.VITE_FIREBASE_API_KEY
  const authDomain = import.meta.env.VITE_FIREBASE_AUTH_DOMAIN
  const projectId = import.meta.env.VITE_FIREBASE_PROJECT_ID
  const appId = import.meta.env.VITE_FIREBASE_APP_ID
  if (!apiKey || !authDomain || !projectId || !appId) {
    throw new Error('Firebase phone verification is not configured.')
  }

  const app = getApps().length > 0
    ? getApp()
    : initializeApp({ apiKey, authDomain, projectId, appId, messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID })
  return getAuth(app)
}

export async function sendPhoneVerification(phoneNumber: string, container: HTMLElement): Promise<PhoneConfirmation> {
  verifier?.clear()
  verifier = new RecaptchaVerifier(getFirebaseAuth(), container, { size: 'invisible' })
  return signInWithPhoneNumber(getFirebaseAuth(), normalizeVietnamesePhoneNumber(phoneNumber), verifier)
}

export async function confirmPhoneVerification(confirmation: PhoneConfirmation | null, code: string): Promise<string> {
  if (!confirmation || code.length !== 6) throw new Error('Enter the six-digit verification code.')
  return (await confirmation.confirm(code)).user.getIdToken(true)
}

export function clearPhoneVerification() {
  verifier?.clear()
  verifier = null
}
