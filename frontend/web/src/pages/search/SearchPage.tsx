import { useInfiniteQuery } from '@tanstack/react-query'
import { useAuth } from '../../auth/useAuth'
import { Link, useSearchParams } from 'react-router-dom'
import { searchApi, type GlobalSearchResponse, type SearchEvent, type SearchGroup, type SearchHashtag, type SearchPage as SearchPageResult, type SearchPerson, type SearchPost, type SearchReel, type SearchType } from '../../api/search'
import { resolveProfileImageUrl } from '../../api/users'
import { usePreferences } from '../../preferences'
import { publicProfileHandle } from '../../shared/publicProfileHandle'
import HighlightedText from './HighlightedText'
import { getSearchDestination, mergeSearchResults } from './searchPresentation'

const tabs: Array<{ type: SearchType; label: string }> = [
  { type: 'all', label: 'Tất cả' },
  { type: 'people', label: 'Mọi người' },
  { type: 'posts', label: 'Bài viết' },
  { type: 'groups', label: 'Nhóm' },
  { type: 'pages', label: 'Trang' },
  { type: 'reels', label: 'Reels' },
  { type: 'events', label: 'Sự kiện' },
]

const emptyResult: GlobalSearchResponse = { people: [], groups: [], pages: [], posts: [], reels: [], hashtags: [], events: [], nextCursor: null }

function typeFrom(value: string | null): SearchType {
  return tabs.some((tab) => tab.type === value) ? value as SearchType : 'all'
}

function Avatar({ src, label }: { src: string | null; label: string }) {
  return <div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-xs font-bold text-white">{src ? <img src={resolveProfileImageUrl(src)} alt="" loading="lazy" decoding="async" className="h-full w-full object-cover" /> : label.slice(0, 2).toUpperCase()}</div>
}

function PersonCard({ item, query }: { item: SearchPerson; query: string }) {
  const { t } = usePreferences()
  const username = publicProfileHandle(item.username)
  const relationshipLabel = item.friendshipState === 'friends'
    ? t('friends')
    : item.friendshipState === 'request_sent'
      ? t('requestSent')
      : item.friendshipState === 'request_received'
        ? t('friendRequestReceived')
        : null

  return <Link to={getSearchDestination('people', item.userId)} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"><Avatar src={item.avatarUrl} label={item.displayName} /><div className="min-w-0"><h3 className="truncate font-semibold text-text"><HighlightedText text={item.displayName} query={query} /></h3>{username && <p className="truncate text-sm text-text-muted">@{username}</p>}{item.bio && <p className="mt-1 line-clamp-2 text-sm text-text-muted"><HighlightedText text={item.bio} query={query} /></p>}<p className="mt-2 text-xs text-text-muted">{item.followerCount} {t('followers')} · {item.followingCount} {t('followingCount')}{relationshipLabel ? ` · ${relationshipLabel}` : ''}{item.isFollowing ? ` · ${t('following')}` : ''}{item.isFollowedBy ? ` · ${t('followsYou')}` : ''}</p></div></Link>
}

function GroupCard({ item, query }: { item: SearchGroup; query: string }) {
  return <Link to={getSearchDestination('groups', item.groupId)} className="overflow-hidden rounded-xl border border-border bg-surface no-underline transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">{item.coverUrl ? <img src={resolveProfileImageUrl(item.coverUrl)} alt="" loading="lazy" decoding="async" className="h-24 w-full object-cover" /> : <div className="h-24 bg-primary/15" />}<div className="p-4"><h3 className="truncate font-semibold text-text"><HighlightedText text={item.name} query={query} /></h3><p className="mt-1 text-xs text-text-muted">{item.privacy === 'private' ? 'Nhóm riêng tư' : 'Nhóm công khai'} · {item.memberCount} thành viên{item.viewerMembershipState ? ` · ${item.viewerMembershipState === 'member' ? 'Đã tham gia' : 'Đang chờ duyệt'}` : ''}</p>{item.description && <p className="mt-2 line-clamp-2 text-sm text-text-muted"><HighlightedText text={item.description} query={query} /></p>}</div></Link>
}

