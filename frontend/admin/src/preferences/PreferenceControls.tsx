import { Moon, Sun } from 'lucide-react'
import { usePreferences } from './usePreferences'

export default function PreferenceControls({ className = '' }: { className?: string }) {
  const { language, setLanguage, theme, setTheme, t } = usePreferences()
  const isDark = theme === 'dark'

  return <div className={`flex items-center gap-2 ${className}`}>
    <label className="sr-only" htmlFor="admin-language">{t('language')}</label>
    <select id="admin-language" value={language} onChange={(event) => setLanguage(event.target.value as 'en' | 'vi')} className="h-8 rounded-lg border border-border bg-surface px-2 text-xs font-semibold text-text-muted"><option value="en">EN</option><option value="vi">VI</option></select>
    <button type="button" onClick={() => setTheme(isDark ? 'light' : 'dark')} title={isDark ? t('switchToLight') : t('switchToDark')} aria-label={isDark ? t('switchToLight') : t('switchToDark')} className="icon-button">{isDark ? <Sun size={17} /> : <Moon size={17} />}</button>
  </div>
}
