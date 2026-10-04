import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState, type PointerEvent } from 'react'
import { ApiError } from '../../../api/client'
import type { MediaReadUrl } from '../../../api/media'
import { storiesApi, type Story, type StoryReactionType, type StoryTrayAuthor, type StoryViewer } from '../../../api/stories'
import { resolveProfileImageUrl } from '../../../api/users'
import { useDialogFocus } from '../../../shared/useDialogFocus'
import { isEditableTarget } from './postPhotoLightbox'
import { imageDurationMs, imageElapsedMs, pauseImageClock, type ImageStoryClock } from './storyPlayback'

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

interface StoryMediaState {
  key: string
  status: 'loading' | 'ready' | 'error' | 'unavailable'
  url: string | null
  posterUrl: string | null
}

interface ReactionMutation {
  version: number
  desired: StoryReactionType | null
  confirmed: Story
  running: boolean
}

interface MediaGesture {
  pointerId: number
  x: number
  y: number
  startedAt: number
  moved: boolean
}

interface MediaAccessEntry {
  promise: Promise<[MediaReadUrl, MediaReadUrl | null]>
  expiresAt: number
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
  const [mediaState, setMediaState] = useState<StoryMediaState | null>(null)
  const [mediaAttempt, setMediaAttempt] = useState(0)
  const [progress, setProgress] = useState(0)
  const [isPaused, setIsPaused] = useState(false)
  const [isHolding, setIsHolding] = useState(false)
  const [isMuted, setIsMuted] = useState(true)
  const [documentHidden, setDocumentHidden] = useState(document.hidden)
  const [windowFocused, setWindowFocused] = useState(true)
  const [playbackBlocked, setPlaybackBlocked] = useState(false)
  const imageClock = useRef<ImageStoryClock>({ elapsedMs: 0, startedAt: null })
  const [reply, setReply] = useState('')
  const [isReplyFocused, setIsReplyFocused] = useState(false)
  const replyRequest = useRef<symbol | null>(null)
  const [isSendingReply, setIsSendingReply] = useState(false)
  const [replyStatus, setReplyStatus] = useState<string | null>(null)
  const [viewers, setViewers] = useState<StoryViewer[]>([])
  const [viewerCursor, setViewerCursor] = useState<string | null>(null)
  const [isViewerListOpen, setIsViewerListOpen] = useState(false)
  const [isLoadingViewers, setIsLoadingViewers] = useState(false)
  const [viewerError, setViewerError] = useState<string | null>(null)
  const viewerRequest = useRef<symbol | null>(null)
  const reactionMutations = useRef(new Map<string, ReactionMutation>())
  const [reactionError, setReactionError] = useState<{ storyId: string; message: string } | null>(null)
  const videoRef = useRef<HTMLVideoElement>(null)
  const dialogRef = useRef<HTMLDivElement>(null)
  const mediaGesture = useRef<MediaGesture | null>(null)
  const suppressMediaClick = useRef(false)
  const activeRef = useRef<Story | undefined>(undefined)
  const groupsRef = useRef(groups)
  const onStoriesChangedRef = useRef(onStoriesChanged)
  const viewedRequests = useRef(new Set<string>())
  const viewedStories = useRef(new Set<string>())
  const mounted = useRef(false)
  const activeMediaKey = useRef('')
  const pauseRef = useRef(true)
  const mediaAccessCache = useRef(new Map<string, MediaAccessEntry>())

  const activeGroup = groups[authorIndex]
  const active = activeGroup?.stories[storyIndex]
  const activeKey = active?.id
  const isVideo = active?.media.mediaType === 'video'
  const mediaIdentity = `${activeKey ?? ''}:${active?.media.mediaId ?? ''}`
  const mediaKey = `${mediaIdentity}:${mediaAttempt}`
  const nextStory = activeGroup?.stories[storyIndex + 1] ?? groups[authorIndex + 1]?.stories[0]
  const nextStoryId = nextStory?.id
  const nextMediaId = nextStory?.media.mediaId
  const nextMediaType = nextStory?.media.mediaType
  const currentMedia = mediaState?.key === mediaKey ? mediaState : null
  const mediaUrl = currentMedia?.url ?? null
  const mediaReady = currentMedia?.status === 'ready'
  const mediaFailed = currentMedia?.status === 'error' || currentMedia?.status === 'unavailable'
  const shouldPause = isPaused || isHolding || isReplyFocused || isSendingReply || isViewerListOpen || documentHidden || !windowFocused || !mediaReady
  useDialogFocus(Boolean(active), dialogRef, onClose)

