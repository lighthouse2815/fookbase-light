import { useEffect, useState, type FormEvent } from 'react'
import { Navigate, useSearchParams } from 'react-router-dom'
import { authApi } from '../../api/auth'
import { ApiError } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { PreferenceControls, usePreferences } from '../../preferences'

export default function LoginPage() {
  const { session, signIn, completeTwoFactor, signUp } = useAuth()
  const { t } = usePreferences()
  const features = [
    ['◌', t('shareWorld'), t('shareWorldDescription')],
    ['⌁', t('stayConnected'), t('stayConnectedDescription')],
    ['⌘', t('madeForCircle'), t('madeForCircleDescription')],
  ]
  const [searchParams, setSearchParams] = useSearchParams()
  const [isRegistering, setIsRegistering] = useState(false)
  const [email, setEmail] = useState(() => searchParams.get('email') ?? '')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [isPasswordVisible, setIsPasswordVisible] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [twoFactorChallenge, setTwoFactorChallenge] = useState<string | null>(null)
  const [twoFactorCode, setTwoFactorCode] = useState('')
  const [verificationState, setVerificationState] = useState<'loading' | 'success' | 'error'>('loading')

  const accountMode = searchParams.get('mode')
  const linkedEmail = searchParams.get('email') ?? ''
  const linkedToken = searchParams.get('token') ?? ''
  const isRequestingReset = accountMode === 'forgot'
  const isResetting = accountMode === 'reset'
  const isVerifying = accountMode === 'verify'
  const isAccountFlow = isRequestingReset || isResetting || isVerifying

  useEffect(() => {
    if (!isVerifying) return

    if (!linkedEmail || !linkedToken) {
      return
    }

    let isActive = true
    void authApi.verifyEmail(linkedEmail, linkedToken)
      .then(() => {
        if (isActive) setVerificationState('success')
      })
      .catch((requestError: unknown) => {
        if (isActive) {
          setVerificationState('error')
          setError(requestError instanceof ApiError ? requestError.message : t('unableVerify'))
        }
      })

    return () => {
      isActive = false
    }
  }, [isVerifying, linkedEmail, linkedToken, t])

  if (session && !isAccountFlow) return <Navigate to="/feed" replace />

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setNotice(null)
    setIsSubmitting(true)

    try {
      if (isRequestingReset) {
        await authApi.requestPasswordReset(email)
        setNotice(t('resetLinkSent'))
      } else if (isResetting) {
        await authApi.resetPassword({ email, token: linkedToken, password, confirmPassword })
        setNotice(t('passwordReset'))
        setPassword('')
        setConfirmPassword('')
      } else if (isRegistering) {
        await signUp({ email, username, password })
      } else {
        const response = await signIn({ email, password })
        if ('twoFactorRequired' in response) setTwoFactorChallenge(response.challenge)
      }
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableAuthenticate'))
    } finally {
      setIsSubmitting(false)
    }
  }

  const switchMode = () => {
    setIsRegistering((current) => !current)
    setError(null)
    setNotice(null)
    setPassword('')
  }

  const returnToSignIn = () => {
    setSearchParams({})
    setError(null)
    setNotice(null)
    setPassword('')
    setConfirmPassword('')
  }

  const fieldClassName = 'w-full rounded-xl border border-border bg-surface-2/70 px-11 py-3 text-[15px] text-text outline-none transition placeholder:text-text-light focus:border-primary focus:bg-surface-2 focus:ring-4 focus:ring-primary/15'

  return (
    <main className="relative isolate min-h-screen overflow-hidden bg-bg px-4 py-6 sm:px-6 lg:flex lg:items-center lg:justify-center lg:p-8">
      <PreferenceControls className="absolute right-4 top-4 z-20 sm:right-6 sm:top-6" />
      <div className="pointer-events-none absolute inset-0 -z-10 overflow-hidden">
        <div className="absolute -left-48 top-[-15%] h-[32rem] w-[32rem] rounded-full bg-primary/20 blur-[130px]" />
        <div className="absolute -bottom-56 right-[-8%] h-[34rem] w-[34rem] rounded-full bg-[#7f5af0]/15 blur-[150px]" />
        <div className="absolute inset-0 opacity-[0.035]" style={{ backgroundImage: 'radial-gradient(#fff 1px, transparent 1px)', backgroundSize: '22px 22px' }} />
      </div>

      <div className="grid w-full max-w-6xl overflow-hidden rounded-[2rem] border border-white/10 bg-surface/85 shadow-[0_30px_100px_rgba(0,0,0,0.45)] backdrop-blur-xl lg:grid-cols-[1.08fr_0.92fr]">
        <section className="relative hidden min-h-[640px] overflow-hidden border-r border-white/10 p-10 lg:flex lg:flex-col">
          <div className="absolute inset-x-0 bottom-0 h-64 bg-gradient-to-t from-[#15284d] to-transparent opacity-80" />
          <div className="relative flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-primary text-2xl font-extrabold text-white shadow-lg shadow-primary/30">f</div>
            <span className="font-heading text-xl font-extrabold tracking-tight text-text">fookbase</span>
          </div>

          <div className="relative mt-auto max-w-lg">
            <span className="mb-5 inline-flex rounded-full border border-primary/30 bg-primary/10 px-3 py-1 text-xs font-semibold tracking-wide text-primary-light">{t('socialSpace')}</span>
            <h1 className="font-heading text-4xl font-extrabold leading-[1.15] tracking-tight text-text xl:text-5xl">{t('quieterCorner')}</h1>
            <p className="mt-5 max-w-md text-base leading-7 text-text-muted">{t('socialDescription')}</p>

            <div className="mt-10 grid gap-4 sm:grid-cols-3 lg:grid-cols-1 xl:grid-cols-3">
              {features.map(([icon, title, description]) => (
                <div key={title} className="rounded-2xl border border-white/10 bg-black/10 p-4">
                  <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/15 text-lg text-primary-light">{icon}</span>
                  <h2 className="mt-3 text-sm font-bold text-text">{title}</h2>
                  <p className="mt-1 text-xs leading-5 text-text-muted">{description}</p>
                </div>
              ))}
            </div>
          </div>
        </section>

        <section className="flex min-h-[620px] items-center justify-center p-5 sm:p-10 lg:p-12">
          <form onSubmit={isVerifying ? (event) => event.preventDefault() : submit} className="w-full max-w-md">
            <div className="mb-9 flex items-center justify-between lg:hidden">
              <div className="flex items-center gap-2.5">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary text-xl font-extrabold text-white">f</div>
                <span className="font-heading text-lg font-extrabold tracking-tight text-text">fookbase</span>
              </div>
              <span className="text-xs font-semibold text-text-light">{t('socialSimplified')}</span>
            </div>

            <div className="mb-7">
              <p className="text-sm font-semibold text-primary-light">{isVerifying ? t('emailVerification') : isResetting ? t('resetPassword') : isRequestingReset ? t('accountRecovery') : isRegistering ? t('joinFookbase') : t('welcomeBack')}</p>
              <h1 className="mt-2 font-heading text-3xl font-extrabold tracking-tight text-text sm:text-4xl">{isVerifying ? t('verifyEmailTitle') : isResetting ? t('newPasswordTitle') : isRequestingReset ? t('resetPasswordTitle') : isRegistering ? t('createSpaceTitle') : t('signInSpaceTitle')}</h1>
              <p className="mt-3 text-sm leading-6 text-text-muted">{isVerifying ? t('verifyEmailDescription') : isResetting ? t('newPasswordDescription') : isRequestingReset ? t('resetPasswordDescription') : isRegistering ? t('createSpaceDescription') : t('signInSpaceDescription')}</p>
            </div>

            {error && <p role="alert" className="mb-5 rounded-xl border border-[#e15f5f]/45 bg-[#e15f5f]/10 px-4 py-3 text-sm leading-5 text-[#ff9b9b]">{error}</p>}
            {notice && <p role="status" className="mb-5 rounded-xl border border-primary/35 bg-primary/10 px-4 py-3 text-sm leading-5 text-primary-light">{notice}</p>}

            {twoFactorChallenge ? (
              <div className="flex flex-col gap-5"><label className="flex flex-col gap-2 text-sm font-semibold text-text">Mã xác thực<input autoFocus inputMode="numeric" autoComplete="one-time-code" value={twoFactorCode} onChange={(event) => setTwoFactorCode(event.target.value)} className={fieldClassName} /></label><button type="button" className="rounded-xl bg-primary px-4 py-3 font-bold text-white" disabled={isSubmitting || !twoFactorCode.trim()} onClick={() => { setIsSubmitting(true); void completeTwoFactor(twoFactorChallenge, twoFactorCode).catch((reason) => setError(reason instanceof ApiError ? reason.message : t('unableAuthenticate'))).finally(() => setIsSubmitting(false)) }}>Xác minh</button></div>
            ) : isVerifying ? (
              <div className="rounded-xl border border-border bg-surface-2/60 p-5 text-sm leading-6 text-text-muted">
                {(!linkedEmail || !linkedToken) && t('invalidVerificationLink')}
                {linkedEmail && linkedToken && verificationState === 'loading' && t('verifyingEmail')}
                {verificationState === 'success' && t('emailVerified')}
                {verificationState === 'error' && t('unableVerifyEmail')}
              </div>
            ) : (
              <>
                <div className="flex flex-col gap-5">
                  <label className="flex flex-col gap-2 text-sm font-semibold text-text">
                    {t('emailAddress')}
                    <span className="relative block">
                      <span className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-text-light">@</span>
                      <input required readOnly={isResetting && Boolean(linkedEmail)} autoComplete="email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="you@example.com" className={fieldClassName} />
                    </span>
                  </label>

                  {isRegistering && (
                    <label className="flex flex-col gap-2 text-sm font-semibold text-text">
                      {t('username')}
                      <span className="relative block">
                        <span className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-text-light">#</span>
                        <input required minLength={3} maxLength={32} autoComplete="username" value={username} onChange={(event) => setUsername(event.target.value)} placeholder={t('chooseUsername')} className={fieldClassName} />
                      </span>
                    </label>
                  )}

                  {!isRequestingReset && (
                    <label className="flex flex-col gap-2 text-sm font-semibold text-text">
                      {t('password')}
                      <span className="relative block">
                        <span className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-text-light">⌁</span>
                        <input required minLength={isRegistering || isResetting ? 8 : undefined} autoComplete={isRegistering || isResetting ? 'new-password' : 'current-password'} type={isPasswordVisible ? 'text' : 'password'} value={password} onChange={(event) => setPassword(event.target.value)} placeholder={isRegistering || isResetting ? t('passwordAtLeastEight') : t('yourPassword')} className={`${fieldClassName} pr-16`} />
                        <button type="button" onClick={() => setIsPasswordVisible((current) => !current)} className="absolute right-3 top-1/2 -translate-y-1/2 rounded-lg px-2 py-1 text-xs font-bold text-text-muted transition hover:bg-white/5 hover:text-text" aria-label={isPasswordVisible ? t('hidePassword') : t('showPassword')}>{isPasswordVisible ? t('hide') : t('show')}</button>
                      </span>
                    </label>
                  )}

                  {isResetting && (
                    <label className="flex flex-col gap-2 text-sm font-semibold text-text">
                      {t('confirmPassword')}
                      <input required minLength={8} autoComplete="new-password" type="password" value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} placeholder={t('repeatPassword')} className={fieldClassName} />
                    </label>
                  )}
                </div>

                {isRegistering && <p className="mt-4 text-xs leading-5 text-text-light">{t('respectfulUse')}</p>}

                <button disabled={isSubmitting} className="mt-7 flex w-full items-center justify-center gap-2 rounded-xl bg-primary px-4 py-3.5 text-sm font-bold text-white shadow-lg shadow-primary/25 transition hover:bg-primary-dark hover:shadow-primary/35 disabled:cursor-not-allowed disabled:opacity-60">
                  {isSubmitting && <span className="h-4 w-4 animate-spin rounded-full border-2 border-white/30 border-t-white" />}
                  {isSubmitting ? t('pleaseWait') : isRequestingReset ? t('sendResetLink') : isResetting ? t('resetPasswordAction') : isRegistering ? t('createAccount') : t('signIn')}
                </button>
              </>
            )}

            {isVerifying || isRequestingReset || isResetting ? (
              <button type="button" onClick={returnToSignIn} className="mt-6 w-full rounded-xl border border-border bg-surface-2/40 px-4 py-3 text-sm font-bold text-text transition hover:border-primary/60 hover:bg-surface-2">{t('backToSignIn')}</button>
            ) : (
              <>
                {!isRegistering && <button type="button" onClick={() => setSearchParams({ mode: 'forgot' })} className="mt-4 text-sm font-semibold text-primary-light hover:text-text">{t('forgotPassword')}</button>}
                <div className="my-6 flex items-center gap-3 text-xs font-medium text-text-light"><span className="h-px flex-1 bg-border" />{t('or')}<span className="h-px flex-1 bg-border" /></div>
                <button type="button" onClick={switchMode} className="w-full rounded-xl border border-border bg-surface-2/40 px-4 py-3 text-sm font-bold text-text transition hover:border-primary/60 hover:bg-surface-2">
                  {isRegistering ? t('alreadyHaveAccount') : t('createNewAccount')}
                </button>
              </>
            )}
          </form>
        </section>
      </div>
    </main>
  )
}
