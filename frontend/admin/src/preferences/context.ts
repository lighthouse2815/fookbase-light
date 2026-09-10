import { createContext } from 'react'

export type AdminLanguage = 'en' | 'vi'
export type AdminTheme = 'dark' | 'light'

export interface PreferencesContextValue {
  language: AdminLanguage
  theme: AdminTheme
  setLanguage: (language: AdminLanguage) => void
  setTheme: (theme: AdminTheme) => void
  t: (key: string) => string
}

export const PreferencesContext = createContext<PreferencesContextValue | null>(null)
