import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { groupsApi } from '../../api/groups'
import type { Group, GroupFeedItem, GroupInvite } from '../../api/groups'
import type { Post } from '../../api/posts'
import { resolveProfileImageUrl } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import LivePostCard from '../feed/components/LivePostCard'

type GroupView = 'feed' | 'discover' | 'mine'

function GroupGlyph({ className = 'h-5 w-5' }: { className?: string }) {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className={className} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="8" r="3" /><path d="M5 21c.5-3.8 2.75-6 7-6s6.5 2.2 7 6M3.5 10.5a2.5 2.5 0 1 1 2.75-4.4M20.5 10.5a2.5 2.5 0 1 0-2.75-4.4" /></svg>
}

function FeedGlyph({ className = 'h-5 w-5' }: { className?: string }) {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className={className} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="3" width="18" height="18" rx="3" /><path d="M7 8h10M7 12h10M7 16h6" /></svg>
}

function CompassGlyph({ className = 'h-5 w-5' }: { className?: string }) {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className={className} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="9" /><path d="m15.5 8.5-2.1 4.8-4.8 2.1 2.1-4.8 4.8-2.1Z" /></svg>
}

function SearchGlyph() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round"><circle cx="10.8" cy="10.8" r="6.3" /><path d="m16 16 4.2 4.2" /></svg>
}

function MoreGlyph() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5" fill="currentColor"><circle cx="5" cy="12" r="1.6" /><circle cx="12" cy="12" r="1.6" /><circle cx="19" cy="12" r="1.6" /></svg>
}

function GroupCover({ group, className }: { group: Group; className: string }) {
  if (group.coverUrl) return <img src={resolveProfileImageUrl(group.coverUrl)} alt="" className={className + ' object-cover'} />

  return <div className={className + ' flex items-center justify-center bg-gradient-to-br from-primary/60 via-[#6d4cbf] to-[#e15f5f] text-white'}><GroupGlyph className="h-10 w-10" /></div>
}

function memberLabel(count: number) {
  return new Intl.NumberFormat('vi-VN', { notation: 'compact', maximumFractionDigits: 1 }).format(count) + ' thành viên'
}

function GroupCard({ group, onJoin, isJoining, isRequested }: {
  group: Group
  onJoin?: (group: Group) => void
  isJoining?: boolean
  isRequested?: boolean
}) {
  const isMember = group.viewerRole !== null

  return (
    <article className="group overflow-hidden rounded-xl border border-border bg-surface shadow-sm transition-colors hover:border-text-light/60">
      <Link to={`/groups/${group.id}`} className="block no-underline">
        <GroupCover group={group} className="h-32 w-full sm:h-36" />
        <div className="min-h-25 p-3">
          <h3 className="line-clamp-2 text-[15px] font-bold leading-5 text-text group-hover:underline">{group.name}</h3>
          <p className="mt-1 text-xs text-text-muted">{memberLabel(group.memberCount)} · {group.privacy === 'public' ? 'Công khai' : 'Riêng tư'}</p>
          {group.description && <p className="mt-1 line-clamp-1 text-xs text-text-light">{group.description}</p>}
        </div>
      </Link>
      <div className="px-3 pb-3">
        {isMember
          ? <Link to={`/groups/${group.id}`} className="flex h-8 items-center justify-center rounded-md bg-surface-2 text-xs font-bold text-text no-underline transition-colors hover:bg-surface-hover">Xem nhóm</Link>
          : <button type="button" disabled={isJoining || isRequested} onClick={() => onJoin?.(group)} className="flex h-8 w-full items-center justify-center rounded-md border-0 bg-primary/25 text-xs font-bold text-primary-light transition-colors hover:bg-primary hover:text-white disabled:cursor-not-allowed disabled:opacity-70 cursor-pointer">{isRequested ? 'Đã gửi yêu cầu' : isJoining ? 'Đang tham gia…' : group.privacy === 'private' ? 'Yêu cầu tham gia' : 'Tham gia nhóm'}</button>}
      </div>
    </article>
  )
}

function GroupRow({ group }: { group: Group }) {
  return <Link to={`/groups/${group.id}`} className="flex items-center gap-2.5 rounded-lg px-2 py-2 no-underline transition-colors hover:bg-surface-2">
    <GroupCover group={group} className="h-9 w-9 shrink-0 rounded-lg" />
    <span className="min-w-0"><span className="block line-clamp-1 text-[13px] font-semibold leading-4 text-text">{group.name}</span><span className="block text-[11px] text-text-light">{memberLabel(group.memberCount)}</span></span>
  </Link>
}

