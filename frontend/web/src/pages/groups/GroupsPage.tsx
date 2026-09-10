import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { groupsApi } from '../../api/groups'
import type { Group, GroupInvite } from '../../api/groups'

function GroupCard({ group }: { group: Group }) {
  return (
    <Link to={`/groups/${group.id}`} className="block rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2">
      {group.coverUrl
        ? <img src={group.coverUrl} alt="" className="mb-3 h-28 w-full rounded-lg object-cover" />
        : <div className="mb-3 flex h-28 items-center justify-center rounded-lg bg-primary/15 text-3xl">👥</div>}
      <h2 className="font-heading text-base font-bold text-text">{group.name}</h2>
      <p className="mt-1 text-sm text-text-muted">{group.memberCount} members · {group.privacy}</p>
      {group.description && <p className="mt-2 line-clamp-2 text-sm text-text-muted">{group.description}</p>}
    </Link>
  )
}

export default function GroupsPage() {
  const [mine, setMine] = useState<Group[]>([])
  const [discover, setDiscover] = useState<Group[]>([])
  const [mineCursor, setMineCursor] = useState<string | null>(null)
  const [discoverCursor, setDiscoverCursor] = useState<string | null>(null)
  const [invites, setInvites] = useState<GroupInvite[]>([])
  const [query, setQuery] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [privacy, setPrivacy] = useState<'public' | 'private'>('public')
  const [isCreating, setIsCreating] = useState(false)

  const load = useCallback(async (nextQuery = query) => {
    setIsLoading(true)
    setError(null)
    try {
      const [myGroups, publicGroups, pendingInvites] = await Promise.all([
        groupsApi.getMine(),
        groupsApi.discover(nextQuery),
        groupsApi.getMyInvites(),
      ])
      setMine(myGroups.items)
      setMineCursor(myGroups.nextCursor)
      setDiscover(publicGroups.items)
      setDiscoverCursor(publicGroups.nextCursor)
      setInvites(pendingInvites.items)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to load groups.')
    } finally {
      setIsLoading(false)
    }
  }, [query])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => { void load() }, 0)
    return () => window.clearTimeout(timeoutId)
  }, [load])

  const createGroup = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setIsCreating(true)
    setError(null)
    try {
      const group = await groupsApi.create({ name, description, privacy })
      setMine((current) => [group, ...current])
      if (group.privacy === 'public') setDiscover((current) => [group, ...current])
      setName('')
      setDescription('')
      setIsCreateOpen(false)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to create the group.')
    } finally {
      setIsCreating(false)
    }
  }

  const search = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void load(query)
  }

  const loadMoreMine = async () => {
    if (!mineCursor) return
    const page = await groupsApi.getMine(mineCursor)
    setMine((current) => [...current, ...page.items.filter((item) => !current.some((group) => group.id === item.id))])
    setMineCursor(page.nextCursor)
  }

  const loadMoreDiscover = async () => {
    if (!discoverCursor) return
    const page = await groupsApi.discover(query, discoverCursor)
    setDiscover((current) => [...current, ...page.items.filter((item) => !current.some((group) => group.id === item.id))])
    setDiscoverCursor(page.nextCursor)
  }

  const respondToInvite = async (invite: GroupInvite, accept: boolean) => {
    try {
      if (accept) {
        await groupsApi.acceptInvite(invite.groupId, invite.id)
        await load()
      } else {
        await groupsApi.declineInvite(invite.groupId, invite.id)
        setInvites((current) => current.filter((item) => item.id !== invite.id))
      }
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to respond to this invite.')
    }
  }

  return (
    <main className="mx-auto min-h-screen w-full max-w-6xl px-3 py-5 sm:px-5">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div><p className="text-xs font-bold uppercase tracking-widest text-primary">Community</p><h1 className="font-heading text-3xl font-bold text-text">Groups</h1></div>
        <button type="button" onClick={() => setIsCreateOpen((current) => !current)} className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white cursor-pointer">Create group</button>
      </div>

      {error && <div className="mb-4 rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-3 text-sm text-[#ff8a9b]">{error} <button type="button" onClick={() => void load()} className="ml-2 border-0 bg-transparent font-semibold text-[#ff8a9b] underline cursor-pointer">Retry</button></div>}
      {isCreateOpen && (
        <form onSubmit={createGroup} className="mb-6 grid gap-3 rounded-xl border border-border bg-surface p-4">
          <h2 className="font-heading text-lg font-bold text-text">Create a group</h2>
          <input value={name} onChange={(event) => setName(event.target.value)} required maxLength={120} placeholder="Group name" className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none" />
          <textarea value={description} onChange={(event) => setDescription(event.target.value)} maxLength={2000} placeholder="Description (optional)" className="min-h-20 rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none" />
          <label className="flex items-center gap-2 text-sm text-text"><span>Privacy</span><select value={privacy} onChange={(event) => setPrivacy(event.target.value as 'public' | 'private')} className="rounded-md border border-border bg-surface-2 px-2 py-1 text-text"><option value="public">Public</option><option value="private">Private</option></select></label>
          <div className="flex gap-2"><button disabled={isCreating} className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white cursor-pointer disabled:opacity-60">{isCreating ? 'Creating…' : 'Create group'}</button><button type="button" onClick={() => setIsCreateOpen(false)} className="rounded-lg border border-border bg-transparent px-4 py-2 text-sm text-text cursor-pointer">Cancel</button></div>
        </form>
      )}

      {invites.length > 0 && <section className="mb-6 rounded-xl border border-primary/30 bg-primary/5 p-4"><h2 className="font-heading text-lg font-bold text-text">Group invitations</h2><div className="mt-3 flex flex-col gap-3">{invites.map((invite) => <div key={invite.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-surface p-3 text-sm"><span className="text-text">You have an invitation to group {invite.groupId.slice(0, 8)}.</span><span className="flex gap-2"><button type="button" onClick={() => void respondToInvite(invite, true)} className="rounded-md border-0 bg-primary px-3 py-1.5 text-xs font-semibold text-white cursor-pointer">Accept</button><button type="button" onClick={() => void respondToInvite(invite, false)} className="rounded-md border border-border bg-transparent px-3 py-1.5 text-xs text-text cursor-pointer">Decline</button></span></div>)}</div></section>}

      <section className="mb-8">
        <div className="mb-3 flex items-center justify-between"><h2 className="font-heading text-xl font-bold text-text">My Groups</h2><span className="text-sm text-text-muted">{mine.length}</span></div>
        {isLoading ? <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3"><div className="h-48 animate-pulse rounded-xl bg-surface-2" /><div className="h-48 animate-pulse rounded-xl bg-surface-2" /></div> : mine.length === 0 ? <p className="rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">You have not joined a group yet.</p> : <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{mine.map((group) => <GroupCard key={group.id} group={group} />)}</div>}
        {mineCursor && <button type="button" onClick={() => void loadMoreMine()} className="mt-3 border-0 bg-transparent text-sm font-semibold text-primary cursor-pointer">Load more groups</button>}
      </section>

      <section>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3"><h2 className="font-heading text-xl font-bold text-text">Discover public groups</h2><form onSubmit={search}><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search groups" className="rounded-full border border-border bg-surface px-3 py-2 text-sm text-text outline-none" /></form></div>
        {!isLoading && discover.length === 0 ? <p className="rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">No public groups match this search.</p> : <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{discover.map((group) => <GroupCard key={group.id} group={group} />)}</div>}
        {discoverCursor && <button type="button" onClick={() => void loadMoreDiscover()} className="mt-3 border-0 bg-transparent text-sm font-semibold text-primary cursor-pointer">Load more groups</button>}
      </section>
    </main>
  )
}
