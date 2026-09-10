import { useState, type FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { useAuth } from '../../auth/useAuth'

const features = [
  ['◌', 'Share your world', 'Posts, reactions, and conversations that feel close.'],
  ['⌁', 'Stay connected', 'Private messages and live updates with your friends.'],
  ['⌘', 'Made for your circle', 'Your feed is shaped around the people you know.'],
]

export default function LoginPage() {
  const { session, signIn, signUp } = useAuth()
  const [isRegistering, setIsRegistering] = useState(false)
  const [email, setEmail] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [isPasswordVisible, setIsPasswordVisible] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (session) return <Navigate to="/feed" replace />

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)

    try {
      if (isRegistering) {
        await signUp({ email, username, password })
      } else {
        await signIn({ email, password })
      }
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể xác thực.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const switchMode = () => {
    setIsRegistering((current) => !current)
    setError(null)
    setPassword('')
  }

  const fieldClassName = 'w-full rounded-xl border border-border bg-surface-2/70 px-11 py-3 text-[15px] text-text outline-none transition placeholder:text-text-light focus:border-primary focus:bg-surface-2 focus:ring-4 focus:ring-primary/15'

  return (
    <main className="relative isolate min-h-screen overflow-hidden bg-[#0e1118] px-4 py-6 sm:px-6 lg:flex lg:items-center lg:justify-center lg:p-8">
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
            <span className="mb-5 inline-flex rounded-full border border-primary/30 bg-primary/10 px-3 py-1 text-xs font-semibold tracking-wide text-primary-light">YOUR SOCIAL SPACE</span>
            <h1 className="font-heading text-4xl font-extrabold leading-[1.15] tracking-tight text-text xl:text-5xl">A quieter corner of the internet.</h1>
            <p className="mt-5 max-w-md text-base leading-7 text-text-muted">Keep up with your people, share what matters, and turn everyday moments into conversations.</p>

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
          <form onSubmit={submit} className="w-full max-w-md">
            <div className="mb-9 flex items-center justify-between lg:hidden">
              <div className="flex items-center gap-2.5">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary text-xl font-extrabold text-white">f</div>
                <span className="font-heading text-lg font-extrabold tracking-tight text-text">fookbase</span>
              </div>
              <span className="text-xs font-semibold text-text-light">SOCIAL, SIMPLIFIED</span>
            </div>

            <div className="mb-7">
              <p className="text-sm font-semibold text-primary-light">{isRegistering ? 'JOIN FOOKBASE' : 'WELCOME BACK'}</p>
              <h1 className="mt-2 font-heading text-3xl font-extrabold tracking-tight text-text sm:text-4xl">{isRegistering ? 'Create your space.' : 'Sign in to your space.'}</h1>
              <p className="mt-3 text-sm leading-6 text-text-muted">{isRegistering ? 'Set up your account in a moment and start connecting.' : 'Enter your details to continue where you left off.'}</p>
            </div>

            {error && <p role="alert" className="mb-5 rounded-xl border border-[#e15f5f]/45 bg-[#e15f5f]/10 px-4 py-3 text-sm leading-5 text-[#ff9b9b]">{error}</p>}

            <div className="flex flex-col gap-5">
              <label className="flex flex-col gap-2 text-sm font-semibold text-text">
                Email address
                <span className="relative block">
                  <span className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-text-light">@</span>
                  <input required autoComplete="email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} placeholder="you@example.com" className={fieldClassName} />
                </span>
              </label>

              {isRegistering && (
                <label className="flex flex-col gap-2 text-sm font-semibold text-text">
                  Username
                  <span className="relative block">
                    <span className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-text-light">#</span>
                    <input required minLength={3} maxLength={32} autoComplete="username" value={username} onChange={(event) => setUsername(event.target.value)} placeholder="Choose a username" className={fieldClassName} />
                  </span>
                </label>
              )}

              <label className="flex flex-col gap-2 text-sm font-semibold text-text">
                Password
                <span className="relative block">
                  <span className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-text-light">⌁</span>
                  <input required minLength={isRegistering ? 8 : undefined} autoComplete={isRegistering ? 'new-password' : 'current-password'} type={isPasswordVisible ? 'text' : 'password'} value={password} onChange={(event) => setPassword(event.target.value)} placeholder={isRegistering ? 'At least 8 characters' : 'Your password'} className={`${fieldClassName} pr-16`} />
                  <button type="button" onClick={() => setIsPasswordVisible((current) => !current)} className="absolute right-3 top-1/2 -translate-y-1/2 rounded-lg px-2 py-1 text-xs font-bold text-text-muted transition hover:bg-white/5 hover:text-text" aria-label={isPasswordVisible ? 'Hide password' : 'Show password'}>{isPasswordVisible ? 'HIDE' : 'SHOW'}</button>
                </span>
              </label>
            </div>

            {isRegistering && <p className="mt-4 text-xs leading-5 text-text-light">By creating an account, you agree to use Fookbase respectfully and keep your login details private.</p>}

            <button disabled={isSubmitting} className="mt-7 flex w-full items-center justify-center gap-2 rounded-xl bg-primary px-4 py-3.5 text-sm font-bold text-white shadow-lg shadow-primary/25 transition hover:bg-primary-dark hover:shadow-primary/35 disabled:cursor-not-allowed disabled:opacity-60">
              {isSubmitting && <span className="h-4 w-4 animate-spin rounded-full border-2 border-white/30 border-t-white" />}
              {isSubmitting ? 'Please wait...' : isRegistering ? 'Create account' : 'Sign in'}
            </button>

            <div className="my-6 flex items-center gap-3 text-xs font-medium text-text-light"><span className="h-px flex-1 bg-border" />OR<span className="h-px flex-1 bg-border" /></div>

            <button type="button" onClick={switchMode} className="w-full rounded-xl border border-border bg-surface-2/40 px-4 py-3 text-sm font-bold text-text transition hover:border-primary/60 hover:bg-surface-2">
              {isRegistering ? 'I already have an account' : 'Create a new account'}
            </button>
          </form>
        </section>
      </div>
    </main>
  )
}
