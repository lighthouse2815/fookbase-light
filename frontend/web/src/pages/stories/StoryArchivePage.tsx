import { useCallback, useEffect, useMemo, useState } from 'react'
import { ApiError } from '../../api/client'
import { storiesApi, type Story, type StoryTrayAuthor } from '../../api/stories'
import StoryViewer from '../feed/components/StoryViewer'

export default function StoryArchivePage() {
  const [stories, setStories] = useState<Story[]>([])
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null)

  const load = useCallback(async (cursor?: string) => {
    if (cursor) setIsLoadingMore(true)
    else { setIsLoading(true); setError(null) }
    try {
      const page = await storiesApi.archive(cursor)
      setStories((current) => cursor
        ? [...current, ...page.items.filter((story) => !current.some((item) => item.id === story.id))]
        : page.items)
      setNextCursor(page.nextCursor)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải kho lưu trữ Story.')
    } finally {
      if (cursor) setIsLoadingMore(false)
      else setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => { void load() }, 0)
    return () => window.clearTimeout(timeoutId)
  }, [load])
  const groups = useMemo<StoryTrayAuthor[]>(() => stories.length === 0 ? [] : [{
    author: stories[0].author,
    hasUnseenStories: false,
    stories: [...stories].reverse(),
  }], [stories])
  const updateStory = (updated: Story) => setStories((current) => current.map((story) => story.id === updated.id ? updated : story))

  return <main className="mx-auto min-h-screen w-full max-w-4xl px-3 py-6 sm:px-6">
    <div className="mb-5 flex items-center justify-between"><div><h1 className="font-heading text-2xl font-bold text-text">Kho lưu trữ Story</h1><p className="text-sm text-text-muted">Chỉ bạn có thể xem các Story đã hết hạn.</p></div></div>
    {error && <div className="mb-4 rounded-lg bg-danger/15 p-3 text-sm text-danger">{error} <button type="button" className="underline" onClick={() => void load()}>Thử lại</button></div>}
    {isLoading && <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">{Array.from({ length: 6 }, (_, index) => <div key={index} className="aspect-[9/14] animate-pulse rounded-xl bg-surface-2" />)}</div>}
    {!isLoading && stories.length === 0 && !error && <div className="rounded-xl border border-border bg-surface p-10 text-center text-sm text-text-muted">Chưa có Story đã hết hạn.</div>}
    <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">{stories.map((story, index) => <button key={story.id} type="button" onClick={() => setSelectedIndex(index)} className="group relative aspect-[9/14] overflow-hidden rounded-xl border border-border bg-surface-2 text-left"><ArchivePreview story={story} /><span className="absolute inset-x-0 bottom-0 bg-linear-to-t from-black/90 to-transparent px-3 pb-3 pt-12 text-xs text-white"><span className="line-clamp-2">{story.caption ?? 'Story'}</span><span className="mt-1 block text-white/65">{new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(new Date(story.createdAtUtc))}</span></span></button>)}</div>
    {nextCursor && <div className="mt-5 text-center"><button type="button" disabled={isLoadingMore} onClick={() => void load(nextCursor)} className="rounded-lg border border-border bg-surface px-4 py-2 text-sm font-semibold text-text disabled:opacity-50">{isLoadingMore ? 'Đang tải…' : 'Tải thêm'}</button></div>}
    {selectedIndex !== null && groups[0]?.stories.length > 0 && <StoryViewer groups={groups} initialAuthorIndex={0} initialStoryIndex={groups[0].stories.length - 1 - selectedIndex} onClose={() => setSelectedIndex(null)} onStoriesChanged={updateStory} />}
  </main>
}

function ArchivePreview({ story }: { story: Story }) {
  const [url, setUrl] = useState<string | null>(null)
  useEffect(() => { let alive = true; void storiesApi.mediaAccess(story).then((access) => { if (alive) setUrl(access.url) }).catch(() => undefined); return () => { alive = false } }, [story])
  if (!url) return <div className="grid h-full place-items-center text-sm text-text-muted">Đang tải…</div>
  return story.media.mediaType === 'video'
    ? <video src={url} muted className="h-full w-full object-cover" />
    : <img src={url} className="h-full w-full object-cover transition-transform group-hover:scale-105" alt="" />
}
