import { useCallback, useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { searchApi, type GlobalSearchResponse, type SearchGroup, type SearchHashtag, type SearchPage as SearchPageResult, type SearchPerson, type SearchPost, type SearchReel, type SearchType } from '../../api/search'
import { resolveProfileImageUrl } from '../../api/users'

const tabs: Array<{ type: SearchType; label: string }> = [
  { type: 'all', label: 'All' },
  { type: 'people', label: 'People' },
  { type: 'posts', label: 'Posts' },
  { type: 'groups', label: 'Groups' },
  { type: 'pages', label: 'Pages' },
  { type: 'reels', label: 'Reels' },
]

const emptyResult: GlobalSearchResponse = { people: [], groups: [], pages: [], posts: [], reels: [], hashtags: [], nextCursor: null }

function typeFrom(value: string | null): SearchType {
  return tabs.some((tab) => tab.type === value) ? value as SearchType : 'all'
}

function Avatar({ src, label }: { src: string | null; label: string }) {
  return <div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-xs font-bold text-white">{src ? <img src={resolveProfileImageUrl(src)} alt="" className="h-full w-full object-cover" /> : label.slice(0, 2).toUpperCase()}</div>
}

function PersonCard({ item }: { item: SearchPerson }) {
  return <Link to={`/profile/${item.userId}`} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2"><Avatar src={item.avatarUrl} label={item.displayName} /><div className="min-w-0"><h3 className="truncate font-semibold text-text">{item.displayName}</h3><p className="truncate text-sm text-text-muted">@{item.username}</p>{item.bio && <p className="mt-1 line-clamp-2 text-sm text-text-muted">{item.bio}</p>}</div></Link>
}

function GroupCard({ item }: { item: SearchGroup }) {
  return <Link to={`/groups/${item.groupId}`} className="overflow-hidden rounded-xl border border-border bg-surface no-underline transition-colors hover:bg-surface-2">{item.coverUrl ? <img src={resolveProfileImageUrl(item.coverUrl)} alt="" className="h-24 w-full object-cover" /> : <div className="h-24 bg-primary/15" />}<div className="p-4"><h3 className="truncate font-semibold text-text">{item.name}</h3><p className="mt-1 text-xs text-text-muted">{item.privacy} · {item.memberCount} members{item.viewerMembershipState ? ` · ${item.viewerMembershipState}` : ''}</p>{item.description && <p className="mt-2 line-clamp-2 text-sm text-text-muted">{item.description}</p>}</div></Link>
}

function PageCard({ item }: { item: SearchPageResult }) {
  return <Link to={`/pages/${item.username}`} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2"><Avatar src={item.avatarUrl} label={item.name} /><div className="min-w-0"><h3 className="truncate font-semibold text-text">{item.name}</h3><p className="truncate text-sm text-text-muted">@{item.username} · {item.category}</p><p className="mt-1 text-xs text-text-muted">{item.followerCount} followers{item.viewerIsFollowing ? ' · Following' : ''}</p>{item.bio && <p className="mt-1 line-clamp-2 text-sm text-text-muted">{item.bio}</p>}</div></Link>
}

function PostCard({ item }: { item: SearchPost }) {
  const author = item.displayAuthor?.type === 'page' ? item.displayAuthor.name : item.authorUserId ? 'View profile' : 'Post'
  const destination = item.containerType === 'group' ? `/groups/${item.containerId}` : item.displayAuthor?.type === 'page' ? `/pages/${item.displayAuthor.username}` : item.authorUserId ? `/profile/${item.authorUserId}` : '/feed'
  return <Link to={destination} className="block rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2"><div className="flex items-center justify-between gap-3 text-xs text-text-muted"><span className="font-semibold text-text">{author}</span><time>{new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(item.createdAtUtc))}</time></div><p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-text">{item.snippet || `${item.mediaIds.length} attachment${item.mediaIds.length === 1 ? '' : 's'}`}</p><p className="mt-2 text-xs text-text-muted">{item.commentCount} comments · {Object.values(item.reactionCounts).reduce((sum, count) => sum + count, 0)} reactions · {item.containerType}</p></Link>
}

function ReelCard({ item }: { item: SearchReel }) {
  return <Link to={`/reels?reel=${item.reelId}`} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2"><div className="grid h-16 w-12 shrink-0 place-items-center rounded-lg bg-black text-xl">🎞️</div><div className="min-w-0"><h3 className="truncate font-semibold text-text">{item.author.displayName} <span className="font-normal text-text-muted">@{item.author.username}</span></h3><p className="mt-1 line-clamp-2 text-sm text-text-muted">{item.snippet || 'Reel'}</p><p className="mt-2 text-xs text-text-muted">{item.viewCount} views · {item.reactionCount} reactions · {item.commentCount} comments</p></div></Link>
}