function PageCard({ item, query }: { item: SearchPageResult; query: string }) {
  return <Link to={getSearchDestination('pages', item.username)} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"><Avatar src={item.avatarUrl} label={item.name} /><div className="min-w-0"><h3 className="truncate font-semibold text-text"><HighlightedText text={item.name} query={query} /></h3><p className="truncate text-sm text-text-muted">@{item.username} · {item.category}</p><p className="mt-1 text-xs text-text-muted">{item.followerCount} người theo dõi{item.viewerIsFollowing ? ' · Đang theo dõi' : ''}</p>{item.bio && <p className="mt-1 line-clamp-2 text-sm text-text-muted"><HighlightedText text={item.bio} query={query} /></p>}</div></Link>
}

function PostCard({ item, query }: { item: SearchPost; query: string }) {
  const author = item.displayAuthor?.name ?? (item.authorUserId ? 'Người dùng' : 'Bài viết')
  const destination = getSearchDestination('posts', item.postId)
  return <Link to={destination} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"><Avatar src={item.displayAuthor?.avatarUrl ?? null} label={author} /><div className="min-w-0 flex-1"><div className="flex items-center justify-between gap-3 text-xs text-text-muted"><span className="truncate font-semibold text-text"><HighlightedText text={author} query={query} /></span><time className="shrink-0">{new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(item.createdAtUtc))}</time></div><p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-text"><HighlightedText text={item.snippet || `${item.mediaIds.length} tệp đính kèm`} query={query} /></p><p className="mt-2 text-xs text-text-muted">{item.commentCount} bình luận · {Object.values(item.reactionCounts).reduce((sum, count) => sum + count, 0)} cảm xúc · {item.containerType}</p></div></Link>
}

function ReelCard({ item, query }: { item: SearchReel; query: string }) {
  const username = publicProfileHandle(item.author.username)
  return <Link to={getSearchDestination('reels', item.reelId)} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"><div className="grid h-16 w-12 shrink-0 place-items-center rounded-lg bg-black text-xl">🎞️</div><div className="min-w-0"><h3 className="truncate font-semibold text-text"><HighlightedText text={item.author.displayName} query={query} /> {username && <span className="font-normal text-text-muted">@{username}</span>}</h3><p className="mt-1 line-clamp-2 text-sm text-text-muted"><HighlightedText text={item.snippet || 'Reel'} query={query} /></p><p className="mt-2 text-xs text-text-muted">{item.viewCount} lượt xem · {item.reactionCount} cảm xúc · {item.commentCount} bình luận</p></div></Link>
}

function HashtagCard({ item, query }: { item: SearchHashtag; query: string }) {
  return <Link to={`/hashtag/${encodeURIComponent(item.tag)}`} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"><div className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-primary/15 text-lg text-primary">#</div><div className="min-w-0"><h3 className="truncate font-semibold text-text">#<HighlightedText text={item.displayName} query={query} /></h3><p className="mt-1 text-sm text-text-muted">Xem bài viết theo hashtag</p></div></Link>
}

function ResultSection({ title, seeAll, children }: { title: string; seeAll?: string; children: React.ReactNode }) {
  return <section><div className="mb-3 flex items-center justify-between"><h2 className="font-heading text-xl font-bold text-text">{title}</h2>{seeAll && <Link to={seeAll} className="text-sm font-semibold text-primary no-underline hover:underline">Xem tất cả</Link>}</div><div className="grid gap-3 sm:grid-cols-2">{children}</div></section>
}

