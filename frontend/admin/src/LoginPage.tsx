import { useState, type FormEvent } from 'react'
import { ApiError } from './api/client'
import { PreferenceControls, usePreferences } from './preferences'

export default function LoginPage({ onSignIn }: { onSignIn: (identifier: string, password: string) => Promise<void> }) {
  const { t } = usePreferences()
  const [identifier, setIdentifier] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await onSignIn(identifier, password)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableSignIn'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="relative flex min-h-screen items-center justify-center bg-bg p-5">
      <div className="absolute right-5 top-5 z-10"><PreferenceControls /></div>
      <div className="grid w-full max-w-4xl overflow-hidden rounded-3xl border border-border bg-surface shadow-2xl md:grid-cols-[1fr_0.92fr]">
        <section className="hidden bg-gradient-to-br from-[#0b493d] via-[#116c58] to-[#17947a] p-10 md:flex md:flex-col md:justify-between">
          <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-white/15 text-xl font-black text-white">f</div>
          <div><p className="text-sm font-bold tracking-[0.2em] text-white/70">FOOKBASE</p><h1 className="mt-3 font-heading text-4xl font-extrabold leading-tight text-white">{t('adminCenter')}</h1><p className="mt-4 max-w-sm text-sm leading-6 text-white/75">{t('protectedWorkspace')}</p></div>
          <p className="text-xs text-white/60">{t('restricted')}</p>
        </section>
        <section className="p-7 sm:p-10">
          <p className="text-xs font-bold tracking-[0.15em] text-primary">{t('adminSignIn')}</p>
          <h2 className="mt-2 font-heading text-3xl font-bold text-text">{t('welcomeBack')}</h2>
          <p className="mt-2 text-sm text-text-muted">{t('adminRoleHint')}</p>
          <form className="mt-7 space-y-5" onSubmit={submit}>
            <label className="block text-sm font-semibold text-text">{t('email')}<input required autoComplete="email" type="email" value={identifier} onChange={(event) => setIdentifier(event.target.value)} className="mt-2 w-full rounded-xl border border-border bg-surface-2 px-4 py-3 text-text outline-none focus:border-primary" placeholder="admin@example.com" /></label>
            <label className="block text-sm font-semibold text-text">{t('password')}<input required autoComplete="current-password" type="password" value={password} onChange={(event) => setPassword(event.target.value)} className="mt-2 w-full rounded-xl border border-border bg-surface-2 px-4 py-3 text-text outline-none focus:border-primary" placeholder={t('yourPassword')} /></label>
            {error && <p role="alert" className="rounded-xl border border-danger/40 bg-danger/10 px-4 py-3 text-sm text-danger">{error}</p>}
            <button disabled={isSubmitting} className="w-full rounded-xl bg-primary-dark px-4 py-3 font-bold text-white transition hover:bg-primary-dark disabled:opacity-60">{isSubmitting ? t('signingIn') : t('signIn')}</button>
          </form>
        </section>
      </div>
    </main>
  )
}