function HashtagCard({ item }: { item: SearchHashtag }) {
  return <Link to={`/hashtag/${encodeURIComponent(item.tag)}`} className="flex gap-3 rounded-xl border border-border bg-surface p-4 no-underline transition-colors hover:bg-surface-2"><div className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-primary/15 text-lg text-primary">#</div><div className="min-w-0"><h3 className="truncate font-semibold text-text">#{item.displayName}</h3><p className="mt-1 text-sm text-text-muted">Xem bài viết theo hashtag</p></div></Link>
}

function ResultSection({ title, seeAll, children }: { title: string; seeAll?: string; children: React.ReactNode }) {
  return <section><div className="mb-3 flex items-center justify-between"><h2 className="font-heading text-xl font-bold text-text">{title}</h2>{seeAll && <Link to={seeAll} className="text-sm font-semibold text-primary no-underline hover:underline">See all</Link>}</div><div className="grid gap-3 sm:grid-cols-2">{children}</div></section>
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
      setError(requestError instanceof ApiError ? requestError.message : 'Unable to search right now.')
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
  const tabsList = <nav className="flex gap-2 overflow-x-auto border-b border-border pb-2" aria-label="Search categories">{tabs.map((tab) => <button key={tab.type} type="button" onClick={() => chooseTab(tab.type)} className={['rounded-full px-4 py-2 text-sm font-semibold transition-colors', type === tab.type ? 'bg-primary text-white' : 'bg-surface-2 text-text-muted hover:bg-surface-3'].join(' ')}>{tab.label}</button>)}</nav>

  return <main className="mx-auto min-h-screen w-full max-w-5xl px-3 py-5 sm:px-5"><h1 className="font-heading text-3xl font-bold text-text">Search</h1><p className="mt-1 text-sm text-text-muted">{query ? `Results for “${query}”` : 'Find people, groups, Pages, posts, Reels, and hashtags.'}</p><div className="mt-5">{tabsList}</div>{query.length < 2 ? <p className="mt-6 rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">Enter at least 2 characters to search.</p> : <div className="mt-5 space-y-7">{error && <div className="rounded-xl border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-4 text-sm text-[#ff8a9b]"><p>{error}</p><button type="button" onClick={() => void load()} className="mt-2 rounded-md border border-[#ff8a9b]/50 bg-transparent px-3 py-1 text-xs font-semibold text-[#ff8a9b]">Retry</button></div>}{isLoading && <div className="grid gap-3 sm:grid-cols-2"><div className="h-28 animate-pulse rounded-xl bg-surface-2" /><div className="h-28 animate-pulse rounded-xl bg-surface-2" /></div>}{!isLoading && !error && allEmpty && <p className="rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">No results match “{query}”.</p>}{!isLoading && !error && type === 'all' && <><>{result.hashtags && result.hashtags.length > 0 && <ResultSection title="Hashtags">{result.hashtags.map((item) => <HashtagCard key={item.tag} item={item} />)}</ResultSection>}</>{result.people.length > 0 && <ResultSection title="People" seeAll={`/search?q=${encodeURIComponent(query)}&type=people`}>{result.people.map((item) => <PersonCard key={item.userId} item={item} />)}</ResultSection>}{result.groups.length > 0 && <ResultSection title="Groups" seeAll={`/search?q=${encodeURIComponent(query)}&type=groups`}>{result.groups.map((item) => <GroupCard key={item.groupId} item={item} />)}</ResultSection>}{result.pages.length > 0 && <ResultSection title="Pages" seeAll={`/search?q=${encodeURIComponent(query)}&type=pages`}>{result.pages.map((item) => <PageCard key={item.pageId} item={item} />)}</ResultSection>}{result.posts.length > 0 && <ResultSection title="Posts" seeAll={`/search?q=${encodeURIComponent(query)}&type=posts`}>{result.posts.map((item) => <PostCard key={item.postId} item={item} />)}</ResultSection>}{result.reels.length > 0 && <ResultSection title="Reels" seeAll={`/search?q=${encodeURIComponent(query)}&type=reels`}>{result.reels.map((item) => <ReelCard key={item.reelId} item={item} />)}</ResultSection>}</>}{!isLoading && !error && type === 'people' && <ResultSection title="People">{result.people.map((item) => <PersonCard key={item.userId} item={item} />)}</ResultSection>}{!isLoading && !error && type === 'groups' && <ResultSection title="Groups">{result.groups.map((item) => <GroupCard key={item.groupId} item={item} />)}</ResultSection>}{!isLoading && !error && type === 'pages' && <ResultSection title="Pages">{result.pages.map((item) => <PageCard key={item.pageId} item={item} />)}</ResultSection>}{!isLoading && !error && type === 'posts' && <ResultSection title="Posts">{result.posts.map((item) => <PostCard key={item.postId} item={item} />)}</ResultSection>}{!isLoading && !error && type === 'reels' && <ResultSection title="Reels">{result.reels.map((item) => <ReelCard key={item.reelId} item={item} />)}</ResultSection>}{type !== 'all' && result.nextCursor && <button type="button" disabled={isLoadingMore} onClick={() => void load(result.nextCursor ?? undefined, true)} className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">{isLoadingMore ? 'Loading…' : 'Load more'}</button>}</div>}</main>
}
