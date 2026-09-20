import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { friendsApi } from '../../api/friends'
import type { Friend, FriendRequest, FriendSuggestion } from '../../api/friends'
import { resolveProfileImageUrl, usersApi } from '../../api/users'
import type { UserProfile } from '../../api/users'
import { usePreferences } from '../../preferences'
import { Mascot } from 'page-mascot'

type FriendsView = 'home' | 'requests' | 'suggestions' | 'all'
type CardPerson = Pick<UserProfile, 'userId' | 'username' | 'displayName' | 'avatarUrl'>

const navigation: Array<{ id: FriendsView; label: string; icon: string }> = [
  { id: 'home', label: 'Trang chủ', icon: '👥' },
  { id: 'requests', label: 'Lời mời kết bạn', icon: '♟' },
  { id: 'suggestions', label: 'Gợi ý', icon: '✦' },
  { id: 'all', label: 'Tất cả bạn bè', icon: '👤' },
]

function Avatar({ person, className = '' }: { person: CardPerson; className?: string }) {
  const initials = person.displayName.slice(0, 2).toUpperCase()

  return person.avatarUrl ? (
    <img src={resolveProfileImageUrl(person.avatarUrl)} alt="" className={`object-cover ${className}`} />
  ) : (
    <span className={`flex items-center justify-center bg-surface-2 font-bold text-text-muted ${className}`} aria-hidden="true">
      {initials}
    </span>
  )
}

function FriendCard({
  person,
  detail,
  primaryLabel,
  secondaryLabel,
  onPrimary,
  onSecondary,
  isUpdating,
}: {
  person: CardPerson
  detail: string
  primaryLabel: string
  secondaryLabel: string
  onPrimary: () => void
  onSecondary: () => void
  isUpdating: boolean
}) {
  return (
    <article className="overflow-hidden rounded-xl border border-border bg-surface shadow-sm">
      <Link to={`/profile/${person.userId}`} className="block aspect-[1.08] bg-surface-2">
        <Avatar person={person} className="h-full w-full" />
      </Link>
      <div className="p-2.5">
        <Link to={`/profile/${person.userId}`} className="block truncate text-[15px] font-bold text-text no-underline hover:underline">
          {person.displayName}
        </Link>
        <p className="mt-1 h-4 truncate text-xs text-text-muted">{detail}</p>
        <div className="mt-2 flex flex-col gap-1.5">
          <button type="button" disabled={isUpdating} onClick={onPrimary} className="h-8 rounded-md border-0 bg-primary px-3 text-[13px] font-semibold text-white hover:bg-primary-hover disabled:opacity-60">
            {isUpdating ? 'Đang xử lý...' : primaryLabel}
          </button>
          <button type="button" disabled={isUpdating} onClick={onSecondary} className="h-8 rounded-md border-0 bg-surface-2 px-3 text-[13px] font-semibold text-text hover:bg-border disabled:opacity-60">
            {secondaryLabel}
          </button>
        </div>
      </div>
    </article>
  )
}

