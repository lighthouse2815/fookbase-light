import { useCallback, useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { searchApi, type GlobalSearchResponse, type SearchGroup, type SearchHashtag, type SearchPage as SearchPageResult, type SearchPerson, type SearchPost, type SearchReel, type SearchType } from '../../api/search'
import { resolveProfileImageUrl } from '../../api/users'
import { usePreferences } from '../../preferences'
import { publicProfileHandle } from '../../shared/publicProfileHandle'

const tabs: Array<{ type: SearchType; label: string }> = [
  { type: 'all', label: 'Tất cả' },
  { type: 'people', label: 'Mọi người' },
  { type: 'posts', label: 'Bài viết' },
  { type: 'groups', label: 'Nhóm' },
  { type: 'pages', label: 'Trang' },
  { type: 'reels', label: 'Reels' },
]

const emptyResult: GlobalSearchResponse = { people: [], groups: [], pages: [], posts: [], reels: [], hashtags: [], nextCursor: null }

function typeFrom(value: string | null): SearchType {
  return tabs.some((tab) => tab.type === value) ? value as SearchType : 'all'
}

function Avatar({ src, label }: { src: string | null; label: string }) {
  return <div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-xs font-bold text-white">{src ? <img src={resolveProfileImageUrl(src)} alt="" loading="lazy" decoding="async" className="h-full w-full object-cover" /> : label.slice(0, 2).toUpperCase()}</div>
}

function PersonCard({ item }: { item: SearchPerson }) {
  const { t } = usePreferences()
  const username = publicProfileHandle(item.username)
  const relationshipLabel = item.friendshipState === 'friends'
    ? t('friends')
    : item.friendshipState === 'request_sent'
      ? t('requestSent')
      : item.friendshipState === 'request_received'
        ? t('friendRequestReceived')
        : null

  return <Link to={`/profile/${item.userId}`} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2"><Avatar src={item.avatarUrl} label={item.displayName} /><div className="min-w-0"><h3 className="truncate font-semibold text-text">{item.displayName}</h3>{username && <p className="truncate text-sm text-text-muted">@{username}</p>}{item.bio && <p className="mt-1 line-clamp-2 text-sm text-text-muted">{item.bio}</p>}<p className="mt-2 text-xs text-text-muted">{item.followerCount} {t('followers')} · {item.followingCount} {t('followingCount')}{relationshipLabel ? ` · ${relationshipLabel}` : ''}{item.isFollowing ? ` · ${t('following')}` : ''}{item.isFollowedBy ? ` · ${t('followsYou')}` : ''}</p></div></Link>
}

function GroupCard({ item }: { item: SearchGroup }) {
  return <Link to={`/groups/${item.groupId}`} className="overflow-hidden rounded-xl border border-border bg-surface no-underline transition-colors hover:bg-surface-2">{item.coverUrl ? <img src={resolveProfileImageUrl(item.coverUrl)} alt="" loading="lazy" decoding="async" className="h-24 w-full object-cover" /> : <div className="h-24 bg-primary/15" />}<div className="p-4"><h3 className="truncate font-semibold text-text">{item.name}</h3><p className="mt-1 text-xs text-text-muted">{item.privacy} · {item.memberCount} thành viên{item.viewerMembershipState ? ` · ${item.viewerMembershipState}` : ''}</p>{item.description && <p className="mt-2 line-clamp-2 text-sm text-text-muted">{item.description}</p>}</div></Link>
}

function PageCard({ item }: { item: SearchPageResult }) {
  return <Link to={`/pages/${item.username}`} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2"><Avatar src={item.avatarUrl} label={item.name} /><div className="min-w-0"><h3 className="truncate font-semibold text-text">{item.name}</h3><p className="truncate text-sm text-text-muted">@{item.username} · {item.category}</p><p className="mt-1 text-xs text-text-muted">{item.followerCount} người theo dõi{item.viewerIsFollowing ? ' · Đang theo dõi' : ''}</p>{item.bio && <p className="mt-1 line-clamp-2 text-sm text-text-muted">{item.bio}</p>}</div></Link>
}

function PostCard({ item }: { item: SearchPost }) {
  const author = item.displayAuthor?.type === 'page' ? item.displayAuthor.name : item.authorUserId ? 'Xem hồ sơ' : 'Bài viết'
  const destination = item.containerType === 'group' ? `/groups/${item.containerId}` : item.displayAuthor?.type === 'page' ? `/pages/${item.displayAuthor.username}` : item.authorUserId ? `/profile/${item.authorUserId}` : '/feed'
  return <Link to={destination} className="block rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2"><div className="flex items-center justify-between gap-3 text-xs text-text-muted"><span className="font-semibold text-text">{author}</span><time>{new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(item.createdAtUtc))}</time></div><p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-text">{item.snippet || `${item.mediaIds.length} tệp đính kèm`}</p><p className="mt-2 text-xs text-text-muted">{item.commentCount} bình luận · {Object.values(item.reactionCounts).reduce((sum, count) => sum + count, 0)} cảm xúc · {item.containerType}</p></Link>
}

