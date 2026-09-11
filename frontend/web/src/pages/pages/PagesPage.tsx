import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { pagesApi, type Page, type PageInvitation } from '../../api/pages'
import { resolveProfileImageUrl } from '../../api/users'

function PageCard({ page }: { page: Page }) {
  return <Link to={`/pages/${page.username}`} className="overflow-hidden rounded-xl border border-border bg-surface no-underline transition-colors hover:bg-surface-2">
    {page.coverUrl ? <img src={resolveProfileImageUrl(page.coverUrl)} alt="" className="h-24 w-full object-cover" /> : <div className="h-24 bg-primary/15" />}
    <div className="flex gap-3 p-4"><div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-sm font-bold text-white">{page.avatarUrl ? <img src={resolveProfileImageUrl(page.avatarUrl)} alt="" className="h-full w-full object-cover" /> : page.name.slice(0, 2).toUpperCase()}</div><div className="min-w-0"><h2 className="truncate font-heading text-lg font-bold text-text">{page.name}</h2><p className="truncate text-sm text-text-muted">@{page.username} · {page.category}</p><p className="mt-1 text-xs text-text-muted">{page.followerCount} followers {page.viewerRole ? `· ${page.viewerRole}` : ''}</p></div></div>
  </Link>
}

export default function PagesPage() {
  const [mine, setMine] = useState<Page[]>([])
  const [following, setFollowing] = useState<Page[]>([])
  const [discover, setDiscover] = useState<Page[]>([])
  const [invitations, setInvitations] = useState<PageInvitation[]>([])
  const [query, setQuery] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const load = useCallback(async (search = '') => {
    setIsLoading(true)
    setError(null)
    try {
      const [minePage, followingPage, discoverPage, invitationPage] = await Promise.all([
        pagesApi.mine(), pagesApi.following(), pagesApi.discover(search), pagesApi.invitationsMine(),
      ])
      setMine(minePage.items)
      setFollowing(followingPage.items)
      setDiscover(discoverPage.items)
      setInvitations(invitationPage.items)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to load Pages.')
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => { void load() }, 0)
    return () => window.clearTimeout(timeoutId)
  }, [load])

  const search = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void load(query.trim())
  }

  const respond = async (invitation: PageInvitation, accept: boolean) => {
    try {
      if (accept) await pagesApi.acceptInvitation(invitation.id)
      else await pagesApi.declineInvitation(invitation.id)
      setInvitations((current) => current.filter((item) => item.id !== invitation.id))
      if (accept) await load(query)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to update this invitation.')
    }
  }

  return <main className="mx-auto min-h-screen w-full max-w-6xl px-3 py-5 sm:px-5">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="font-heading text-3xl font-bold text-text">Pages</h1><p className="mt-1 text-sm text-text-muted">Follow public Pages and manage the Pages you represent.</p></div><Link to="/pages/create" className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white no-underline">Create Page</Link></div>
    {error && <p className="mt-4 rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-3 text-sm text-[#ff8a9b]">{error}</p>}
    {isLoading ? <div className="mt-5 h-48 animate-pulse rounded-xl bg-surface-2" /> : <>
      {invitations.length > 0 && <section className="mt-5 rounded-xl border border-border bg-surface p-4"><h2 className="font-heading text-xl font-bold text-text">Role invitations</h2><div className="mt-3 flex flex-col gap-2">{invitations.map((invitation) => <div key={invitation.id} className="flex flex-wrap items-center justify-between gap-3 rounded-lg bg-surface-2 p-3 text-sm text-text"><span>You were invited to manage a Page as <strong>{invitation.role}</strong>.</span><span className="flex gap-2"><button type="button" onClick={() => void respond(invitation, true)} className="rounded-md border-0 bg-primary px-3 py-1.5 font-semibold text-white cursor-pointer">Accept</button><button type="button" onClick={() => void respond(invitation, false)} className="rounded-md border border-border bg-transparent px-3 py-1.5 text-text cursor-pointer">Decline</button></span></div>)}</div></section>}
      <section className="mt-5"><h2 className="font-heading text-xl font-bold text-text">Your Pages</h2>{mine.length === 0 ? <p className="mt-3 rounded-xl border border-border bg-surface p-4 text-sm text-text-muted">You do not manage a Page yet.</p> : <div className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{mine.map((page) => <PageCard key={page.id} page={page} />)}</div>}</section>
      <section className="mt-7"><h2 className="font-heading text-xl font-bold text-text">Following</h2>{following.length === 0 ? <p className="mt-3 rounded-xl border border-border bg-surface p-4 text-sm text-text-muted">Follow Pages to keep track of them here.</p> : <div className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{following.map((page) => <PageCard key={page.id} page={page} />)}</div>}</section>
      <section className="mt-7"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="font-heading text-xl font-bold text-text">Discover Pages</h2><form onSubmit={search}><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search name, @username, category" className="rounded-full border border-border bg-surface px-3 py-2 text-sm text-text outline-none" /></form></div>{discover.length === 0 ? <p className="mt-3 rounded-xl border border-border bg-surface p-4 text-sm text-text-muted">No published Pages match this search.</p> : <div className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{discover.map((page) => <PageCard key={page.id} page={page} />)}</div>}</section>
    </>}
  </main>
}