export default function ExplorePage() {
  const { t } = usePreferences()
  const [view, setView] = useState<FriendsView>('home')
  const [requests, setRequests] = useState<FriendRequest[]>([])
  const [suggestions, setSuggestions] = useState<FriendSuggestion[]>([])
  const [friends, setFriends] = useState<Friend[]>([])
  const [profiles, setProfiles] = useState<Record<string, CardPerson>>({})
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [updatingId, setUpdatingId] = useState<string | null>(null)

  useEffect(() => {
    let isCurrent = true

    void Promise.all([
      friendsApi.getIncomingRequests(0, 20),
      friendsApi.getSuggestions(undefined, 36),
      friendsApi.getFriends(0, 60),
    ])
      .then(async ([incoming, suggested, friendPage]) => {
        const userIds = new Set([
          ...incoming.items.map((request) => request.senderUserId),
          ...friendPage.items.map((friend) => friend.userId),
        ])
        const results = await Promise.allSettled([...userIds].map((userId) => usersApi.getById(userId)))
        if (!isCurrent) return

        const nextProfiles: Record<string, CardPerson> = {}
        suggested.items.forEach((suggestion) => { nextProfiles[suggestion.profile.userId] = suggestion.profile })
        results.forEach((result) => {
          if (result.status === 'fulfilled') nextProfiles[result.value.userId] = result.value
        })
        setRequests(incoming.items)
        setSuggestions(suggested.items)
        setFriends(friendPage.items)
        setProfiles(nextProfiles)
      })
      .catch((loadError: unknown) => {
        if (isCurrent) setError(loadError instanceof ApiError ? loadError.message : t('unableLoadFriends'))
      })
      .finally(() => { if (isCurrent) setIsLoading(false) })

    return () => { isCurrent = false }
  }, [t])

  const updateRequest = async (request: FriendRequest, action: 'accept' | 'decline') => {
    setUpdatingId(request.id)
    setError(null)
    try {
      if (action === 'accept') {
        const friend = await friendsApi.acceptRequest(request.id)
        setFriends((current) => current.some((item) => item.userId === friend.userId) ? current : [friend, ...current])
      } else {
        await friendsApi.declineRequest(request.id)
      }
      setRequests((current) => current.filter((item) => item.id !== request.id))
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : t('unableUpdateRelationship'))
    } finally {
      setUpdatingId(null)
    }
  }

  const sendRequest = async (suggestion: FriendSuggestion) => {
    setUpdatingId(suggestion.profile.userId)
    setError(null)
    try {
      await friendsApi.sendRequest(suggestion.profile.userId)
      setSuggestions((current) => current.filter((item) => item.profile.userId !== suggestion.profile.userId))
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : t('unableUpdateRelationship'))
    } finally {
      setUpdatingId(null)
    }
  }

  const showRequests = view === 'home' || view === 'requests'
  const showSuggestions = view === 'home' || view === 'suggestions'
  const friendProfiles = friends.map((friend) => profiles[friend.userId]).filter((profile): profile is CardPerson => Boolean(profile))

  return (
    <div className="flex min-h-[calc(100vh-56px)] bg-bg">
      <aside className="hidden h-[calc(100vh-56px)] w-[308px] shrink-0 border-r border-border bg-surface xl:block">
        <div className="flex items-center justify-between px-6 pb-2 pt-4">
          <div className="flex items-center gap-3">
            <Mascot
              directions="/mascots/astronaut-directions.webp"
              reactions="/mascots/astronaut-reactions.webp"
              size={52}
              label="Explore Astronaut"
            />
            <h1 className="text-2xl font-bold text-text">Bạn bè</h1>
          </div>
          <span className="flex h-8 w-8 items-center justify-center rounded-full bg-surface-2 text-text-muted" aria-label="Cài đặt bạn bè">⚙</span>
        </div>
        <nav className="px-2" aria-label="Danh mục bạn bè">
          {navigation.map((item) => (
            <button key={item.id} type="button" onClick={() => setView(item.id)} className={`flex w-full items-center gap-3 rounded-lg px-4 py-3 text-left text-[15px] font-semibold ${view === item.id ? 'bg-surface-2 text-text' : 'text-text-muted hover:bg-surface-2'}`}>
              <span className="flex h-8 w-8 items-center justify-center rounded-full bg-surface-2 text-sm" aria-hidden="true">{item.icon}</span>
              {item.label}
            </button>
          ))}
          <Link to="/birthdays" className="flex items-center gap-3 rounded-lg px-4 py-3 text-[15px] font-semibold text-text-muted no-underline hover:bg-surface-2">
            <span className="flex h-8 w-8 items-center justify-center rounded-full bg-surface-2" aria-hidden="true">🎁</span>
            Sinh nhật
          </Link>
        </nav>
      </aside>

      <main className="min-w-0 flex-1 px-4 py-6 md:px-7 xl:px-7">
        {error && <div className="mb-4 flex items-center justify-between rounded-lg border border-danger/40 bg-danger/10 px-4 py-3 text-sm text-danger"><span>{error}</span><button type="button" onClick={() => setError(null)} aria-label="Đóng thông báo">×</button></div>}
        <div className="mb-4 flex items-center justify-between xl:hidden">
          <div className="flex items-center gap-3">
            <Mascot
              directions="/mascots/astronaut-directions.webp"
              reactions="/mascots/astronaut-reactions.webp"
              size={48}
              label="Explore Astronaut"
            />
            <h1 className="text-2xl font-bold text-text">Bạn bè</h1>
          </div>
        </div>
        <nav aria-label="Bộ lọc bạn bè" className="mb-4 flex flex-wrap gap-2 xl:hidden">
          {navigation.map((item) => <button key={item.id} type="button" onClick={() => setView(item.id)} aria-pressed={view === item.id} className={`rounded-full px-3 py-2 text-sm font-semibold ${view === item.id ? 'bg-primary text-white' : 'bg-surface text-text hover:bg-surface-2'}`}>{item.label}</button>)}
          <Link to="/birthdays" className="rounded-full bg-surface px-3 py-2 text-sm font-semibold text-text no-underline hover:bg-surface-2">Sinh nhật</Link>
        </nav>
        {isLoading ? <p className="py-10 text-center text-text-muted">Đang tải bạn bè...</p> : <>
          {showRequests && <section>
            <div className="mb-3 flex items-center justify-between gap-4">
              <h2 className="text-xl font-bold text-text">Lời mời kết bạn{requests.length > 0 ? ` (${requests.length})` : ''}</h2>
              {view === 'home' && <button type="button" onClick={() => setView('requests')} className="border-0 bg-transparent text-sm font-semibold text-primary hover:underline">Xem tất cả</button>}
            </div>
            {requests.length > 0 ? <div className="grid auto-cols-[164px] grid-flow-col gap-2 overflow-x-auto pb-5">
              {requests.map((request) => {
                const person = profiles[request.senderUserId]
                return person ? <FriendCard key={request.id} person={person} detail="Có thể bạn quen" primaryLabel="Xác nhận" secondaryLabel="Xóa" onPrimary={() => void updateRequest(request, 'accept')} onSecondary={() => void updateRequest(request, 'decline')} isUpdating={updatingId === request.id} /> : null
              })}
            </div> : <p className="rounded-xl bg-surface px-4 py-6 text-sm text-text-muted">Bạn không có lời mời kết bạn mới.</p>}
          </section>}

          {showSuggestions && <section className={showRequests ? 'mt-5 border-t border-border pt-5' : ''}>
            <div className="mb-3 flex items-center justify-between gap-4">
              <h2 className="text-xl font-bold text-text">Những người bạn có thể biết</h2>
              {view === 'home' && <button type="button" onClick={() => setView('suggestions')} className="border-0 bg-transparent text-sm font-semibold text-primary hover:underline">Xem tất cả</button>}
            </div>
            {suggestions.length > 0 ? <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-4 2xl:grid-cols-6">
              {suggestions.map((suggestion) => <FriendCard key={suggestion.profile.userId} person={suggestion.profile} detail={suggestion.mutualFriendCount > 0 ? `${suggestion.mutualFriendCount} bạn chung` : 'Gợi ý cho bạn'} primaryLabel="Thêm bạn bè" secondaryLabel="Gỡ" onPrimary={() => void sendRequest(suggestion)} onSecondary={() => setSuggestions((current) => current.filter((item) => item.profile.userId !== suggestion.profile.userId))} isUpdating={updatingId === suggestion.profile.userId} />)}
            </div> : <p className="rounded-xl bg-surface px-4 py-6 text-sm text-text-muted">Hiện không có gợi ý kết bạn mới.</p>}
          </section>}

          {view === 'all' && <section>
            <h2 className="mb-3 text-xl font-bold text-text">Tất cả bạn bè ({friends.length})</h2>
            {friendProfiles.length > 0 ? <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4">
              {friendProfiles.map((person) => <Link key={person.userId} to={`/profile/${person.userId}`} className="flex items-center gap-3 rounded-xl bg-surface p-3 text-text no-underline hover:bg-surface-2"><Avatar person={person} className="h-14 w-14 rounded-full" /><span className="truncate font-bold">{person.displayName}</span></Link>)}
            </div> : <p className="rounded-xl bg-surface px-4 py-6 text-sm text-text-muted">Bạn chưa có bạn bè nào.</p>}
          </section>}
        </>}
      </main>
    </div>
  )
}