function EventCard({ item, query }: { item: SearchEvent; query: string }) {
  return <Link to={getSearchDestination('events', item.eventId)} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary">
    {item.coverUrl ? <img src={resolveProfileImageUrl(item.coverUrl)} alt="" loading="lazy" decoding="async" className="h-16 w-16 shrink-0 rounded-xl object-cover" /> : <span aria-hidden="true" className="grid h-16 w-16 shrink-0 place-items-center rounded-xl bg-primary/15 text-2xl">📅</span>}
    <div className="min-w-0"><h3 className="font-semibold text-text"><HighlightedText text={item.name} query={query} /></h3>
      <time dateTime={item.startsAtUtc} className="mt-1 block text-sm text-text-muted">{new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(item.startsAtUtc))}</time>
      {item.locationName && <p className="mt-1 text-sm text-text-muted"><HighlightedText text={item.locationName} query={query} /></p>}
      {item.locationType === 'online' && !item.locationName && <p className="mt-1 text-sm text-text-muted">Trực tuyến</p>}
      <p className="mt-1 text-xs text-text-muted"><HighlightedText text={item.hostName} query={query} /> · {item.goingCount} sẽ tham gia · {item.interestedCount} quan tâm</p>
    </div>
  </Link>
}

function ResultSkeleton({ type }: { type: SearchType }) {
  return <div role="status" aria-label="Đang tải kết quả tìm kiếm" className="grid gap-3 sm:grid-cols-2"><span className="sr-only">Đang tải kết quả tìm kiếm…</span>{Array.from({ length: 4 }, (_, index) => <div key={index} aria-hidden="true" className="rounded-xl border border-border bg-surface p-4 motion-safe:animate-pulse">
    {type === 'groups' && <div className="mb-3 h-20 rounded-lg bg-surface-2" />}
    <div className="flex gap-3"><span className={`${type === 'reels' ? 'h-16 w-12 rounded-lg' : type === 'events' ? 'h-16 w-16 rounded-xl' : 'h-11 w-11 rounded-full'} shrink-0 bg-surface-2`} /><div className="flex-1 space-y-2"><div className="h-3 w-3/4 rounded bg-surface-2" /><div className="h-2.5 w-1/2 rounded bg-surface-2" /><div className="h-2.5 w-2/3 rounded bg-surface-2" /></div></div>
    {type === 'posts' && <div className="mt-4 space-y-2"><div className="h-3 rounded bg-surface-2" /><div className="h-3 w-4/5 rounded bg-surface-2" /></div>}
  </div>)}</div>
}

