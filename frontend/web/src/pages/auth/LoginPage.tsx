import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { useAuth } from '../../auth/useAuth'

export default function LoginPage() {
  const { session, signIn, signUp } = useAuth()
  const [isRegistering, setIsRegistering] = useState(false)
  const [email, setEmail] = useState('')
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (session) return <Navigate to="/feed" replace />

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
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

  return (
    <main className="min-h-screen bg-bg flex items-center justify-center p-4">
      <form onSubmit={submit} className="w-full max-w-md bg-surface border border-border rounded-2xl p-6 flex flex-col gap-4 shadow-xl">
        <div>
          <div className="w-10 h-10 rounded-full bg-primary text-white flex items-center justify-center font-bold text-xl mb-4">f</div>
          <h1 className="font-heading font-bold text-2xl text-text">{isRegistering ? 'Create account' : 'Welcome back'}</h1>
          <p className="text-sm text-text-muted mt-1">Sign in to use Fookbase.</p>
        </div>

        {error && <p className="rounded-lg bg-[#e41e3f]/10 border border-[#e41e3f]/40 px-3 py-2 text-sm text-[#ff8a9b]">{error}</p>}

        <label className="flex flex-col gap-1.5 text-sm text-text">
          Email
          <input required type="email" value={email} onChange={(event) => setEmail(event.target.value)} className="bg-surface-2 border border-border rounded-lg px-3 py-2 text-text outline-none focus:input-focus" />
        </label>
        {isRegistering && (
          <label className="flex flex-col gap-1.5 text-sm text-text">
            Username
            <input required value={username} onChange={(event) => setUsername(event.target.value)} className="bg-surface-2 border border-border rounded-lg px-3 py-2 text-text outline-none focus:input-focus" />
          </label>
        )}
        <label className="flex flex-col gap-1.5 text-sm text-text">
          Password
          <input required type="password" value={password} onChange={(event) => setPassword(event.target.value)} className="bg-surface-2 border border-border rounded-lg px-3 py-2 text-text outline-none focus:input-focus" />
        </label>

        <button disabled={isSubmitting} className="bg-primary hover:bg-primary-dark disabled:opacity-60 text-white rounded-lg py-2.5 font-semibold border-none cursor-pointer">
          {isSubmitting ? 'Please wait...' : isRegistering ? 'Create account' : 'Sign in'}
        </button>
        <button type="button" onClick={() => setIsRegistering((value) => !value)} className="text-primary bg-transparent border-none cursor-pointer text-sm">
          {isRegistering ? 'Already have an account? Sign in' : 'Need an account? Register'}
        </button>
      </form>
    </main>
  )
}