function ReelCard({ item }: { item: SearchReel }) {
  const username = publicProfileHandle(item.author.username)
  return <Link to={`/reels?reel=${item.reelId}`} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2"><div className="grid h-16 w-12 shrink-0 place-items-center rounded-lg bg-black text-xl">🎞️</div><div className="min-w-0"><h3 className="truncate font-semibold text-text">{item.author.displayName} {username && <span className="font-normal text-text-muted">@{username}</span>}</h3><p className="mt-1 line-clamp-2 text-sm text-text-muted">{item.snippet || 'Reel'}</p><p className="mt-2 text-xs text-text-muted">{item.viewCount} lượt xem · {item.reactionCount} cảm xúc · {item.commentCount} bình luận</p></div></Link>
}

function HashtagCard({ item }: { item: SearchHashtag }) {
  return <Link to={`/hashtag/${encodeURIComponent(item.tag)}`} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2"><div className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-primary/15 text-lg text-primary">#</div><div className="min-w-0"><h3 className="truncate font-semibold text-text">#{item.displayName}</h3><p className="mt-1 text-sm text-text-muted">Xem bài viết theo hashtag</p></div></Link>
}

function ResultSection({ title, seeAll, children }: { title: string; seeAll?: string; children: React.ReactNode }) {
  return <section><div className="mb-3 flex items-center justify-between"><h2 className="font-heading text-xl font-bold text-text">{title}</h2>{seeAll && <Link to={seeAll} className="text-sm font-semibold text-primary no-underline hover:underline">Xem tất cả</Link>}</div><div className="grid gap-3 sm:grid-cols-2">{children}</div></section>
}

