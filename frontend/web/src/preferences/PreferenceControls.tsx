import { usePreferences } from './usePreferences'

export default function PreferenceControls({ className = '' }: { className?: string }) {
  const { language, setLanguage, theme, setTheme, t } = usePreferences()
  const isDark = theme === 'dark'

  return (
    <div className={`flex items-center gap-1.5 ${className}`}>
      <label className="sr-only" htmlFor="fookbase-language">{t('language')}</label>
      <select id="fookbase-language" value={language} onChange={(event) => setLanguage(event.target.value as 'en' | 'vi')} className="h-9 rounded-full border border-border bg-surface-2 px-2 text-xs font-bold text-text outline-none transition hover:bg-surface-hover focus:border-primary">
        <option value="en">EN</option>
        <option value="vi">VI</option>
      </select>
      <button type="button" onClick={() => setTheme(isDark ? 'light' : 'dark')} title={isDark ? t('switchToLight') : t('switchToDark')} aria-label={isDark ? t('switchToLight') : t('switchToDark')} className="flex h-9 w-9 items-center justify-center rounded-full border border-border bg-surface-2 text-base text-text transition hover:bg-surface-hover">
        {isDark ? '☀️' : '🌙'}
      </button>
    </div>
  )
}