  useLayoutEffect(() => {
    activeRef.current = active
    groupsRef.current = groups
    onStoriesChangedRef.current = onStoriesChanged
  }, [active, groups, onStoriesChanged])
  useLayoutEffect(() => {
    activeMediaKey.current = mediaKey
    pauseRef.current = shouldPause
  }, [mediaKey, shouldPause])
  useEffect(() => {
    mounted.current = true
    return () => { mounted.current = false }
  }, [])
  const publishStory = useCallback((updated: Story) => {
    // Mirror before React commits so simultaneous view/reaction responses merge into the same snapshot.
    groupsRef.current = groupsRef.current.map((group) => ({
      ...group,
      stories: group.stories.map((story) => story.id === updated.id ? updated : story),
    }))
    onStoriesChangedRef.current(updated)
  }, [])
  const resolveMedia = useCallback((story: Story) => {
    const cache = mediaAccessCache.current
    const key = `${story.id}:${story.media.mediaId}`
    const cached = cache.get(key)
    if (cached && cached.expiresAt > Date.now() + 1000) {
      cache.delete(key)
      cache.set(key, cached)
      return cached.promise
    }
    const storyExpiry = story.canManage ? Infinity : Date.parse(story.expiresAtUtc)
    const entry: MediaAccessEntry = {
      expiresAt: storyExpiry,
      promise: Promise.all([storiesApi.mediaAccess(story), storiesApi.posterAccess(story).catch(() => null)]),
    }
    entry.promise = entry.promise.then(([media, poster]): [MediaReadUrl, MediaReadUrl | null] => {
      entry.expiresAt = Math.min(storyExpiry, Date.parse(media.expiresAtUtc), poster ? Date.parse(poster.expiresAtUtc) : Infinity)
      return [media, poster]
    }).catch((error: unknown) => {
      if (cache.get(key) === entry) cache.delete(key)
      throw error
    })
    cache.set(key, entry)
    while (cache.size > 3) cache.delete(cache.keys().next().value!)
    return entry.promise
  }, [])

  const [playbackKey, setPlaybackKey] = useState(mediaKey)
  if (playbackKey !== mediaKey) {
    setPlaybackKey(mediaKey)
    setProgress(0)
    setPlaybackBlocked(false)
    setIsHolding(false)
  }