export default function SearchPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const query = searchParams.get('q')?.trim() ?? ''
  const type = typeFrom(searchParams.get('type'))
  const [result, setResult] = useState<GlobalSearchResponse>(emptyResult)
  const [isLoading, setIsLoading] = useState(false)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (cursor?: string, append = false, signal?: AbortSignal) => {
    if (query.length < 2) {
      setResult(emptyResult)
      setError(null)
      return
    }
    if (append) setIsLoadingMore(true)
    else setIsLoading(true)
    setError(null)
    try {
      const next = await searchApi.search(query, type, cursor, 20, signal ? { signal } : undefined)
      setResult((current) => append ? {
        people: [...current.people, ...next.people.filter((item) => !current.people.some((existing) => existing.userId === item.userId))],
        groups: [...current.groups, ...next.groups.filter((item) => !current.groups.some((existing) => existing.groupId === item.groupId))],
        pages: [...current.pages, ...next.pages.filter((item) => !current.pages.some((existing) => existing.pageId === item.pageId))],
        posts: [...current.posts, ...next.posts.filter((item) => !current.posts.some((existing) => existing.postId === item.postId))],
        reels: [...current.reels, ...next.reels.filter((item) => !current.reels.some((existing) => existing.reelId === item.reelId))],
        hashtags: [...(current.hashtags ?? []), ...(next.hashtags ?? []).filter((item) => !(current.hashtags ?? []).some((existing) => existing.tag === item.tag))],
        nextCursor: next.nextCursor,
      } : next)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tìm kiếm lúc này.')
    } finally {
      if (append) setIsLoadingMore(false)
      else setIsLoading(false)
    }
  }, [query, type])

  useEffect(() => {
    const controller = new AbortController()
    const timeoutId = window.setTimeout(() => { void load(undefined, false, controller.signal) }, 0)
    return () => { controller.abort(); window.clearTimeout(timeoutId) }
  }, [load])

  const chooseTab = (next: SearchType) => {
    const params = new URLSearchParams()
    if (query) params.set('q', query)
    if (next !== 'all') params.set('type', next)
    setSearchParams(params)
  }

  const allEmpty = result.people.length + result.groups.length + result.pages.length + result.posts.length + result.reels.length + (result.hashtags?.length ?? 0) === 0
  const tabsList = <nav className="flex gap-2 overflow-x-auto border-b border-border pb-2" aria-label="Danh mục tìm kiếm">{tabs.map((tab) => <button key={tab.type} type="button" onClick={() => chooseTab(tab.type)} className={['rounded-full px-4 py-2 text-sm font-semibold transition-colors', type === tab.type ? 'bg-primary text-white' : 'bg-surface-2 text-text-muted hover:bg-surface-3'].join(' ')}>{tab.label}</button>)}</nav>

  return <main className="mx-auto min-h-screen w-full max-w-5xl px-3 py-5 sm:px-5"><h1 className="font-heading text-3xl font-bold text-text">Tìm kiếm</h1><p className="mt-1 text-sm text-text-muted">{query ? `Kết quả cho “${query}”` : 'Tìm người, nhóm, Trang, bài viết, Reels và hashtag.'}</p><div className="mt-5">{tabsList}</div>{query.length < 2 ? <p className="mt-6 rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">Nhập ít nhất 2 ký tự để tìm kiếm.</p> : <div className="mt-5 space-y-7">{error && <div className="rounded-xl border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-4 text-sm text-[#ff8a9b]"><p>{error}</p><button type="button" onClick={() => void load()} className="mt-2 rounded-md border border-[#ff8a9b]/50 bg-transparent px-3 py-1 text-xs font-semibold text-[#ff8a9b]">Thử lại</button></div>}{isLoading && <div className="grid gap-3 sm:grid-cols-2"><div className="h-28 animate-pulse rounded-xl bg-surface-2" /><div className="h-28 animate-pulse rounded-xl bg-surface-2" /></div>}{!isLoading && !error && allEmpty && <p className="rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">Không có kết quả cho “{query}”.</p>}{!isLoading && !error && type === 'all' && <><>{result.hashtags && result.hashtags.length > 0 && <ResultSection title="Hashtag">{result.hashtags.map((item) => <HashtagCard key={item.tag} item={item} />)}</ResultSection>}</>{result.people.length > 0 && <ResultSection title="Mọi người" seeAll={`/search?q=${encodeURIComponent(query)}&type=people`}>{result.people.map((item) => <PersonCard key={item.userId} item={item} />)}</ResultSection>}{result.groups.length > 0 && <ResultSection title="Nhóm" seeAll={`/search?q=${encodeURIComponent(query)}&type=groups`}>{result.groups.map((item) => <GroupCard key={item.groupId} item={item} />)}</ResultSection>}{result.pages.length > 0 && <ResultSection title="Trang" seeAll={`/search?q=${encodeURIComponent(query)}&type=pages`}>{result.pages.map((item) => <PageCard key={item.pageId} item={item} />)}</ResultSection>}{result.posts.length > 0 && <ResultSection title="Bài viết" seeAll={`/search?q=${encodeURIComponent(query)}&type=posts`}>{result.posts.map((item) => <PostCard key={item.postId} item={item} />)}</ResultSection>}{result.reels.length > 0 && <ResultSection title="Reels" seeAll={`/search?q=${encodeURIComponent(query)}&type=reels`}>{result.reels.map((item) => <ReelCard key={item.reelId} item={item} />)}</ResultSection>}</>}{!isLoading && !error && type === 'people' && <ResultSection title="Mọi người">{result.people.map((item) => <PersonCard key={item.userId} item={item} />)}</ResultSection>}{!isLoading && !error && type === 'groups' && <ResultSection title="Nhóm">{result.groups.map((item) => <GroupCard key={item.groupId} item={item} />)}</ResultSection>}{!isLoading && !error && type === 'pages' && <ResultSection title="Trang">{result.pages.map((item) => <PageCard key={item.pageId} item={item} />)}</ResultSection>}{!isLoading && !error && type === 'posts' && <ResultSection title="Bài viết">{result.posts.map((item) => <PostCard key={item.postId} item={item} />)}</ResultSection>}{!isLoading && !error && type === 'reels' && <ResultSection title="Reels">{result.reels.map((item) => <ReelCard key={item.reelId} item={item} />)}</ResultSection>}{type !== 'all' && result.nextCursor && <button type="button" disabled={isLoadingMore} onClick={() => void load(result.nextCursor ?? undefined, true)} className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">{isLoadingMore ? 'Đang tải…' : 'Xem thêm'}</button>}</div>}</main>
}
