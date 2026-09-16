import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Navigate, useSearchParams } from 'react-router-dom'
import { authApi, type RegistrationChallenge } from '../../api/auth'
import { ApiError, apiBaseUrl } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { PreferenceControls, usePreferences } from '../../preferences'

function LoginArtwork() {
  return <div className="relative h-[590px] w-full max-w-[560px]">
    <div className="absolute left-[26%] top-4 h-[430px] w-[290px] overflow-hidden rounded-[2rem] bg-[linear-gradient(145deg,#36b5da_0%,#1978d4_45%,#8ed7f0_100%)] shadow-2xl shadow-[#1877f2]/20">
      <div className="absolute inset-x-6 top-6 h-1 rounded-full bg-white/70" />
      <div className="absolute -right-16 bottom-[-20%] h-72 w-72 rounded-full bg-white/30 blur-2xl" />
      <div className="absolute bottom-12 left-9 h-36 w-52 -rotate-12 rounded-[2rem] border-[10px] border-white/85 bg-[#f2b778] shadow-xl" />
      <div className="absolute bottom-20 left-[43%] h-32 w-28 rotate-12 rounded-[2rem] bg-[#1b2638] shadow-xl" />
    </div>
    <div className="absolute left-[4%] top-32 h-64 w-64 overflow-hidden rounded-[1.6rem] bg-[linear-gradient(145deg,#ffd1b6,#fa8d62)] shadow-xl">
      <div className="absolute -left-12 top-10 h-52 w-72 rotate-12 rounded-[3rem] bg-[#8fd7ee]" />
      <div className="absolute bottom-6 left-8 h-24 w-36 rounded-3xl bg-white/65" />
    </div>
    <div className="absolute bottom-4 left-[17%] h-[260px] w-[245px] rounded-[1.7rem] bg-white p-4 shadow-2xl">
      <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-sm font-black text-white">★</div>
      <div className="mt-4 h-36 rounded-xl bg-[linear-gradient(140deg,#8f2735,#df7359_55%,#ead3a8)]" />
      <div className="mt-4 h-4 w-4/5 rounded-full bg-[#e4e6eb]" />
      <div className="mt-2 h-3 w-3/5 rounded-full bg-[#e4e6eb]" />
    </div>
    <div className="absolute bottom-[-2%] left-[49%] h-40 w-40 rounded-full border-[5px] border-primary bg-[radial-gradient(circle_at_55%_34%,#f5d0c3_0_28%,#934f42_29%_32%,#f8bf9f_33%_66%,#3d2a29_67%)] shadow-xl" />
    <div className="absolute right-[1%] top-[59%] grid h-20 w-20 place-items-center rounded-full bg-[#f02849] text-4xl text-white shadow-xl">♥</div>
    <div className="absolute left-[1%] top-[4%] grid h-16 w-16 place-items-center rounded-full bg-[#ffd35c] text-4xl shadow-lg">☺</div>
  </div>
}

