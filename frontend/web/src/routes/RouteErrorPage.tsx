import { useEffect, useRef } from 'react'
import { useAuth } from '../auth/useAuth'
import { usePreferences } from '../preferences'

export default function RouteErrorPage() {
  const { session } = useAuth()
  const { t } = usePreferences()
  const heading = useRef<HTMLHeadingElement>(null)

  useEffect(() => { heading.current?.focus() }, [])

  return (
    <section role="alert" aria-labelledby="route-error-title" className="grid min-h-[60dvh] place-items-center bg-bg px-6 py-16 text-text">
      <div className="max-w-md space-y-5 text-center">
        <h1 ref={heading} id="route-error-title" tabIndex={-1} className="text-2xl font-bold outline-none">{t('routeErrorTitle')}</h1>
        <p className="text-text-muted">{t('routeErrorDescription')}</p>
        <div className="flex flex-wrap justify-center gap-3">
          <button type="button" onClick={() => window.location.reload()} className="rounded-xl bg-primary px-5 py-3 font-semibold text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary">{t('retry')}</button>
          <a href={session ? '/feed' : '/login'} className="rounded-xl border border-border bg-surface px-5 py-3 font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary">{t(session ? 'backToFeed' : 'backToSignIn')}</a>
        </div>
      </div>
    </section>
  )
}