export default function SearchPage() {
  const { session } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const query = searchParams.get('q')?.trim() ?? ''
  const type = typeFrom(searchParams.get('type'))
  const eligible = query.length >= 2 && query.length <= 100
  const resultsQuery = useInfiniteQuery({
    queryKey: ['search', session?.user.id, query, type],
    enabled: eligible && Boolean(session),
    initialPageParam: undefined as string | undefined,
    queryFn: async ({ pageParam, signal }) => {
      // Keep the existing deferred dispatch so StrictMode can cancel its trial mount.
      await new Promise((resolve) => setTimeout(resolve, 0))
      signal.throwIfAborted()
      return searchApi.search(query, type, pageParam, 20, { signal })
    },
    getNextPageParam: (page) => page.nextCursor ?? undefined,
  })
  const result = resultsQuery.data?.pages.reduce(mergeSearchResults, emptyResult) ?? emptyResult
  const isLoading = eligible && resultsQuery.isPending
  const hasError = resultsQuery.isError && !resultsQuery.data

  const chooseTab = (next: SearchType) => {
    const params = new URLSearchParams()
    if (query) params.set('q', query)
    if (next !== 'all') params.set('type', next)
    setSearchParams(params)
  }
  const categories = [
    { type: 'people', title: 'Mọi người', cards: result.people.map((item) => <PersonCard key={item.userId} item={item} query={query} />) },
    { type: 'groups', title: 'Nhóm', cards: result.groups.map((item) => <GroupCard key={item.groupId} item={item} query={query} />) },
    { type: 'pages', title: 'Trang', cards: result.pages.map((item) => <PageCard key={item.pageId} item={item} query={query} />) },
    { type: 'posts', title: 'Bài viết', cards: result.posts.map((item) => <PostCard key={item.postId} item={item} query={query} />) },
    { type: 'reels', title: 'Reels', cards: result.reels.map((item) => <ReelCard key={item.reelId} item={item} query={query} />) },
    { type: 'events', title: 'Sự kiện', cards: (result.events ?? []).map((item) => <EventCard key={item.eventId} item={item} query={query} />) },
  ]
  const allEmpty = categories.every((category) => category.cards.length === 0) && (result.hashtags?.length ?? 0) === 0
  const emptyLabel: Record<SearchType, string> = { all: 'kết quả', people: 'người dùng', posts: 'bài viết', groups: 'nhóm', pages: 'Trang', reels: 'Reels', events: 'sự kiện' }

  return <main className="mx-auto min-h-screen w-full max-w-5xl px-3 py-5 [overflow-wrap:anywhere] sm:px-5">
    <h1 className="font-heading text-3xl font-bold text-text">Tìm kiếm</h1>
    <p className="mt-1 break-words text-sm text-text-muted">{query ? `Kết quả tìm kiếm cho “${query}”` : 'Tìm người, nhóm, Trang, bài viết, Reels, sự kiện và hashtag.'}</p>
    <nav className="mt-5 flex flex-wrap gap-2 border-b border-border pb-3" aria-label="Danh mục tìm kiếm">{tabs.map((tab) => <button key={tab.type} type="button" aria-current={type === tab.type ? 'page' : undefined} onClick={() => chooseTab(tab.type)} className={`min-h-11 rounded-full border-0 px-4 py-2 text-sm font-semibold focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary motion-safe:transition-colors ${type === tab.type ? 'bg-primary text-white' : 'bg-surface-2 text-text-muted hover:bg-surface-3'}`}>{tab.label}</button>)}</nav>
    {!eligible ? <p className="mt-6 rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">{query.length > 100 ? 'Từ khóa tìm kiếm không được quá 100 ký tự.' : 'Nhập ít nhất 2 ký tự để tìm kiếm.'}</p> : <div className="mt-5 space-y-7">
      {isLoading && <ResultSkeleton type={type} />}
      {!isLoading && hasError && <div role="alert" className="rounded-xl border border-border bg-surface p-5"><p className="font-semibold text-text">Không thể tải kết quả tìm kiếm</p><p className="mt-1 text-sm text-text-muted">Hãy thử lại sau ít phút.</p><button type="button" onClick={() => void resultsQuery.refetch()} className="mt-3 min-h-11 rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white focus-visible:ring-2 focus-visible:ring-primary">Thử lại</button></div>}
      {!isLoading && !hasError && allEmpty && <div role="status" className="rounded-2xl border border-border bg-surface px-5 py-7 text-center"><span aria-hidden="true" className="mx-auto mb-3 grid h-12 w-12 place-items-center rounded-full bg-surface-2 text-3xl text-text-muted">⌕</span><h2 className="break-words text-lg font-semibold text-text">Không tìm thấy {emptyLabel[type]} cho “{query}”</h2><p className="mt-2 text-sm leading-6 text-text-muted">Kiểm tra chính tả, dùng từ khóa ngắn hơn hoặc tìm loại nội dung khác.</p></div>}
      {!isLoading && !hasError && !allEmpty && <>
        {type === 'all' && (result.hashtags?.length ?? 0) > 0 && <ResultSection title="Hashtag">{result.hashtags?.map((item) => <HashtagCard key={item.tag} item={item} query={query} />)}</ResultSection>}
        {categories.filter((category) => category.cards.length > 0 && (type === 'all' || type === category.type)).map((category) => <ResultSection key={category.type} title={category.title} seeAll={type === 'all' ? `/search?q=${encodeURIComponent(query)}&type=${category.type}` : undefined}>{category.cards}</ResultSection>)}
        {type !== 'all' && result.nextCursor && <div>{resultsQuery.isFetchNextPageError && <p role="status" className="mb-2 text-sm text-text-muted">Không thể tải thêm kết quả. Danh sách hiện tại được giữ lại.</p>}<button type="button" disabled={resultsQuery.isFetching} onClick={() => { if (!resultsQuery.isFetching) void resultsQuery.fetchNextPage() }} className="min-h-11 rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white focus-visible:ring-2 focus-visible:ring-primary disabled:opacity-50">{resultsQuery.isFetchingNextPage ? 'Đang tải…' : resultsQuery.isFetchNextPageError ? 'Thử lại tải thêm' : 'Xem thêm'}</button></div>}
      </>}
    </div>}
  </main>
}