export default function LoginPage() {
  const { session, signIn, completeGoogleSignIn, linkGoogleSignIn, completeTwoFactor, signUp, completeRegistration, resendRegistration } = useAuth()
  const { t } = usePreferences()
  const [searchParams, setSearchParams] = useSearchParams()
  const googleCompletionCode = searchParams.get('provider') === 'google' ? searchParams.get('code') : null
  const callbackRequiresGoogleLink = searchParams.get('mode') === 'link'
  const [isRegistering, setIsRegistering] = useState(false)
  const [email, setEmail] = useState(() => searchParams.get('email') ?? '')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [birthDay, setBirthDay] = useState('')
  const [birthMonth, setBirthMonth] = useState('')
  const [birthYear, setBirthYear] = useState('')
  const [gender, setGender] = useState<'female' | 'male' | 'other' | 'preferNotToSay'>('preferNotToSay')
  const [registrationChallenge, setRegistrationChallenge] = useState<RegistrationChallenge | null>(null)
  const [registrationCode, setRegistrationCode] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [isPasswordVisible, setIsPasswordVisible] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [twoFactorChallenge, setTwoFactorChallenge] = useState<string | null>(null)
  const [twoFactorCode, setTwoFactorCode] = useState('')
  const [verificationState, setVerificationState] = useState<'loading' | 'success' | 'error'>('loading')
  const [googleEnabled, setGoogleEnabled] = useState(false)
  const [isCompletingGoogle, setIsCompletingGoogle] = useState(
    () => Boolean(googleCompletionCode && !callbackRequiresGoogleLink))
  const [googleLinkCompletion, setGoogleLinkCompletion] = useState(() =>
    callbackRequiresGoogleLink && googleCompletionCode
      ? { code: googleCompletionCode, email: searchParams.get('email') ?? '' }
      : null)
  const completedGoogleCodes = useRef(new Set<string>())

  const accountMode = searchParams.get('mode')
  const linkedEmail = searchParams.get('email') ?? ''
  const linkedToken = searchParams.get('token') ?? ''
  const isRequestingReset = accountMode === 'forgot'
  const isResetting = accountMode === 'reset'
  const isVerifying = accountMode === 'verify'
  const isAccountFlow = isRequestingReset || isResetting || isVerifying
  const isGoogleLinking = googleLinkCompletion !== null
  const isEmbeddedBrowser = /\b(Zalo|FBAN|FBAV|Messenger)\b/i.test(navigator.userAgent)

  useEffect(() => {
    if (isAccountFlow || isRegistering || isGoogleLinking) return

    let isActive = true
    void authApi.providers()
      .then((providers) => {
        if (isActive) setGoogleEnabled(providers.google)
      })
      .catch(() => {
        if (isActive) setGoogleEnabled(false)
      })

    return () => {
      isActive = false
    }
  }, [isAccountFlow, isGoogleLinking, isRegistering])

  useEffect(() => {
    if (!googleCompletionCode || completedGoogleCodes.current.has(googleCompletionCode)) return

    completedGoogleCodes.current.add(googleCompletionCode)
    window.history.replaceState({}, document.title, window.location.pathname)
    if (callbackRequiresGoogleLink) return

    let isActive = true
    void completeGoogleSignIn(googleCompletionCode)
      .then((response) => {
        if (isActive && 'twoFactorRequired' in response) setTwoFactorChallenge(response.challenge)
      })
      .catch((requestError: unknown) => {
        if (isActive) setError(requestError instanceof ApiError ? requestError.message : t('googleCompletionFailed'))
      })
      .finally(() => {
        if (isActive) setIsCompletingGoogle(false)
      })

    return () => {
      isActive = false
    }
  }, [callbackRequiresGoogleLink, completeGoogleSignIn, googleCompletionCode, t])

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
      if (isGoogleLinking) {
        const response = await linkGoogleSignIn(googleLinkCompletion.code, password)
        if ('twoFactorRequired' in response) setTwoFactorChallenge(response.challenge)
      } else if (isRequestingReset) {
        await authApi.requestPasswordReset(email)
        setNotice(t('resetLinkSent'))
      } else if (isResetting) {
        await authApi.resetPassword({ email, token: linkedToken, password, confirmPassword })
        setNotice(t('passwordReset'))
        setPassword('')
        setConfirmPassword('')
      } else if (isRegistering) {
        if (registrationChallenge) {
          await completeRegistration(registrationChallenge.challengeId, registrationCode)
        } else {
          const dateOfBirth = `${birthYear}-${birthMonth.padStart(2, '0')}-${birthDay.padStart(2, '0')}`
          setRegistrationChallenge(await signUp({ firstName, lastName, dateOfBirth, gender, contact: email, password }))
          setRegistrationCode('')
        }
      } else {
        const response = await signIn({ identifier: email, password })
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
    setRegistrationChallenge(null)
    setRegistrationCode('')
  }

  const returnToSignIn = () => {
    setSearchParams({})
    setGoogleLinkCompletion(null)
    setError(null)
    setNotice(null)
    setPassword('')
    setConfirmPassword('')
    setRegistrationChallenge(null)
    setRegistrationCode('')
  }

  const fieldClassName = 'w-full rounded-lg border border-border bg-surface px-11 py-3 text-[15px] text-text outline-none transition placeholder:text-text-light focus:border-primary focus:bg-surface focus:ring-4 focus:ring-primary/15'

  return (
    <main className="fookbase-login relative flex min-h-screen flex-col overflow-hidden bg-[#f5f6f7] px-4 py-5 sm:px-6 lg:p-0">
      <PreferenceControls className="absolute right-5 top-5 z-20 sm:right-8 sm:top-8" />
      <div className="grid flex-1 overflow-hidden border border-[#e4e6eb] bg-white lg:grid-cols-[minmax(230px,0.78fr)_minmax(470px,1.2fr)_minmax(360px,0.78fr)]">
        <section className="relative hidden min-h-[760px] border-r border-[#e4e6eb] p-9 xl:flex xl:flex-col">
          <div className="flex items-center gap-3 text-primary"><div className="grid h-12 w-12 place-items-center rounded-full bg-primary text-4xl font-bold text-white">f</div><span className="font-heading text-xl font-extrabold tracking-tight text-[#1c1e21]">Fookbase</span></div>
          <div className="mt-auto pb-6"><h1 className="font-heading text-4xl font-extrabold leading-[1.08] tracking-tight text-[#1c1e21]">Khám phá<br />những điều<br /><span className="text-primary">bạn yêu<br />thích.</span></h1></div>
        </section>
        <section className="relative hidden min-h-[760px] items-center justify-center overflow-hidden border-r border-[#e4e6eb] bg-white px-8 xl:flex"><LoginArtwork /></section>
        <section className="flex min-h-[620px] items-center justify-center bg-white p-5 sm:p-10 lg:p-12">
          <form onSubmit={isVerifying ? (event) => event.preventDefault() : submit} className="w-full max-w-md">
            <div className="mb-9 flex items-center justify-between xl:hidden">
              <div className="flex items-center gap-2.5">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary text-xl font-extrabold text-white">f</div>
                <span className="font-heading text-lg font-extrabold tracking-tight text-text">fookbase</span>
              </div>
              <span className="text-xs font-semibold text-text-light">{t('socialSimplified')}</span>
            </div>

            <div className="mb-7">
              <p className="text-sm font-semibold text-primary-light">{isGoogleLinking ? t('continueWithGoogle') : isVerifying ? t('emailVerification') : isResetting ? t('resetPassword') : isRequestingReset ? t('accountRecovery') : registrationChallenge ? t('registrationVerification') : isRegistering ? t('joinFookbase') : t('welcomeBack')}</p>
              <h1 className="mt-2 font-heading text-3xl font-extrabold tracking-tight text-text sm:text-4xl">{isGoogleLinking ? t('signInSpaceTitle') : isVerifying ? t('verifyEmailTitle') : isResetting ? t('newPasswordTitle') : isRequestingReset ? t('resetPasswordTitle') : registrationChallenge ? t('registrationVerificationTitle') : isRegistering ? t('createSpaceTitle') : t('signInSpaceTitle')}</h1>
              <p className="mt-3 text-sm leading-6 text-text-muted">{isGoogleLinking ? t('confirmGooglePassword').replace('{email}', googleLinkCompletion.email) : isVerifying ? t('verifyEmailDescription') : isResetting ? t('newPasswordDescription') : isRequestingReset ? t('resetPasswordDescription') : registrationChallenge ? t('registrationVerificationDescription') : isRegistering ? t('createSpaceDescription') : t('signInSpaceDescription')}</p>
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
            ) : registrationChallenge ? (
              <div className="flex flex-col gap-5">
                <label className="flex flex-col gap-2 text-sm font-semibold text-text">
                  {t('verificationCode')}
                  <input required autoFocus inputMode="numeric" autoComplete="one-time-code" maxLength={6} value={registrationCode} onChange={(event) => setRegistrationCode(event.target.value.replace(/\D/g, ''))} placeholder="123456" className={fieldClassName} />
                </label>
                <button disabled={isSubmitting || registrationCode.length !== 6} className="rounded-xl bg-primary px-4 py-3.5 text-sm font-bold text-white shadow-lg shadow-primary/25 disabled:cursor-not-allowed disabled:opacity-60">
                  {isSubmitting ? t('pleaseWait') : t('verifyAndCreateAccount')}
                </button>
                <button type="button" disabled={isSubmitting} onClick={() => { setIsSubmitting(true); void resendRegistration(registrationChallenge.challengeId).then(setRegistrationChallenge).catch((reason) => setError(reason instanceof ApiError ? reason.message : t('unableAuthenticate'))).finally(() => setIsSubmitting(false)) }} className="rounded-xl border border-border bg-surface-2/40 px-4 py-3 text-sm font-bold text-text disabled:opacity-60">
                  {t('resendCode')}
                </button>
                <button type="button" disabled={isSubmitting} onClick={() => { setRegistrationChallenge(null); setRegistrationCode(''); setError(null) }} className="text-sm font-semibold text-primary-light hover:text-text">
                  {t('editRegistrationDetails')}
                </button>
              </div>
            ) : (
              <>
                <div className="flex flex-col gap-5">
                  {isRegistering && <>
                    <div className="grid grid-cols-2 gap-3">
                      <label className="flex flex-col gap-2 text-sm font-semibold text-text">{t('lastName')}<input required maxLength={50} autoComplete="family-name" value={lastName} onChange={(event) => setLastName(event.target.value)} className={fieldClassName.replace('px-11', 'px-4')} /></label>
                      <label className="flex flex-col gap-2 text-sm font-semibold text-text">{t('firstName')}<input required maxLength={50} autoComplete="given-name" value={firstName} onChange={(event) => setFirstName(event.target.value)} className={fieldClassName.replace('px-11', 'px-4')} /></label>
                    </div>
                    <fieldset className="flex flex-col gap-2 text-sm font-semibold text-text"><legend>{t('dateOfBirth')}</legend><div className="grid grid-cols-3 gap-3"><select required value={birthDay} onChange={(event) => setBirthDay(event.target.value)} className={fieldClassName.replace('px-11', 'px-4')}><option value="">{t('day')}</option>{Array.from({ length: 31 }, (_, index) => <option key={index + 1} value={String(index + 1)}>{index + 1}</option>)}</select><select required value={birthMonth} onChange={(event) => setBirthMonth(event.target.value)} className={fieldClassName.replace('px-11', 'px-4')}><option value="">{t('month')}</option>{Array.from({ length: 12 }, (_, index) => <option key={index + 1} value={String(index + 1)}>{index + 1}</option>)}</select><select required value={birthYear} onChange={(event) => setBirthYear(event.target.value)} className={fieldClassName.replace('px-11', 'px-4')}><option value="">{t('year')}</option>{Array.from({ length: 100 }, (_, index) => new Date().getFullYear() - 13 - index).map((year) => <option key={year} value={String(year)}>{year}</option>)}</select></div></fieldset>
                    <label className="flex flex-col gap-2 text-sm font-semibold text-text">{t('gender')}<select value={gender} onChange={(event) => setGender(event.target.value as typeof gender)} className={fieldClassName.replace('px-11', 'px-4')}><option value="female">{t('female')}</option><option value="male">{t('male')}</option><option value="other">{t('otherGender')}</option><option value="preferNotToSay">{t('preferNotToSay')}</option></select></label>
                  </>}

                  <label className="flex flex-col gap-2 text-sm font-semibold text-text">
                    {isRegistering || !isResetting ? t('mobileOrEmail') : t('emailAddress')}
                    <span className="relative block">
                      <span className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-text-light">@</span>
                      <input required readOnly={(isResetting && Boolean(linkedEmail)) || isGoogleLinking} autoComplete={isRegistering ? 'email' : 'username'} type={isResetting ? 'email' : 'text'} value={email} onChange={(event) => setEmail(event.target.value)} placeholder={t('mobileOrEmailPlaceholder')} className={fieldClassName} />
                    </span>
                  </label>

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

                <button disabled={isSubmitting || isCompletingGoogle} className="mt-7 flex w-full items-center justify-center gap-2 rounded-xl bg-primary px-4 py-3.5 text-sm font-bold text-white shadow-lg shadow-primary/25 transition hover:bg-primary-dark hover:shadow-primary/35 disabled:cursor-not-allowed disabled:opacity-60">
                  {(isSubmitting || isCompletingGoogle) && <span className="h-4 w-4 animate-spin rounded-full border-2 border-white/30 border-t-white" />}
                  {isSubmitting || isCompletingGoogle ? t('pleaseWait') : isGoogleLinking ? t('continueWithGoogle') : isRequestingReset ? t('sendResetLink') : isResetting ? t('resetPasswordAction') : isRegistering ? t('createAccount') : t('signIn')}
                </button>
              </>
            )}

            {isVerifying || isRequestingReset || isResetting ? (
              <button type="button" onClick={returnToSignIn} className="mt-6 w-full rounded-xl border border-border bg-surface-2/40 px-4 py-3 text-sm font-bold text-text transition hover:border-primary/60 hover:bg-surface-2">{t('backToSignIn')}</button>
            ) : (
              <>
                {!isRegistering && !isGoogleLinking && <button type="button" onClick={() => setSearchParams({ mode: 'forgot' })} className="mt-4 text-sm font-semibold text-primary-light hover:text-text">{t('forgotPassword')}</button>}
                {!isRegistering && !isGoogleLinking && googleEnabled && <>
                  <div className="my-6 flex items-center gap-3 text-xs font-medium text-text-light"><span className="h-px flex-1 bg-border" />{t('or')}<span className="h-px flex-1 bg-border" /></div>
                  {isEmbeddedBrowser ? <p className="rounded-xl border border-border bg-surface-2/60 px-4 py-3 text-sm leading-5 text-text-muted">{t('openExternalBrowserGoogle')}</p> : <button type="button" onClick={() => window.location.assign(`${apiBaseUrl}/api/auth/google/start?client=web`)} className="w-full rounded-xl border border-border bg-white px-4 py-3 text-sm font-bold text-text transition hover:border-primary/60 hover:bg-surface-2"><span className="mr-2 text-base text-[#4285f4]">G</span>{t('continueWithGoogle')}</button>}
                </>}
                {!isGoogleLinking && <><div className="my-6 flex items-center gap-3 text-xs font-medium text-text-light"><span className="h-px flex-1 bg-border" />{t('or')}<span className="h-px flex-1 bg-border" /></div>
                  <button type="button" onClick={switchMode} className="w-full rounded-xl border border-border bg-surface-2/40 px-4 py-3 text-sm font-bold text-text transition hover:border-primary/60 hover:bg-surface-2">
                    {isRegistering ? t('alreadyHaveAccount') : t('createNewAccount')}
                  </button></>}
              </>
            )}
          </form>
        </section>
      </div>
      <footer className="hidden border-t border-[#e4e6eb] bg-white px-8 py-5 text-center text-xs leading-6 text-[#8a8d91] xl:block">Tiếng Việt · English (UK) · Français (France) · 日本語 · Đăng ký · Đăng nhập · Zola Light · Fookbase · Điều khoản · Quyền riêng tư · Cookie</footer>
    </main>
  )
}
