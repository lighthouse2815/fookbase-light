import { createContext } from 'react'

export type AppLanguage = 'en' | 'vi'
export type AppTheme = 'dark' | 'light'

export interface PreferencesContextValue {
  language: AppLanguage
  theme: AppTheme
  setLanguage: (language: AppLanguage) => void
  setTheme: (theme: AppTheme) => void
  t: (key: string) => string
}

export const PreferencesContext = createContext<PreferencesContextValue | null>(null)
