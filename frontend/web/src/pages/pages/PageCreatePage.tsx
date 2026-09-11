import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { pagesApi } from '../../api/pages'

export default function PageCreatePage() {
  const navigate = useNavigate()
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const create = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setIsSaving(true)
    setError(null)
    try {
      const page = await pagesApi.create({
        name: String(form.get('name') ?? ''),
        username: String(form.get('username') ?? ''),
        category: String(form.get('category') ?? ''),
        bio: String(form.get('bio') ?? '') || null,
      })
      navigate(`/pages/${page.username}`)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to create this Page.')
    } finally {
      setIsSaving(false)
    }
  }

  return <main className="mx-auto min-h-screen w-full max-w-xl px-3 py-8 sm:px-5"><Link to="/pages" className="text-sm font-semibold text-primary no-underline">← Pages</Link><section className="mt-4 rounded-2xl border border-border bg-surface p-5"><h1 className="font-heading text-3xl font-bold text-text">Create a Page</h1><p className="mt-2 text-sm text-text-muted">A Page starts unpublished. You can prepare its identity and invite your team before publishing.</p>{error && <p className="mt-4 rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-3 text-sm text-[#ff8a9b]">{error}</p>}<form onSubmit={create} className="mt-5 flex flex-col gap-4"><label className="text-sm font-semibold text-text">Name<input name="name" required maxLength={120} className="mt-1 w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-text" /></label><label className="text-sm font-semibold text-text">Username<input name="username" required minLength={3} maxLength={50} pattern="[a-zA-Z0-9._]+" placeholder="your.page" className="mt-1 w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-text" /></label><p className="-mt-3 text-xs text-text-muted">Lowercase letters, numbers, dots and underscores only. It becomes your public address.</p><label className="text-sm font-semibold text-text">Category<input name="category" required maxLength={80} placeholder="Community, Artist, Local business…" className="mt-1 w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-text" /></label><label className="text-sm font-semibold text-text">Bio<textarea name="bio" maxLength={2000} rows={4} className="mt-1 w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-text" /></label><button disabled={isSaving} className="rounded-lg border-0 bg-primary px-4 py-2.5 font-semibold text-white cursor-pointer disabled:opacity-60">{isSaving ? 'Creating…' : 'Create unpublished Page'}</button></form></section></main>
}