  const [replyStoryId, setReplyStoryId] = useState(activeKey)
  if (replyStoryId !== activeKey) {
    setReplyStoryId(activeKey)
    setReply('')
    setReplyStatus(null)
    setIsReplyFocused(false)
    setIsSendingReply(false)
    setViewers([])
    setViewerCursor(null)
    setIsViewerListOpen(false)
    setIsLoadingViewers(false)
    setViewerError(null)
    setReactionError(null)
  }
  useLayoutEffect(() => {
    replyRequest.current = null
    viewerRequest.current = null
    return () => { replyRequest.current = null; viewerRequest.current = null }
  }, [activeKey])

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
    const story = activeRef.current
    if (!story) return
    let alive = true
    const timeoutId = window.setTimeout(() => {
      void resolveMedia(story)
        .then(([media, poster]) => {
          if (!alive) return
          setMediaState({ key: mediaKey, status: 'loading', url: media.url, posterUrl: poster?.url ?? null })
        })
        .catch((error: unknown) => {
          if (!alive) return
          const unavailable = error instanceof ApiError && [403, 404, 410].includes(error.status)
          setMediaState({ key: mediaKey, status: unavailable ? 'unavailable' : 'error', url: null, posterUrl: null })
        })
    }, 0)
    return () => { alive = false; window.clearTimeout(timeoutId) }
  }, [mediaKey, resolveMedia])

  useEffect(() => {
    if (!mediaReady || !nextStoryId || nextMediaType !== 'image') return
    const story = groupsRef.current.flatMap((group) => group.stories).find((item) => item.id === nextStoryId)
    if (!story || story.media.mediaId !== nextMediaId) return
    let alive = true
    void resolveMedia(story).then(([media]) => {
      if (!alive) return
      const image = new Image()
      image.src = media.url
      void image.decode().catch(() => undefined)
    }).catch(() => undefined)
    return () => { alive = false }
  }, [mediaReady, nextStoryId, nextMediaId, nextMediaType, resolveMedia])

  useEffect(() => {
    if (!activeKey || !mediaReady || active?.canManage || active?.isViewed || viewedStories.current.has(activeKey) || viewedRequests.current.has(activeKey)) return
    const storyId = activeKey
    viewedRequests.current.add(storyId)
    void storiesApi.markViewed(storyId).then(() => {
      viewedStories.current.add(storyId)
      const story = groupsRef.current.flatMap((group) => group.stories).find((item) => item.id === storyId)
      if (mounted.current && story) publishStory({ ...story, isViewed: true })
    }).catch(() => undefined).finally(() => { viewedRequests.current.delete(storyId) })
  }, [activeKey, mediaReady, active?.canManage, active?.isViewed, publishStory])

  const nextRef = useRef(next)
  useLayoutEffect(() => { nextRef.current = next }, [next])
  useLayoutEffect(() => {
    imageClock.current = { elapsedMs: 0, startedAt: null }
    mediaGesture.current = null
  }, [mediaKey])

  useEffect(() => {
    const releaseHold = () => {
      if (mediaGesture.current) suppressMediaClick.current = true
      mediaGesture.current = null
      setIsHolding(false)
    }
    const visibility = () => {
      setDocumentHidden(document.hidden)
      if (document.hidden) releaseHold()
    }
    const blur = () => { setWindowFocused(false); releaseHold() }
    const focus = () => setWindowFocused(true)
    document.addEventListener('visibilitychange', visibility)
    window.addEventListener('blur', blur)
    window.addEventListener('focus', focus)
    return () => {
      document.removeEventListener('visibilitychange', visibility)
      window.removeEventListener('blur', blur)
      window.removeEventListener('focus', focus)
    }
  }, [])

  useEffect(() => {
    if (!activeKey || isVideo || shouldPause || !mediaReady) return
    const clock = imageClock.current
    clock.startedAt = performance.now()
    const intervalId = window.setInterval(() => {
      const elapsed = imageElapsedMs(clock, performance.now())
      setProgress(elapsed / imageDurationMs * 100)
      if (elapsed >= imageDurationMs) {
        window.clearInterval(intervalId)
        nextRef.current()
      }
    }, 80)
    return () => {
      window.clearInterval(intervalId)
      pauseImageClock(clock, performance.now())
    }
  }, [activeKey, mediaKey, shouldPause, isVideo, mediaReady])

  useEffect(() => {
    const video = videoRef.current
    if (!video) return
    let alive = true
    if (shouldPause) video.pause()
    else void video.play().then(() => {
      if (videoRef.current !== video || pauseRef.current) video.pause()
      if (alive) setPlaybackBlocked(false)
    }).catch(() => { if (alive) setPlaybackBlocked(true) })
    return () => { alive = false; video.pause() }
  }, [mediaKey, mediaUrl, shouldPause])

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented) return
      if (event.key === 'Escape') { onClose(); return }
      if (isEditableTarget(event.target)) return
      if (event.key === 'ArrowLeft') { event.preventDefault(); previous() }
      if (event.key === 'ArrowRight') { event.preventDefault(); next() }
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

  const mediaLoaded = () => {
    if (activeMediaKey.current !== mediaKey) return
    setMediaState((current) => current?.key === mediaKey && current.status === 'loading' ? { ...current, status: 'ready' } : current)
  }
  const mediaLoadFailed = () => {
    if (activeMediaKey.current !== mediaKey) return
    videoRef.current?.pause()
    setMediaState((current) => current?.key === mediaKey ? { ...current, status: 'error' } : current)
  }
  const retryMedia = () => {
    mediaAccessCache.current.delete(mediaIdentity)
    setMediaAttempt((current) => current + 1)
  }
  const playVideo = () => {
    const video = videoRef.current
    if (!video || shouldPause) return
    void video.play().then(() => {
      if (videoRef.current !== video || pauseRef.current) video.pause()
      if (mounted.current && activeMediaKey.current === mediaKey) setPlaybackBlocked(false)
    }).catch(() => {
      if (mounted.current && activeMediaKey.current === mediaKey) setPlaybackBlocked(true)
    })
  }

  const startGesture = (event: PointerEvent<HTMLDivElement>) => {
    const target = event.target instanceof Element ? event.target : null
    if (!event.isPrimary || event.button !== 0) return
    suppressMediaClick.current = false
    if (target?.closest('button:not([data-story-navigation])')) return
    mediaGesture.current = { pointerId: event.pointerId, x: event.clientX, y: event.clientY, startedAt: performance.now(), moved: false }
    setIsHolding(true)
    // Capture on the original target so a short tap still clicks its navigation button.
    ;(target ?? event.currentTarget).setPointerCapture(event.pointerId)
  }
  const moveGesture = (event: PointerEvent<HTMLDivElement>) => {
    const gesture = mediaGesture.current
    if (!gesture || gesture.pointerId !== event.pointerId) return
    if (Math.max(Math.abs(event.clientX - gesture.x), Math.abs(event.clientY - gesture.y)) > 10) {
      gesture.moved = true
      setIsHolding(false)
    }
  }
  const finishGesture = (event: PointerEvent<HTMLDivElement>) => {
    const gesture = mediaGesture.current
    if (!gesture || gesture.pointerId !== event.pointerId) return
    mediaGesture.current = null
    setIsHolding(false)
    const dx = event.clientX - gesture.x
    const dy = event.clientY - gesture.y
    const swipe = Math.abs(dx) > 40 && Math.abs(dx) > Math.abs(dy)
    suppressMediaClick.current = gesture.moved || swipe || performance.now() - gesture.startedAt >= 250
    if (swipe) { if (dx > 0) previous(); else next() }
  }
  const cancelGesture = (event: PointerEvent<HTMLDivElement>) => {
    if (mediaGesture.current?.pointerId !== event.pointerId) return
    mediaGesture.current = null
    suppressMediaClick.current = true
    setIsHolding(false)
  }

  const setReaction = async (type: StoryReactionType) => {
    const storyId = active.id
    const currentStory = groupsRef.current.flatMap((group) => group.stories).find((story) => story.id === storyId) ?? active
    const mutation = reactionMutations.current.get(storyId) ?? { version: 0, desired: currentStory.viewerReaction, confirmed: currentStory, running: false }
    reactionMutations.current.set(storyId, mutation)
    const selected = mutation.running ? mutation.desired : currentStory.viewerReaction
    mutation.desired = selected === type ? null : type
    mutation.version++
    setReactionError(null)
    if (mutation.running) return
    mutation.confirmed = currentStory
    mutation.running = true
    const publishReaction = () => {
      const current = groupsRef.current.flatMap((group) => group.stories).find((story) => story.id === storyId)
      if (current) publishStory({ ...current, viewerReaction: mutation.confirmed.viewerReaction, reactionCount: mutation.confirmed.reactionCount })
    }
    // Serialize writes per story and coalesce queued clicks so the server also keeps the latest intent.
    try {
      for (;;) {
        const version = mutation.version
        const desired = mutation.desired
        try {
          const updated = desired === null
            ? await storiesApi.removeReaction(storyId).then(() => ({
              ...mutation.confirmed,
              viewerReaction: null,
              reactionCount: Math.max(0, mutation.confirmed.reactionCount - (mutation.confirmed.viewerReaction ? 1 : 0)),
            }))
            : await storiesApi.setReaction(storyId, desired)
          mutation.confirmed = updated
          if (version === mutation.version && mounted.current) publishReaction()
        } catch {
          if (version === mutation.version && mounted.current) {
            publishReaction()
            if (activeRef.current?.id === storyId) setReactionError({ storyId, message: 'Không thể cập nhật phản ứng.' })
          }
        }
        if (version === mutation.version) break
      }
    } finally {
      mutation.running = false
    }
  }

  const sendReply = async () => {
    const content = reply.trim()
    if (!content || replyRequest.current || active.canManage) return
    const storyIdAtSubmit = active.id
    const request = Symbol()
    replyRequest.current = request
    setIsSendingReply(true)
    setReplyStatus(null)
    try {
      await storiesApi.reply(storyIdAtSubmit, content)
      if (replyRequest.current !== request) return
      setReply((current) => current.trim() === content ? '' : current)
      setReplyStatus('Đã gửi vào Zola Light.')
    } catch (error) {
      if (replyRequest.current !== request) return
      setReplyStatus(error instanceof ApiError ? error.message : 'Không thể gửi phản hồi.')
    } finally {
      if (replyRequest.current === request) {
        replyRequest.current = null
        setIsSendingReply(false)
      }
    }
  }

  const loadViewers = async (cursor?: string) => {
    if (!active.canManage || viewerRequest.current) return
    const storyId = active.id
    const request = Symbol()
    viewerRequest.current = request
    setIsViewerListOpen(true)
    setIsLoadingViewers(true)
    setViewerError(null)
    if (!cursor) {
      setViewers([])
      setViewerCursor(null)
    }
    try {
      const page = await storiesApi.viewers(storyId, cursor)
      if (viewerRequest.current !== request || activeRef.current?.id !== storyId) return
      setViewers((current) => cursor
        ? [...current, ...page.items.filter((item) => !current.some((viewer) => viewer.userId === item.userId))]
        : page.items)
      setViewerCursor(page.nextCursor)
    } catch {
      if (viewerRequest.current === request && activeRef.current?.id === storyId) setViewerError('Không thể tải danh sách người xem.')
    } finally {
      if (viewerRequest.current === request) {
        viewerRequest.current = null
        setIsLoadingViewers(false)
      }
    }
  }
  const closeViewers = () => {
    viewerRequest.current = null
    setIsViewerListOpen(false)
    setIsLoadingViewers(false)
    setViewerError(null)
  }

  return <div ref={dialogRef} tabIndex={-1} className="fixed inset-0 z-[100] grid bg-black/90 text-white [&_button]:focus-visible:outline-2 [&_button]:focus-visible:outline-white" role="dialog" aria-modal="true" aria-label="Trình xem Story">
    <div className="relative mx-auto flex h-full w-full max-w-2xl items-center justify-center sm:px-4">
      <div className="relative h-full w-full overflow-hidden bg-black sm:h-[min(92vh,860px)] sm:rounded-2xl">
        <div className="absolute inset-x-3 top-3 z-20 flex gap-1" aria-label="Tiến trình Story">
          {activeGroup.stories.map((story, index) => <span key={story.id} className="h-1 flex-1 overflow-hidden rounded-full bg-white/35">
            <span className="block h-full bg-white transition-[width] motion-reduce:transition-none" style={{ width: index < storyIndex ? '100%' : index === storyIndex ? `${progress}%` : '0%' }} />
          </span>)}
        </div>
        <div className="absolute inset-x-4 top-7 z-20 flex items-center gap-3">
          {active.author.avatarUrl ? <img src={resolveProfileImageUrl(active.author.avatarUrl)} className="h-9 w-9 rounded-full object-cover" alt="" /> : <span className="grid h-9 w-9 place-items-center rounded-full bg-primary text-xs font-bold">{active.author.displayName.slice(0, 2).toUpperCase()}</span>}
          <div className="min-w-0"><p className="truncate text-sm font-semibold">{active.author.displayName} <span className="font-normal text-white/70">@{active.author.username}</span></p><p className="text-xs text-white/70">{new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(new Date(active.createdAtUtc))}</p></div>
          <button type="button" data-dialog-initial-focus className="ml-auto grid h-9 w-9 place-items-center rounded-full border-0 bg-black/35 text-xl text-white" onClick={onClose} aria-label="Đóng Story">×</button>
        </div>

        <div className="absolute inset-0 touch-pan-y select-none" onPointerDown={startGesture} onPointerMove={moveGesture} onPointerUp={finishGesture} onPointerCancel={cancelGesture} onLostPointerCapture={cancelGesture} onClickCapture={(event) => {
          if (suppressMediaClick.current && event.detail > 0) {
            suppressMediaClick.current = false
            event.preventDefault()
            event.stopPropagation()
          }
        }}>
          <div className="absolute inset-0 flex h-full min-h-0 items-center justify-center">
            {mediaFailed && <div role="status" className="rounded-lg bg-black/70 px-4 py-3 text-center text-sm">
              <p>{currentMedia?.status === 'unavailable' ? 'Story không còn khả dụng' : 'Không thể tải tin'}</p>
              {currentMedia?.status === 'unavailable' ? <p className="mt-2 text-xs text-white/70">Story có thể đã hết hạn, bị xóa hoặc bạn không có quyền xem.</p> : <button type="button" onClick={retryMedia} className="mt-3 rounded-lg bg-white/15 px-4 py-2 focus-visible:outline-2 focus-visible:outline-white">Thử lại</button>}
            </div>}
            {!mediaFailed && !mediaReady && <p role="status" className="absolute text-sm text-white/80">Đang tải Story…</p>}
            {mediaUrl && !mediaFailed && isVideo && <video key={mediaKey} ref={videoRef} src={mediaUrl} poster={currentMedia?.posterUrl ?? undefined} autoPlay={!shouldPause} playsInline muted={isMuted} className="block h-full max-h-full w-full max-w-full object-contain" onLoadedMetadata={(event) => { if (Number.isFinite(event.currentTarget.duration) && event.currentTarget.duration > 0) mediaLoaded() }} onError={mediaLoadFailed} onEnded={() => { if (activeMediaKey.current === mediaKey && !pauseRef.current) nextRef.current() }} onTimeUpdate={(event) => { const video = event.currentTarget; if (activeMediaKey.current === mediaKey && mediaReady && video.duration > 0) setProgress(Math.min(100, video.currentTime / video.duration * 100)) }} />}
            {mediaUrl && !mediaFailed && isVideo && playbackBlocked && !shouldPause && <button type="button" aria-label="Phát video Story" onClick={playVideo} className="absolute z-20 rounded-full bg-white/20 px-4 py-2 text-sm focus-visible:outline-2 focus-visible:outline-white">Phát video</button>}
            {mediaUrl && !mediaFailed && !isVideo && <img key={mediaKey} src={mediaUrl} draggable={false} className="block h-full max-h-full w-full max-w-full object-contain" alt={active.caption ?? 'Story'} onLoad={mediaLoaded} onError={mediaLoadFailed} />}
          </div>

          <button type="button" data-story-navigation onClick={previous} className="absolute inset-y-16 left-0 z-10 w-[35%] border-0 bg-transparent" aria-label="Story trước" />
          <button type="button" data-story-navigation onClick={next} className="absolute inset-y-16 right-0 z-10 w-[35%] border-0 bg-transparent" aria-label="Story tiếp theo" />
        </div>
        <button type="button" onClick={() => setIsPaused((current) => !current)} aria-label={isPaused ? 'Tiếp tục' : 'Tạm dừng'} aria-pressed={isPaused} className="absolute right-4 top-20 z-20 rounded-full border-0 bg-black/35 px-3 py-1 text-xs text-white">{isPaused ? 'Tiếp tục' : 'Tạm dừng'}</button>
        {isVideo && mediaUrl && !mediaFailed && <button type="button" onClick={() => setIsMuted((current) => !current)} aria-label={isMuted ? 'Bật âm thanh' : 'Tắt âm thanh'} aria-pressed={!isMuted} className="absolute left-4 top-20 z-20 grid h-8 w-8 place-items-center rounded-full bg-black/35 text-sm">{isMuted ? '🔇' : '🔊'}</button>}

        <div className="absolute inset-x-0 bottom-0 z-20 bg-linear-to-t from-black/90 via-black/45 to-transparent px-4 pb-5 pt-24">
          {active.caption && <p className="mb-3 whitespace-pre-wrap text-sm leading-relaxed">{active.caption}</p>}
          <div className="flex items-center gap-2">
            {!active.canManage && <div className="flex rounded-full bg-white/15 p-1">{reactionChoices.map(([type, icon]) => <button key={type} type="button" onClick={() => void setReaction(type)} className={`grid h-8 w-8 place-items-center rounded-full border-0 text-base ${active.viewerReaction === type ? 'bg-white/30' : 'bg-transparent'}`} aria-label={type} aria-pressed={active.viewerReaction === type}>{icon}</button>)}</div>}
            {active.reactionCount > 0 && <span className="rounded-full bg-white/15 px-3 py-2 text-xs">{active.reactionCount} phản ứng</span>}
            {active.canManage && <button type="button" onClick={() => void loadViewers()} className="ml-auto rounded-full border-0 bg-white/15 px-3 py-2 text-xs text-white">{viewersTitle}</button>}
          </div>
          {!active.canManage && <div className="mt-3 flex gap-2 rounded-full bg-white/15 p-1 pl-4"><input key={activeKey} aria-label="Trả lời Story" value={reply} onFocus={() => setIsReplyFocused(true)} onBlur={() => setIsReplyFocused(false)} maxLength={5000} onChange={(event) => setReply(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') void sendReply() }} placeholder="Trả lời qua Zola Light…" className="min-w-0 flex-1 border-0 bg-transparent text-sm text-white outline-none placeholder:text-white/65 focus-visible:outline-2 focus-visible:outline-white" /><button type="button" disabled={!reply.trim() || isSendingReply} onClick={() => void sendReply()} className="rounded-full border-0 bg-primary px-4 py-2 text-xs font-semibold text-white disabled:opacity-50">Gửi</button></div>}
          {replyStatus && <p className="mt-2 text-xs text-white/80">{replyStatus}</p>}
          {reactionError?.storyId === activeKey && <p role="status" className="mt-2 text-xs text-white/80">{reactionError.message}</p>}
        </div>
      </div>

      {isViewerListOpen && <aside className="absolute inset-x-3 bottom-3 z-30 max-h-[55vh] overflow-y-auto rounded-xl border border-white/20 bg-[#17181b]/95 p-3 shadow-2xl sm:left-auto sm:right-6 sm:w-80">
        <div className="mb-2 flex items-center justify-between"><h2 className="text-sm font-semibold">Người đã xem</h2><button type="button" onClick={closeViewers} aria-label="Đóng danh sách người xem" className="border-0 bg-transparent text-white/80 focus-visible:outline-2 focus-visible:outline-white">×</button></div>
        {isLoadingViewers && <p role="status" className="py-2 text-center text-sm text-white/70">Đang tải người xem…</p>}
        {viewerError && <div role="status" className="py-2 text-center text-sm text-white/70"><p>{viewerError}</p><button type="button" onClick={() => void loadViewers(viewerCursor ?? undefined)} className="mt-2 rounded-lg bg-white/10 px-3 py-2 focus-visible:outline-2 focus-visible:outline-white">Thử lại</button></div>}
        {viewers.length === 0 && !isLoadingViewers && !viewerError && <p className="py-4 text-center text-sm text-white/70">Chưa có lượt xem.</p>}
        <div className="space-y-2">{viewers.map((viewer) => <div key={viewer.userId} className="flex items-center gap-2"><span className="grid h-8 w-8 place-items-center rounded-full bg-primary text-[10px] font-bold">{viewer.displayName.slice(0, 2).toUpperCase()}</span><span className="min-w-0 flex-1"><span className="block truncate text-sm">{viewer.displayName}</span><span className="block text-xs text-white/60">@{viewer.username}</span></span>{viewer.reactionType && <span>{reactionChoices.find(([type]) => type === viewer.reactionType)?.[1]}</span>}</div>)}</div>
        {viewerCursor && <button type="button" disabled={isLoadingViewers} onClick={() => void loadViewers(viewerCursor)} className="mt-3 w-full rounded-lg border border-white/20 bg-white/10 px-3 py-2 text-xs text-white">Tải thêm</button>}
      </aside>}
    </div>
  </div>
}