export default function GroupsPage() {
  const { session } = useAuth()
  const [activeView, setActiveView] = useState<GroupView>('feed')
  const [mine, setMine] = useState<Group[]>([])
  const [discover, setDiscover] = useState<Group[]>([])
  const [mineCursor, setMineCursor] = useState<string | null>(null)
  const [discoverCursor, setDiscoverCursor] = useState<string | null>(null)
  const [invites, setInvites] = useState<GroupInvite[]>([])
  const [feed, setFeed] = useState<GroupFeedItem[]>([])
  const [query, setQuery] = useState('')
  const [sidebarQuery, setSidebarQuery] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingFeed, setIsLoadingFeed] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [privacy, setPrivacy] = useState<'public' | 'private'>('public')
  const [isCreating, setIsCreating] = useState(false)
  const [joiningGroupId, setJoiningGroupId] = useState<string | null>(null)
  const [requestedGroupIds, setRequestedGroupIds] = useState<ReadonlySet<string>>(() => new Set())

  const loadGroupFeed = useCallback(async () => {
    setIsLoadingFeed(true)
    try {
      const page = await groupsApi.getFeed()
      setFeed(page.items)
    } finally {
      setIsLoadingFeed(false)
    }
  }, [])

  const load = useCallback(async (nextQuery = '') => {
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
      await loadGroupFeed()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải danh sách nhóm.')
    } finally {
      setIsLoading(false)
    }
  }, [loadGroupFeed])

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
      setActiveView('mine')
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tạo nhóm.')
    } finally {
      setIsCreating(false)
    }
  }

  const search = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setActiveView('discover')
    void load(query)
  }

  const joinGroup = async (group: Group) => {
    setJoiningGroupId(group.id)
    setError(null)
    try {
      const request = await groupsApi.join(group.id)
      if (request) setRequestedGroupIds((current) => new Set([...current, group.id]))
      else await load(query)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tham gia nhóm.')
    } finally {
      setJoiningGroupId(null)
    }
  }

  const loadMoreMine = async () => {
    if (!mineCursor) return
    try {
      const page = await groupsApi.getMine(mineCursor)
      setMine((current) => [...current, ...page.items.filter((item) => !current.some((group) => group.id === item.id))])
      setMineCursor(page.nextCursor)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải thêm nhóm.')
    }
  }

  const loadMoreDiscover = async () => {
    if (!discoverCursor) return
    try {
      const page = await groupsApi.discover(query, discoverCursor)
      setDiscover((current) => [...current, ...page.items.filter((item) => !current.some((group) => group.id === item.id))])
      setDiscoverCursor(page.nextCursor)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải thêm nhóm.')
    }
  }

  const respondToInvite = async (invite: GroupInvite, accept: boolean) => {
    try {
      if (accept) {
        await groupsApi.acceptInvite(invite.groupId, invite.id)
        setActiveView('mine')
        await load(query)
      } else {
        await groupsApi.declineInvite(invite.groupId, invite.id)
        setInvites((current) => current.filter((item) => item.id !== invite.id))
      }
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật lời mời.')
    }
  }

  const visibleMine = useMemo(() => {
    const normalized = sidebarQuery.trim().toLocaleLowerCase('vi-VN')
    return normalized ? mine.filter((group) => group.name.toLocaleLowerCase('vi-VN').includes(normalized)) : mine
  }, [mine, sidebarQuery])

  const updateFeedPost = (post: Post) => setFeed((current) => current.map((item) => item.post.id === post.id ? { ...item, post } : item))

  return (
    <div className="min-h-screen bg-bg">
      <aside className="fixed left-0 top-14 z-30 hidden h-[calc(100vh-56px)] w-75 flex-col border-r border-border bg-surface px-3 py-3 lg:flex">
        <div className="mb-3 flex items-center justify-between px-1"><h1 className="font-heading text-xl font-extrabold text-text">Nhóm</h1><button type="button" className="grid h-8 w-8 place-items-center rounded-full border-0 bg-surface-2 text-text-muted cursor-pointer" aria-label="Cài đặt nhóm">⚙</button></div>
        <form onSubmit={search} className="relative mb-3"><span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-light"><SearchGlyph /></span><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Tìm kiếm nhóm" className="h-9 w-full rounded-full border-0 bg-surface-2 py-1 pl-9 pr-3 text-[13px] text-text outline-none placeholder:text-text-light focus:ring-2 focus:ring-primary/60" /></form>
        <nav className="flex flex-col gap-1" aria-label="Điều hướng Nhóm">
          <button type="button" onClick={() => setActiveView('feed')} className={'flex h-11 items-center gap-3 rounded-lg border-0 px-3 text-left text-sm font-bold cursor-pointer ' + (activeView === 'feed' ? 'bg-bg text-text' : 'bg-transparent text-text-muted hover:bg-surface-2')}><span className={'grid h-7 w-7 place-items-center rounded-full ' + (activeView === 'feed' ? 'bg-primary text-white' : 'bg-surface-2 text-text-muted')}><FeedGlyph className="h-4 w-4" /></span>Bảng feed của bạn</button>
          <button type="button" onClick={() => setActiveView('discover')} className={'flex h-11 items-center gap-3 rounded-lg border-0 px-3 text-left text-sm font-bold cursor-pointer ' + (activeView === 'discover' ? 'bg-bg text-text' : 'bg-transparent text-text-muted hover:bg-surface-2')}><span className={'grid h-7 w-7 place-items-center rounded-full ' + (activeView === 'discover' ? 'bg-primary text-white' : 'bg-surface-2 text-text-muted')}><CompassGlyph className="h-4 w-4" /></span>Khám phá</button>
          <button type="button" onClick={() => setActiveView('mine')} className={'flex h-11 items-center gap-3 rounded-lg border-0 px-3 text-left text-sm font-bold cursor-pointer ' + (activeView === 'mine' ? 'bg-bg text-text' : 'bg-transparent text-text-muted hover:bg-surface-2')}><span className={'grid h-7 w-7 place-items-center rounded-full ' + (activeView === 'mine' ? 'bg-primary text-white' : 'bg-surface-2 text-text-muted')}><GroupGlyph className="h-4 w-4" /></span>Nhóm của bạn</button>
        </nav>
        <button type="button" onClick={() => setIsCreateOpen(true)} className="mt-2 h-9 rounded-md border-0 bg-primary/25 text-[13px] font-bold text-primary-light transition-colors hover:bg-primary hover:text-white cursor-pointer">＋ Tạo nhóm mới</button>
        <div className="my-3 border-t border-border" />
        <div className="mb-2 flex items-center justify-between px-1"><h2 className="text-sm font-bold text-text-muted">Nhóm bạn đã tham gia</h2><button type="button" onClick={() => setActiveView('mine')} className="border-0 bg-transparent text-xs font-semibold text-primary-light cursor-pointer">Xem tất cả</button></div>
        <div className="relative mb-2"><span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-light"><SearchGlyph /></span><input value={sidebarQuery} onChange={(event) => setSidebarQuery(event.target.value)} placeholder="Lọc nhóm của bạn" className="h-8 w-full rounded-md border-0 bg-surface-2 py-1 pl-9 pr-3 text-xs text-text outline-none placeholder:text-text-light" /></div>
        <div className="min-h-0 flex-1 overflow-y-auto"><div className="flex flex-col gap-0.5">{visibleMine.slice(0, 16).map((group) => <GroupRow key={group.id} group={group} />)}{!isLoading && visibleMine.length === 0 && <p className="px-2 py-3 text-xs text-text-light">Chưa có nhóm phù hợp.</p>}</div></div>
      </aside>

      <main className="min-h-screen lg:ml-75">
        <div className="mx-auto w-full max-w-[1560px] px-3 py-4 sm:px-5 lg:px-7">
          <div className="mb-4 flex gap-2 overflow-x-auto lg:hidden"><button type="button" onClick={() => setActiveView('feed')} className={'whitespace-nowrap rounded-full px-3 py-1.5 text-sm font-semibold ' + (activeView === 'feed' ? 'bg-primary text-white' : 'bg-surface text-text-muted')}>Bảng feed</button><button type="button" onClick={() => setActiveView('discover')} className={'whitespace-nowrap rounded-full px-3 py-1.5 text-sm font-semibold ' + (activeView === 'discover' ? 'bg-primary text-white' : 'bg-surface text-text-muted')}>Khám phá</button><button type="button" onClick={() => setActiveView('mine')} className={'whitespace-nowrap rounded-full px-3 py-1.5 text-sm font-semibold ' + (activeView === 'mine' ? 'bg-primary text-white' : 'bg-surface text-text-muted')}>Nhóm của bạn</button><button type="button" onClick={() => setIsCreateOpen(true)} className="whitespace-nowrap rounded-full bg-primary/20 px-3 py-1.5 text-sm font-semibold text-primary-light">Tạo nhóm</button></div>

          {error && <div className="mb-4 flex items-center justify-between gap-3 rounded-lg border border-danger/40 bg-danger/10 px-4 py-3 text-sm text-[#ff9aaa]"><span>{error}</span><button type="button" onClick={() => void load(query)} className="border-0 bg-transparent text-xs font-bold text-inherit underline cursor-pointer">Thử lại</button></div>}

          {activeView === 'feed' && <section className="mx-auto max-w-[680px]"><div className="mb-3 px-1"><h2 className="text-base font-bold text-text">Hoạt động mới đây</h2><p className="text-xs text-text-light">Bài viết mới từ các nhóm bạn đã tham gia.</p></div>{isLoadingFeed || isLoading ? <div className="flex flex-col gap-4"><div className="h-64 animate-pulse rounded-xl bg-surface" /><div className="h-52 animate-pulse rounded-xl bg-surface" /></div> : feed.length > 0 ? <div className="flex flex-col gap-4">{feed.map(({ group, post }) => <LivePostCard key={post.id} post={post} group={{ id: group.id, name: group.name }} currentUserId={session!.user.id} onPostUpdated={updateFeedPost} onPostDeleted={(postId) => setFeed((current) => current.filter((item) => item.post.id !== postId))} />)}</div> : <div className="rounded-xl border border-border bg-surface p-8 text-center"><span className="mx-auto grid h-12 w-12 place-items-center rounded-full bg-primary/15 text-primary-light"><FeedGlyph className="h-6 w-6" /></span><h3 className="mt-3 font-heading text-lg font-bold text-text">Chưa có hoạt động mới</h3><p className="mx-auto mt-1 max-w-sm text-sm text-text-muted">Khám phá thêm nhóm để cập nhật những cuộc trò chuyện mới nhất trên bảng feed của bạn.</p><button type="button" onClick={() => setActiveView('discover')} className="mt-4 rounded-md border-0 bg-primary px-4 py-2 text-sm font-bold text-white cursor-pointer">Khám phá nhóm</button></div>}</section>}

          {activeView === 'discover' && <section><div className="mb-4 flex items-end justify-between gap-4"><div><h2 className="font-heading text-xl font-extrabold text-text">Gợi ý cho bạn</h2><p className="text-sm text-text-muted">Nhóm mà bạn có thể quan tâm.</p></div>{query && <button type="button" onClick={() => { setQuery(''); void load() }} className="border-0 bg-transparent text-sm font-semibold text-primary-light cursor-pointer">Xóa tìm kiếm</button>}</div>{isLoading ? <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4 2xl:grid-cols-5">{Array.from({ length: 5 }, (_, index) => <div key={index} className="h-65 animate-pulse rounded-xl bg-surface" />)}</div> : discover.length > 0 ? <><div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4 2xl:grid-cols-5">{discover.map((group) => <GroupCard key={group.id} group={group} onJoin={joinGroup} isJoining={joiningGroupId === group.id} isRequested={requestedGroupIds.has(group.id)} />)}</div>{discoverCursor && <button type="button" onClick={() => void loadMoreDiscover()} className="mt-5 rounded-md border-0 bg-surface-2 px-4 py-2 text-sm font-bold text-primary-light cursor-pointer">Xem thêm nhóm</button>}</> : <div className="rounded-xl border border-border bg-surface p-8 text-center text-sm text-text-muted">Không tìm thấy nhóm công khai phù hợp.</div>}</section>}

          {activeView === 'mine' && <section><div className="mb-4 flex items-end justify-between gap-3"><div><h2 className="font-heading text-xl font-extrabold text-text">Nhóm của bạn</h2><p className="text-sm text-text-muted">{mine.length} nhóm bạn đã tham gia hoặc quản lý.</p></div><button type="button" onClick={() => setIsCreateOpen(true)} className="hidden rounded-md border-0 bg-primary px-4 py-2 text-sm font-bold text-white sm:block cursor-pointer">Tạo nhóm mới</button></div>{invites.length > 0 && <section className="mb-6"><div className="mb-3 flex items-center justify-between"><h3 className="font-heading text-base font-bold text-text">Lời mời tham gia nhóm đang chờ ({invites.length})</h3></div><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">{invites.map((invite) => <article key={invite.id} className="flex items-center gap-3 rounded-xl bg-surface p-3">{invite.group ? <GroupCover group={invite.group} className="h-12 w-12 shrink-0 rounded-lg" /> : <span className="grid h-12 w-12 shrink-0 place-items-center rounded-lg bg-primary/15 text-primary-light"><GroupGlyph className="h-6 w-6" /></span>}<div className="min-w-0 flex-1"><p className="truncate text-sm font-bold text-text">{invite.group?.name ?? 'Lời mời tham gia nhóm'}</p><p className="truncate text-xs text-text-light">{invite.group ? memberLabel(invite.group.memberCount) : 'Được gửi gần đây'}</p><div className="mt-2 flex gap-2"><button type="button" onClick={() => void respondToInvite(invite, true)} className="h-8 flex-1 rounded-md border-0 bg-primary px-2 text-xs font-bold text-white cursor-pointer">Chấp nhận</button><button type="button" onClick={() => void respondToInvite(invite, false)} className="grid h-8 w-8 place-items-center rounded-md border-0 bg-surface-2 text-text-muted cursor-pointer" aria-label="Từ chối lời mời"><MoreGlyph /></button></div></div></article>)}</div></section>}{isLoading ? <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4"><div className="h-64 animate-pulse rounded-xl bg-surface" /><div className="h-64 animate-pulse rounded-xl bg-surface" /></div> : mine.length > 0 ? <><div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4 2xl:grid-cols-5">{mine.map((group) => <GroupCard key={group.id} group={group} />)}</div>{mineCursor && <button type="button" onClick={() => void loadMoreMine()} className="mt-5 rounded-md border-0 bg-surface-2 px-4 py-2 text-sm font-bold text-primary-light cursor-pointer">Xem thêm nhóm</button>}</> : <div className="rounded-xl border border-border bg-surface p-8 text-center"><h3 className="font-heading text-lg font-bold text-text">Bạn chưa tham gia nhóm nào</h3><button type="button" onClick={() => setActiveView('discover')} className="mt-3 border-0 bg-transparent text-sm font-bold text-primary-light cursor-pointer">Khám phá nhóm</button></div>}</section>}
        </div>
      </main>

      {isCreateOpen && <div className="fixed inset-0 z-50 grid place-items-center bg-black/60 p-4" role="presentation"><form onSubmit={createGroup} className="w-full max-w-md rounded-xl bg-surface p-5 shadow-2xl" role="dialog" aria-modal="true" aria-labelledby="create-group-title"><div className="mb-4 flex items-center justify-between"><h2 id="create-group-title" className="font-heading text-xl font-extrabold text-text">Tạo nhóm</h2><button type="button" onClick={() => setIsCreateOpen(false)} className="grid h-8 w-8 place-items-center rounded-full border-0 bg-surface-2 text-lg text-text-muted cursor-pointer" aria-label="Đóng">×</button></div><label className="mb-3 block text-sm font-semibold text-text">Tên nhóm<input value={name} onChange={(event) => setName(event.target.value)} required maxLength={120} placeholder="Đặt tên cho nhóm của bạn" className="mt-1.5 h-10 w-full rounded-md border border-border bg-bg px-3 text-sm font-normal text-text outline-none focus:border-primary" /></label><label className="mb-3 block text-sm font-semibold text-text">Mô tả <span className="font-normal text-text-light">(không bắt buộc)</span><textarea value={description} onChange={(event) => setDescription(event.target.value)} maxLength={2000} placeholder="Nhóm này dành cho ai?" className="mt-1.5 min-h-22 w-full rounded-md border border-border bg-bg px-3 py-2 text-sm font-normal text-text outline-none focus:border-primary" /></label><label className="mb-5 block text-sm font-semibold text-text">Quyền riêng tư<select value={privacy} onChange={(event) => setPrivacy(event.target.value as 'public' | 'private')} className="mt-1.5 h-10 w-full rounded-md border border-border bg-bg px-3 text-sm font-normal text-text outline-none focus:border-primary"><option value="public">Công khai</option><option value="private">Riêng tư</option></select></label><div className="flex gap-2"><button type="button" onClick={() => setIsCreateOpen(false)} className="h-10 flex-1 rounded-md border-0 bg-surface-2 text-sm font-bold text-text cursor-pointer">Hủy</button><button disabled={isCreating} className="h-10 flex-1 rounded-md border-0 bg-primary text-sm font-bold text-white cursor-pointer disabled:cursor-not-allowed disabled:opacity-60">{isCreating ? 'Đang tạo…' : 'Tạo nhóm'}</button></div></form></div>}
    </div>
  )
}
