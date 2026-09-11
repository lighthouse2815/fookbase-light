import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ApiError } from '../../../api/client'
import { storiesApi, type Story, type StoryReactionType, type StoryTrayAuthor, type StoryViewer } from '../../../api/stories'

const imageDurationMs = 5_000
const reactionChoices: ReadonlyArray<[StoryReactionType, string]> = [
  ['like', '👍'], ['love', '❤️'], ['haha', '😆'], ['wow', '😮'], ['sad', '😢'], ['angry', '😡'],
]

interface StoryViewerProps {
  groups: StoryTrayAuthor[]
  initialAuthorIndex: number
  initialStoryIndex: number
  onClose: () => void
  onStoriesChanged: (story: Story) => void
}

export default function StoryViewer({
  groups,
  initialAuthorIndex,
  initialStoryIndex,
  onClose,
  onStoriesChanged,
}: StoryViewerProps) {
  const [authorIndex, setAuthorIndex] = useState(initialAuthorIndex)
  const [storyIndex, setStoryIndex] = useState(initialStoryIndex)
  const [mediaUrl, setMediaUrl] = useState<string | null>(null)
  const [posterUrl, setPosterUrl] = useState<string | null>(null)
  const [mediaError, setMediaError] = useState<string | null>(null)
  const [progress, setProgress] = useState(0)
  const [isPaused, setIsPaused] = useState(false)
  const [reply, setReply] = useState('')
  const [isSendingReply, setIsSendingReply] = useState(false)
  const [replyStatus, setReplyStatus] = useState<string | null>(null)
  const [viewers, setViewers] = useState<StoryViewer[]>([])
  const [viewerCursor, setViewerCursor] = useState<string | null>(null)
  const [isViewerListOpen, setIsViewerListOpen] = useState(false)
  const [isLoadingViewers, setIsLoadingViewers] = useState(false)
  const videoRef = useRef<HTMLVideoElement>(null)
  const touchStart = useRef<number | null>(null)

  const activeGroup = groups[authorIndex]
  const active = activeGroup?.stories[storyIndex]
  const activeKey = active?.id
  const isVideo = active?.media.mediaType === 'video'

  const goTo = useCallback((nextAuthorIndex: number, nextStoryIndex: number) => {
    if (!groups[nextAuthorIndex]?.stories[nextStoryIndex]) return
    setAuthorIndex(nextAuthorIndex)
    setStoryIndex(nextStoryIndex)
    setIsPaused(false)
    setReplyStatus(null)
    setIsViewerListOpen(false)
  }, [groups])

  const previous = useCallback(() => {
    if (storyIndex > 0) {
      goTo(authorIndex, storyIndex - 1)
      return
    }
    if (authorIndex > 0) {
      goTo(authorIndex - 1, groups[authorIndex - 1].stories.length - 1)
    }
  }, [authorIndex, goTo, groups, storyIndex])

  const next = useCallback(() => {
    if (storyIndex < activeGroup.stories.length - 1) {
      goTo(authorIndex, storyIndex + 1)
      return
    }
    if (authorIndex < groups.length - 1) {
      goTo(authorIndex + 1, 0)
      return
    }
    onClose()
  }, [activeGroup, authorIndex, goTo, groups.length, onClose, storyIndex])

  useEffect(() => {
    if (!active) return
    let alive = true
    const timeoutId = window.setTimeout(() => {
      setMediaUrl(null)
      setPosterUrl(null)
      setMediaError(null)
      setProgress(0)
      void Promise.all([storiesApi.mediaAccess(active), storiesApi.posterAccess(active)])
        .then(([media, poster]) => {
          if (!alive) return
          setMediaUrl(media.url)
          setPosterUrl(poster?.url ?? null)
        })
        .catch(() => {
          if (alive) setMediaError('Không thể tải Story này.')
        })
    }, 0)
    if (!active.canManage && !active.isViewed) {
      void storiesApi.markViewed(active.id).then(() => {
        if (alive) onStoriesChanged({ ...active, isViewed: true })
      }).catch(() => undefined)
    }
    return () => { alive = false; window.clearTimeout(timeoutId) }
  }, [active, activeKey, onStoriesChanged])

  useEffect(() => {
    if (!active || isVideo || isPaused || !mediaUrl) return
    const startedAt = Date.now()
    const intervalId = window.setInterval(() => {
      const elapsed = Date.now() - startedAt
      setProgress(Math.min(100, elapsed / imageDurationMs * 100))
      if (elapsed >= imageDurationMs) next()
    }, 80)
    return () => window.clearInterval(intervalId)
  }, [active, activeKey, isPaused, isVideo, mediaUrl, next])

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
      if (event.key === 'ArrowLeft') previous()
      if (event.key === 'ArrowRight') next()
      if (event.key === ' ') {
        event.preventDefault()
        setIsPaused((current) => !current)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [next, onClose, previous])

  const viewersTitle = useMemo(() => active?.viewerCount === 1 ? '1 lượt xem' : `${active?.viewerCount ?? 0} lượt xem`, [active?.viewerCount])

  if (!active) return null

  const setReaction = async (type: StoryReactionType) => {
    try {
      const updated = active.viewerReaction === type
        ? await storiesApi.removeReaction(active.id).then(() => ({ ...active, viewerReaction: null, reactionCount: Math.max(0, active.reactionCount - 1) }))
        : await storiesApi.setReaction(active.id, type)
      onStoriesChanged(updated)
    } catch {
      setReplyStatus('Không thể cập nhật phản ứng.')
    }
  }

  const sendReply = async () => {
    const content = reply.trim()
    if (!content || isSendingReply || active.canManage) return
    setIsSendingReply(true)
    setReplyStatus(null)
    try {
      await storiesApi.reply(active.id, content)
      setReply('')
      setReplyStatus('Đã gửi vào Messenger.')
    } catch (error) {
      setReplyStatus(error instanceof ApiError ? error.message : 'Không thể gửi phản hồi.')
    } finally {
      setIsSendingReply(false)
    }
  }

  const loadViewers = async (cursor?: string) => {
    if (!active.canManage || isLoadingViewers) return
    setIsLoadingViewers(true)
    try {
      const page = await storiesApi.viewers(active.id, cursor)
      setViewers((current) => cursor
        ? [...current, ...page.items.filter((item) => !current.some((viewer) => viewer.userId === item.userId))]
        : page.items)
      setViewerCursor(page.nextCursor)
      setIsViewerListOpen(true)
    } finally {
      setIsLoadingViewers(false)
    }
  }

  return <div className="fixed inset-0 z-[100] grid bg-black/90 text-white" role="dialog" aria-modal="true" aria-label="Trình xem Story">
    <div className="relative mx-auto flex h-full w-full max-w-2xl items-center justify-center sm:px-4">
      <div className="relative h-full w-full overflow-hidden bg-black sm:h-[min(92vh,860px)] sm:rounded-2xl">
        <div className="absolute inset-x-3 top-3 z-20 flex gap-1" aria-label="Tiến trình Story">
          {activeGroup.stories.map((story, index) => <span key={story.id} className="h-1 flex-1 overflow-hidden rounded-full bg-white/35">
            <span className="block h-full bg-white transition-[width]" style={{ width: index < storyIndex ? '100%' : index === storyIndex ? `${progress}%` : '0%' }} />
          </span>)}
        </div>
        <div className="absolute inset-x-4 top-7 z-20 flex items-center gap-3">
          {active.author.avatarUrl ? <img src={active.author.avatarUrl} className="h-9 w-9 rounded-full object-cover" alt="" /> : <span className="grid h-9 w-9 place-items-center rounded-full bg-primary text-xs font-bold">{active.author.displayName.slice(0, 2).toUpperCase()}</span>}
          <div className="min-w-0"><p className="truncate text-sm font-semibold">{active.author.displayName} <span className="font-normal text-white/70">@{active.author.username}</span></p><p className="text-xs text-white/70">{new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(new Date(active.createdAtUtc))}</p></div>
          <button type="button" className="ml-auto grid h-9 w-9 place-items-center rounded-full border-0 bg-black/35 text-xl text-white" onClick={onClose} aria-label="Đóng Story">×</button>
        </div>

        <div className="absolute inset-0 grid place-items-center" onTouchStart={(event) => { touchStart.current = event.touches[0]?.clientX ?? null }} onTouchEnd={(event) => { const start = touchStart.current; const end = event.changedTouches[0]?.clientX; touchStart.current = null; if (start === null || end === undefined || Math.abs(end - start) < 40) return; if (end > start) previous(); else next() }}>
          {mediaError && <p className="rounded-lg bg-black/70 px-4 py-3 text-sm">{mediaError}</p>}
          {!mediaError && !mediaUrl && <p className="text-sm text-white/80">Đang tải Story…</p>}
          {mediaUrl && isVideo && <video ref={videoRef} src={mediaUrl} poster={posterUrl ?? undefined} autoPlay={!isPaused} playsInline className="h-full w-full object-contain" onPlay={() => setIsPaused(false)} onPause={() => setIsPaused(true)} onEnded={next} onTimeUpdate={(event) => { const video = event.currentTarget; if (video.duration > 0) setProgress(Math.min(100, video.currentTime / video.duration * 100)) }} />}
          {mediaUrl && !isVideo && <img src={mediaUrl} className="h-full w-full object-contain" alt={active.caption ?? 'Story'} />}
        </div>

        <button type="button" onClick={previous} className="absolute inset-y-16 left-0 z-10 w-[35%] border-0 bg-transparent" aria-label="Story trước" />
        <button type="button" onClick={next} className="absolute inset-y-16 right-0 z-10 w-[35%] border-0 bg-transparent" aria-label="Story tiếp theo" />
        <button type="button" onClick={() => setIsPaused((current) => !current)} className="absolute right-4 top-20 z-20 rounded-full border-0 bg-black/35 px-3 py-1 text-xs text-white">{isPaused ? 'Tiếp tục' : 'Tạm dừng'}</button>

        <div className="absolute inset-x-0 bottom-0 z-20 bg-linear-to-t from-black/90 via-black/45 to-transparent px-4 pb-5 pt-24">
          {active.caption && <p className="mb-3 whitespace-pre-wrap text-sm leading-relaxed">{active.caption}</p>}
          <div className="flex items-center gap-2">
            {!active.canManage && <div className="flex rounded-full bg-white/15 p-1">{reactionChoices.map(([type, icon]) => <button key={type} type="button" onClick={() => void setReaction(type)} className={`grid h-8 w-8 place-items-center rounded-full border-0 text-base ${active.viewerReaction === type ? 'bg-white/30' : 'bg-transparent'}`} aria-label={type}>{icon}</button>)}</div>}
            {active.reactionCount > 0 && <span className="rounded-full bg-white/15 px-3 py-2 text-xs">{active.reactionCount} phản ứng</span>}
            {active.canManage && <button type="button" onClick={() => void loadViewers()} className="ml-auto rounded-full border-0 bg-white/15 px-3 py-2 text-xs text-white">{viewersTitle}</button>}
          </div>
          {!active.canManage && <div className="mt-3 flex gap-2 rounded-full bg-white/15 p-1 pl-4"><input value={reply} maxLength={5000} onChange={(event) => setReply(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') void sendReply() }} placeholder="Trả lời qua Messenger…" className="min-w-0 flex-1 border-0 bg-transparent text-sm text-white outline-none placeholder:text-white/65" /><button type="button" disabled={!reply.trim() || isSendingReply} onClick={() => void sendReply()} className="rounded-full border-0 bg-primary px-4 py-2 text-xs font-semibold text-white disabled:opacity-50">Gửi</button></div>}
          {replyStatus && <p className="mt-2 text-xs text-white/80">{replyStatus}</p>}
        </div>
      </div>

      {isViewerListOpen && <aside className="absolute inset-x-3 bottom-3 z-30 max-h-[55vh] overflow-y-auto rounded-xl border border-white/20 bg-[#17181b]/95 p-3 shadow-2xl sm:left-auto sm:right-6 sm:w-80">
        <div className="mb-2 flex items-center justify-between"><h2 className="text-sm font-semibold">Người đã xem</h2><button type="button" onClick={() => setIsViewerListOpen(false)} className="border-0 bg-transparent text-white/80">×</button></div>
        {viewers.length === 0 && !isLoadingViewers && <p className="py-4 text-center text-sm text-white/70">Chưa có lượt xem.</p>}
        <div className="space-y-2">{viewers.map((viewer) => <div key={viewer.userId} className="flex items-center gap-2"><span className="grid h-8 w-8 place-items-center rounded-full bg-primary text-[10px] font-bold">{viewer.displayName.slice(0, 2).toUpperCase()}</span><span className="min-w-0 flex-1"><span className="block truncate text-sm">{viewer.displayName}</span><span className="block text-xs text-white/60">@{viewer.username}</span></span>{viewer.reactionType && <span>{reactionChoices.find(([type]) => type === viewer.reactionType)?.[1]}</span>}</div>)}</div>
        {viewerCursor && <button type="button" disabled={isLoadingViewers} onClick={() => void loadViewers(viewerCursor)} className="mt-3 w-full rounded-lg border border-white/20 bg-white/10 px-3 py-2 text-xs text-white">Tải thêm</button>}
      </aside>}
    </div>
  </div>
}
