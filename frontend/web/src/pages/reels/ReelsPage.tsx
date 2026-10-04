import { useCallback, useEffect, useRef, useState, type ReactNode, type RefObject } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { mediaApi, type Media } from '../../api/media'
import { postsApi, type Comment } from '../../api/posts'
import { reelsApi, type Reel, type ReelFeedMode } from '../../api/reels'
import { resolveProfileImageUrl, usersApi, type UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import { usePreferences } from '../../preferences'
import TextWithReferences from '../../shared/components/TextWithReferences'
import { formatPostTimestamp } from '../../shared/formatPostTimestamp'
import { CommentComposer } from '../feed/components/PostDiscussion'
import ShareDialog from '../feed/components/ShareDialog'
import { Mascot } from 'page-mascot'
import './reels.css'

const supportedVideoTypes = new Set(['video/mp4', 'video/webm'])
const maximumVideoBytes = 500 * 1024 * 1024

function ReelsSidebar({ activeMode, onCreate, onSelectMode }: { activeMode: ReelFeedMode; onCreate: () => void; onSelectMode: (mode: ReelFeedMode) => void }) {
  return <aside className="absolute inset-y-0 left-0 z-20 hidden w-48 border-r border-white/10 bg-black px-3 py-2 lg:flex lg:flex-col">
    <div className="flex items-center gap-2 px-1 py-1">
      <Mascot
        directions="/mascots/tv-directions.webp"
        reactions="/mascots/tv-reactions.webp"
        size={44}
        label="Reels TV Mascot"
      />
      <h1 className="text-xl font-bold text-white">Reels</h1>
    </div>
    <nav className="mt-1 flex flex-col gap-1" aria-label="Điều hướng Reels">
      <button type="button" onClick={() => onSelectMode('forYou')} className={'flex h-9 items-center gap-3 rounded-md border-0 px-2.5 text-sm font-semibold cursor-pointer ' + (activeMode === 'forYou' ? 'bg-[#27292d] text-white' : 'bg-transparent text-white/70 hover:bg-white/10')}><span aria-hidden="true">★</span>Dành cho bạn</button>
      <button type="button" onClick={() => onSelectMode('following')} className={'flex h-9 items-center gap-3 rounded-md border-0 px-2.5 text-sm font-semibold cursor-pointer ' + (activeMode === 'following' ? 'bg-[#27292d] text-white' : 'bg-transparent text-white/70 hover:bg-white/10')}><span aria-hidden="true">▣</span>Đang theo dõi</button>
      <Link to="/profile" className="flex h-9 items-center gap-3 rounded-md px-2.5 text-sm font-semibold text-white/70 no-underline hover:bg-white/10"><span aria-hidden="true">◎</span>Trang cá nhân</Link>
    </nav>
    <button type="button" onClick={onCreate} className="mt-4 rounded-md border-0 bg-primary px-3 py-2 text-sm font-bold text-white cursor-pointer">＋ Tạo Reel</button>
  </aside>
}

function ReelAction({ label, count, active, onClick, children, buttonRef, expanded, controls }: { label: string; count?: number; active?: boolean; onClick: () => void; children: ReactNode; buttonRef?: RefObject<HTMLButtonElement | null>; expanded?: boolean; controls?: string }) {
  return <button ref={buttonRef} type="button" aria-label={label} aria-expanded={expanded} aria-controls={controls} onClick={onClick} className={'flex w-11 flex-col items-center gap-0.5 border-0 bg-transparent text-xs font-semibold cursor-pointer ' + (active ? 'text-primary-light' : 'text-white hover:text-white/70')}>
    <span className="grid h-9 w-9 place-items-center rounded-full text-2xl leading-none">{children}</span>
    {count !== undefined && <span>{count.toLocaleString()}</span>}
  </button>
}

export default function ReelsPage() {
  const { session } = useAuth()
  const [searchParams] = useSearchParams()
  const requestedReelId = searchParams.get('reel')
  const [viewerProfile, setViewerProfile] = useState<UserProfile>()
  const [reels, setReels] = useState<Reel[]>([])
  const [activeIndex, setActiveIndex] = useState(0)
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [feedMode, setFeedMode] = useState<ReelFeedMode>('forYou')
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [volume, setVolume] = useState(1)
  const [isMuted, setIsMuted] = useState(true)
  const listRef = useRef<HTMLDivElement>(null)
  const currentUserProfile = viewerProfile?.userId === session?.user.id ? viewerProfile : undefined

  useEffect(() => {
    if (!session?.user.id) return
    let isCurrent = true
    void usersApi.getCurrent()
      .then((profile) => {
        if (isCurrent) setViewerProfile(profile)
      })
      .catch(() => undefined)
    return () => { isCurrent = false }
  }, [session?.user.id])

  const load = useCallback(async (cursor?: string, append = false) => {
    if (append) setIsLoadingMore(true)
    else setIsLoading(true)
    setError(null)
    try {
      const page = requestedReelId && !cursor
        ? { items: [await reelsApi.get(requestedReelId)], nextCursor: null }
        : await reelsApi.getFeed(feedMode, cursor)
      setReels((current) => append
        ? [...current, ...page.items.filter((item) => !current.some((reel) => reel.id === item.id))]
        : page.items)
      setNextCursor(page.nextCursor)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải Reels.')
    } finally {
      if (append) setIsLoadingMore(false)
      else setIsLoading(false)
    }
  }, [feedMode, requestedReelId])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => { void load() }, 0)
    return () => window.clearTimeout(timeoutId)
  }, [load])

  const selectIndex = (index: number) => {
    const next = Math.max(0, Math.min(index, reels.length - 1))
    setActiveIndex(next)
    listRef.current?.querySelector<HTMLElement>(`[data-reel-index="${next}"]`)
      ?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
  }

  const updateReel = (updated: Reel) => {
    setReels((current) => current.map((reel) => reel.id === updated.id ? { ...reel, ...updated } : reel))
  }

  return (
    <div className="relative flex h-[calc(100dvh-var(--app-header-height))] flex-col overflow-hidden bg-black">
      <ReelsSidebar activeMode={feedMode} onCreate={() => setIsCreateOpen(true)} onSelectMode={(mode) => { if (mode !== feedMode) { setActiveIndex(0); setFeedMode(mode) } }} />
      <nav aria-label="Chế độ Reels" className="z-20 grid shrink-0 grid-cols-[1fr_1fr_auto] gap-1 border-b border-white/10 bg-black p-2 lg:hidden">
        {([['forYou', 'Dành cho bạn'], ['following', 'Đang theo dõi']] as const).map(([mode, label]) => <button key={mode} type="button" aria-pressed={feedMode === mode} onClick={() => { if (mode !== feedMode) { setActiveIndex(0); setFeedMode(mode) } }} className={`rounded-full px-2 py-2 text-xs font-semibold ${feedMode === mode ? 'bg-white/15 text-white' : 'text-white/70'}`}>{label}</button>)}
        <button type="button" onClick={() => setIsCreateOpen(true)} aria-label="Tạo Reel" className="rounded-full bg-primary px-3 py-2 text-xs font-bold text-white">＋ Tạo</button>
      </nav>
      <div ref={listRef} className="min-h-0 flex-1 snap-y snap-mandatory overflow-y-auto scroll-smooth md:pr-16 lg:pl-48">
        {isLoading && <div className="grid h-full place-items-center text-sm text-text-muted">Đang tải Reels…</div>}
        {error && <div className="grid h-full place-items-center p-6"><div className="max-w-md rounded-xl border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-4 text-sm text-[#ff8a9b]"><p>{error}</p><button type="button" onClick={() => void load()} className="mt-3 rounded-lg bg-primary px-3 py-2 text-white">Thử lại</button></div></div>}
        {!isLoading && !error && reels.length === 0 && (
          <div className="grid h-full place-items-center text-center">
            <div className="flex flex-col items-center gap-3">
              <Mascot
                directions="/mascots/tv-directions.webp"
                reactions="/mascots/tv-reactions.webp"
                size={88}
                label="TV Mascot"
              />
              <p className="text-lg font-semibold text-text">Chưa có Reel để xem</p>
              <button type="button" onClick={() => setIsCreateOpen(true)} className="mt-2 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white">Tạo Reel đầu tiên</button>
            </div>
          </div>
        )}
        {reels.map((reel, index) => (
          <ReelCard
            key={reel.id}
            reel={reel}
            currentUserProfile={currentUserProfile}
            active={activeIndex === index}
            shouldPreload={Math.abs(activeIndex - index) <= 1}
            volume={volume}
            isMuted={isMuted}
            onVolumeChange={(nextVolume) => { if (nextVolume > 0) setVolume(nextVolume); setIsMuted(nextVolume === 0) }}
            onToggleMute={() => setIsMuted((current) => !current)}
            index={index}
            onActivate={() => setActiveIndex(index)}
            onUpdated={updateReel}
          />
        ))}
        {isLoadingMore && <div className="py-4 text-center text-xs text-text-muted">Đang tải thêm…</div>}
      </div>

      {reels.length > 0 && <div className="absolute right-3 top-1/2 z-30 flex -translate-y-1/2 flex-col gap-3 max-md:top-1/4">
        <button type="button" disabled={activeIndex === 0} onClick={() => selectIndex(activeIndex - 1)} className="grid h-10 w-10 place-items-center rounded-full border-2 border-white bg-transparent text-xl text-white disabled:opacity-30 cursor-pointer" aria-label="Reel trước">⌃</button>
        <button type="button" disabled={activeIndex >= reels.length - 1} onClick={() => selectIndex(activeIndex + 1)} className="grid h-10 w-10 place-items-center rounded-full border-2 border-white bg-transparent text-xl text-white disabled:opacity-30 cursor-pointer" aria-label="Reel tiếp theo">⌄</button>
        {nextCursor && activeIndex >= reels.length - 1 && <button type="button" disabled={isLoadingMore} onClick={() => void load(nextCursor, true)} className="rounded-full bg-primary px-3 py-2 text-xs font-semibold text-white disabled:opacity-50">Thêm</button>}
      </div>}
      {isCreateOpen && <CreateReelDialog onClose={() => setIsCreateOpen(false)} onCreated={(reel) => { setReels((current) => [reel, ...current]); setActiveIndex(0); setIsCreateOpen(false) }} />}
    </div>
  )
}

function ReelCard({ reel, currentUserProfile, active, shouldPreload, volume, isMuted, onVolumeChange, onToggleMute, index, onActivate, onUpdated }: {
  reel: Reel
  currentUserProfile?: UserProfile
  active: boolean
  shouldPreload: boolean
  volume: number
  isMuted: boolean
  onVolumeChange: (volume: number) => void
  onToggleMute: () => void
  index: number
  onActivate: () => void
  onUpdated: (reel: Reel) => void
}) {
  const { session } = useAuth()
  const { t } = usePreferences()
  const cardRef = useRef<HTMLElement>(null)
  const videoRef = useRef<HTMLVideoElement>(null)
  const commentButtonRef = useRef<HTMLButtonElement>(null)
  const [videoUrl, setVideoUrl] = useState<string | null>(null)
  const [posterUrl, setPosterUrl] = useState<string | null>(null)
  const [isPaused, setIsPaused] = useState(false)
  const [isCommentsOpen, setIsCommentsOpen] = useState(false)
  const [comments, setComments] = useState<Comment[]>([])
  const [isLoadingComments, setIsLoadingComments] = useState(false)
  const [commentsError, setCommentsError] = useState<string | null>(null)
  const [commentSaveError, setCommentSaveError] = useState<string | null>(null)
  const [commentText, setCommentText] = useState('')
  const [hasRecordedThreshold, setHasRecordedThreshold] = useState(false)
  const [hasRecordedCompletion, setHasRecordedCompletion] = useState(false)
  const [isSavingComment, setIsSavingComment] = useState(false)
  const [currentMs, setCurrentMs] = useState(0)
  const [isSaved, setIsSaved] = useState(reel.viewerHasSaved)
  const [isShareOpen, setIsShareOpen] = useState(false)
  const [isMoreOpen, setIsMoreOpen] = useState(false)
  const [isFollowingAuthor, setIsFollowingAuthor] = useState(reel.viewerFollowsAuthor)
  const commentsOpen = active && isCommentsOpen
  const commentsId = `reel-comments-${reel.id}`

  useEffect(() => {
    if (!commentsOpen) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsCommentsOpen(false)
        commentButtonRef.current?.focus()
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [commentsOpen])

  useEffect(() => {
    const card = cardRef.current
    if (!card) return
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((entry) => entry.isIntersecting && entry.intersectionRatio >= 0.7)) {
        onActivate()
      }
    }, { threshold: [0.7] })
    observer.observe(card)
    return () => observer.disconnect()
  }, [onActivate])

  useEffect(() => {
    if (!shouldPreload || videoUrl) return
    let disposed = false
    Promise.all([reelsApi.getVideoAccess(reel.id), reelsApi.getPosterAccess(reel.id)])
      .then(([video, poster]) => {
        if (!disposed) {
          setVideoUrl(video.url)
          setPosterUrl(poster.url)
        }
      })
      .catch(() => { if (!disposed) setIsPaused(true) })
    return () => { disposed = true }
  }, [reel.id, shouldPreload, videoUrl])

  useEffect(() => {
    if (videoRef.current) videoRef.current.volume = volume
  }, [volume, videoUrl])

  useEffect(() => {
    const video = videoRef.current
    if (!video) return
    if (active && !isPaused) {
      void video.play().catch(() => setIsPaused(true))
    } else {
      video.pause()
    }
  }, [active, isPaused, videoUrl])

  const toggleReaction = async () => {
    try {
      const updated = reel.viewerReaction
        ? await postsApi.removeReaction(reel.id)
        : await postsApi.setReaction(reel.id, 'like')
      onUpdated({ ...reel, ...updated, author: reel.author, caption: reel.caption, video: reel.video, viewCount: reel.viewCount, completionCount: reel.completionCount, reactionCount: Object.values(updated.reactionCounts).reduce((total, count) => total + count, 0) })
    } catch {
      // The persisted feed is authoritative; a later reload restores its current state.
    }
  }

  const loadComments = async () => {
    setIsLoadingComments(true)
    setCommentsError(null)
    try {
      const page = await reelsApi.getComments(reel.id)
      setComments(page.items)
    } catch (requestError) {
      setCommentsError(requestError instanceof ApiError ? requestError.message : t('unableLoadComments'))
    } finally {
      setIsLoadingComments(false)
    }
  }

  const openComments = () => {
    setIsCommentsOpen((current) => !current)
    if (!isCommentsOpen && !isLoadingComments) void loadComments()
  }

  const saveReel = async () => {
    try {
      if (isSaved) await postsApi.removeSaved(reel.id)
      else await postsApi.save(reel.id)
      setIsSaved((current) => !current)
    } catch {
      // Saving can be retried from the same action without changing the Reel state.
    }
  }

  const followAuthor = async () => {
    if (isFollowingAuthor) return
    try {
      await usersApi.follow(reel.author.userId)
      setIsFollowingAuthor(true)
    } catch {
      // The follow action can be retried without interrupting playback.
    }
  }

  const submitComment = async () => {
    const content = commentText.trim()
    if (!content || isSavingComment) return
    setIsSavingComment(true)
    setCommentSaveError(null)
    try {
      const created = await postsApi.createComment(reel.id, content)
      setComments((current) => [...current, created])
      setCommentText('')
      onUpdated({ ...reel, commentCount: reel.commentCount + 1 })
    } catch (requestError) {
      setCommentSaveError(requestError instanceof ApiError ? requestError.message : t('unableCreateComment'))
    } finally {
      setIsSavingComment(false)
    }
  }

  const recordThreshold = (completed: boolean) => {
    const video = videoRef.current
    if (!video) return
    const duration = Math.min(Math.floor(video.currentTime * 1000), reel.video.durationMs)
    if (duration <= 0) return
    if (completed && hasRecordedCompletion) {
      void reelsApi.recordView(reel.id, reel.video.durationMs, true, true)
      return
    }
    if (!completed && hasRecordedThreshold) return
    if (completed) setHasRecordedCompletion(true)
    else setHasRecordedThreshold(true)
    void reelsApi.recordView(reel.id, completed ? reel.video.durationMs : duration, completed, false)
  }

  const initial = reel.author.displayName.slice(0, 2).toUpperCase()
  const volumePercent = isMuted ? 0 : Math.round(volume * 100)
  return (
    <section ref={cardRef} data-reel-index={index} className="relative flex min-h-full snap-start items-center justify-center overflow-hidden bg-black px-2 py-2 sm:px-4" onMouseEnter={onActivate}>
      <div className={`reel-stage ${commentsOpen ? 'reel-stage--comments-open' : ''}`}>
      <div className="reel-viewer">
      <div className="reel-player relative overflow-hidden rounded-lg bg-surface shadow-2xl">
        {videoUrl ? <video ref={videoRef} src={videoUrl} poster={posterUrl ?? undefined} muted={isMuted || !active} playsInline className="h-full w-full object-contain" onPlay={() => { onActivate(); setIsPaused(false) }} onTimeUpdate={() => { const milliseconds = Math.floor((videoRef.current?.currentTime ?? 0) * 1000); setCurrentMs(milliseconds); const threshold = Math.min(3_000, reel.video.durationMs * 0.25); if (milliseconds >= threshold) recordThreshold(false) }} onEnded={() => { setIsPaused(true); recordThreshold(true) }} /> : <div className="grid h-full place-items-center text-sm text-text-muted">Đang chuẩn bị video…</div>}
        <button type="button" onClick={() => { const video = videoRef.current; if (!video) return; if (video.paused) { if (video.ended) video.currentTime = 0; void video.play(); setIsPaused(false) } else { video.pause(); setIsPaused(true) } }} className="absolute inset-0 border-0 bg-transparent" aria-label={isPaused ? 'Phát Reel' : 'Tạm dừng Reel'} />
        <div role="group" aria-label="Điều chỉnh âm thanh Reel" className="absolute left-3 right-3 top-3 z-10 flex w-fit max-w-[calc(100%-1.5rem)] items-center gap-2 rounded-full bg-black/45 pr-3 text-white">
          <button type="button" onClick={onToggleMute} className="grid h-10 w-10 shrink-0 place-items-center rounded-full border-0 bg-transparent text-lg cursor-pointer focus-visible:outline-2 focus-visible:outline-white" aria-label={isMuted ? 'Bật âm thanh' : 'Tắt âm thanh'} aria-pressed={isMuted}>{isMuted ? '🔇' : '🔊'}</button>
          <input aria-label="Âm lượng Reel" aria-valuetext={`${volumePercent}%`} type="range" min="0" max="100" step="1" value={volumePercent} onChange={(event) => onVolumeChange(Number(event.target.value) / 100)} className="h-10 w-20 min-w-0 accent-white cursor-pointer focus-visible:outline-2 focus-visible:outline-white sm:w-24" />
          <span aria-hidden="true" className="w-8 shrink-0 text-right text-xs tabular-nums">{volumePercent}%</span>
        </div>
        <div className="absolute inset-x-0 bottom-0 bg-linear-to-t from-black/95 via-black/55 to-transparent px-4 pb-3 pt-24 text-white pointer-events-none">
          <div className="flex items-end gap-3 pointer-events-auto">
            <div className="h-10 w-10 shrink-0 overflow-hidden rounded-full bg-primary text-center text-xs font-bold leading-10">{reel.author.avatarUrl ? <img src={resolveProfileImageUrl(reel.author.avatarUrl)} alt="" loading="lazy" decoding="async" className="h-full w-full object-cover" /> : initial}</div>
            <div className="min-w-0 flex-1"><p className="font-semibold">{reel.author.displayName} <button type="button" onClick={() => void followAuthor()} disabled={isFollowingAuthor} className="ml-1 rounded border border-white/60 bg-transparent px-1.5 py-0.5 text-[11px] font-bold text-white cursor-pointer disabled:opacity-70">{isFollowingAuthor ? 'Đã theo dõi' : 'Theo dõi'}</button></p>{reel.caption && <TextWithReferences content={reel.caption} mentions={reel.mentions} className="mt-1 whitespace-pre-wrap text-sm leading-relaxed" />}<p className="mt-2 text-xs text-white/70">{reel.viewCount.toLocaleString()} lượt xem</p></div>
          </div>
          <input aria-label="Tiến trình Reel" type="range" min="0" max={reel.video.durationMs} value={Math.min(currentMs, reel.video.durationMs)} onChange={(event) => { const next = Number(event.target.value); if (videoRef.current) videoRef.current.currentTime = next / 1000; setCurrentMs(next) }} className="mt-3 w-full accent-primary pointer-events-auto" />
        </div>
      </div>
      <div className="reel-actions z-10 flex flex-col items-center gap-3">
        <ReelAction label="Thích Reel" count={reel.reactionCount} active={Boolean(reel.viewerReaction)} onClick={() => void toggleReaction()}>♡</ReelAction>
        <ReelAction label="Xem bình luận" count={reel.commentCount} active={commentsOpen} expanded={commentsOpen} controls={commentsId} buttonRef={commentButtonRef} onClick={openComments}><svg viewBox="0 0 24 24" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M21 11.5a8.5 8.5 0 0 1-8.5 8.5H4l-2 2V11.5a8.5 8.5 0 0 1 8.5-8.5h2a8.5 8.5 0 0 1 8.5 8.5Z" /><path d="M7 9h10M7 13h7" /></svg></ReelAction>
        <ReelAction label="Chia sẻ Reel" onClick={() => setIsShareOpen(true)}>↗</ReelAction>
        <div className="relative"><ReelAction label="Tùy chọn khác" onClick={() => setIsMoreOpen((current) => !current)}>•••</ReelAction>{isMoreOpen && <button type="button" onClick={() => void saveReel()} className="absolute bottom-0 right-12 whitespace-nowrap rounded-md border-0 bg-surface px-3 py-2 text-xs font-bold text-text shadow-lg cursor-pointer">{isSaved ? 'Bỏ lưu Reel' : 'Lưu Reel'}</button>}</div>
      </div>
      </div>
      {commentsOpen && <button type="button" className="reel-comments-backdrop" aria-label={t('closeComments')} onClick={() => setIsCommentsOpen(false)} />}
      <div className="reel-comments-shell" aria-hidden={!commentsOpen} inert={!commentsOpen}>
        <aside id={commentsId} aria-labelledby={`${commentsId}-title`} className="reel-comments flex flex-col overflow-hidden rounded-xl border border-border bg-surface text-text shadow-2xl">
          <header className="flex shrink-0 items-center justify-between border-b border-border px-4 py-3">
            <div className="flex items-center gap-2"><h2 id={`${commentsId}-title`} className="text-lg font-bold">{t('reelComments')}</h2><span className="text-sm text-text-muted">{reel.commentCount.toLocaleString()}</span></div>
            <button type="button" onClick={() => { setIsCommentsOpen(false); commentButtonRef.current?.focus() }} aria-label={t('closeComments')} className="grid h-9 w-9 place-items-center rounded-full text-2xl text-text-muted transition-colors hover:bg-surface-2 hover:text-text focus-visible:outline-2 focus-visible:outline-primary">×</button>
          </header>
          <div className="min-h-0 flex-1 space-y-5 overflow-y-auto overscroll-contain px-4 py-4" aria-busy={isLoadingComments}>
            {isLoadingComments && <p className="py-5 text-center text-sm text-text-muted" role="status">{t('loading')}</p>}
            {commentsError && <div className="text-center text-sm text-danger" role="alert"><p>{commentsError}</p><button type="button" onClick={() => void loadComments()} className="mt-2 rounded-lg border border-border px-3 py-1.5 text-text hover:bg-surface-2">{t('retry')}</button></div>}
            {comments.map((comment) => {
              const authorName = comment.author?.displayName ?? t('user')
              const timestamp = formatPostTimestamp(comment.createdAtUtc)
              return <article key={comment.id} className="flex items-start gap-3">
                <Link to={`/profile/${comment.authorUserId}`} aria-label={authorName} className="flex h-8 w-8 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-xs font-bold text-white no-underline">{comment.author?.avatarUrl ? <img src={resolveProfileImageUrl(comment.author.avatarUrl)} alt="" loading="lazy" className="h-full w-full object-cover" /> : authorName.slice(0, 2).toUpperCase()}</Link>
                <div className="min-w-0 flex-1">
                  <div className="mb-1 flex flex-wrap items-baseline gap-x-2 gap-y-0.5"><Link to={`/profile/${comment.authorUserId}`} className="text-xs font-semibold text-text no-underline hover:underline">{authorName}</Link><time dateTime={comment.createdAtUtc} title={timestamp.absolute} className="text-[11px] text-text-muted">{timestamp.compact}</time></div>
                  <TextWithReferences content={comment.content} mentions={comment.mentions} className="whitespace-pre-wrap break-words text-sm leading-relaxed text-text" />
                </div>
              </article>
            })}
            {!isLoadingComments && !commentsError && comments.length === 0 && <p className="py-8 text-center text-sm text-text-muted">{t('noCommentsYet')}</p>}
          </div>
          {commentSaveError && <p role="alert" className="px-4 pb-2 text-sm text-danger">{commentSaveError}</p>}
          <CommentComposer currentUserProfile={currentUserProfile} currentUserName={currentUserProfile?.displayName ?? session?.user.username ?? t('user')} value={commentText} placeholder={t('writeComment')} sendLabel={isSavingComment ? t('sending') : t('send')} isSubmitting={isSavingComment} onChange={(event) => setCommentText(event.target.value)} onSubmit={(event) => { event.preventDefault(); void submitComment() }} />
        </aside>
      </div>
      </div>
      {isShareOpen && <ShareDialog postId={reel.id} onClose={() => setIsShareOpen(false)} />}
    </section>
  )
}

function CreateReelDialog({ onClose, onCreated }: { onClose: () => void; onCreated: (reel: Reel) => void }) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [media, setMedia] = useState<Media | null>(null)
  const [caption, setCaption] = useState('')
  const [privacy, setPrivacy] = useState<'public' | 'friends' | 'onlyMe'>('public')
  const [progress, setProgress] = useState(0)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [posterUrl, setPosterUrl] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isUploading, setIsUploading] = useState(false)
  const [isPublishing, setIsPublishing] = useState(false)

  useEffect(() => {
    if (media?.status !== 'Processing') return
    const intervalId = window.setInterval(() => {
      void mediaApi.getMetadata(media.id).then(setMedia).catch(() => setError('Không thể kiểm tra trạng thái xử lý video.'))
    }, 2_000)
    return () => window.clearInterval(intervalId)
  }, [media?.id, media?.status])

  useEffect(() => {
    if (media?.status !== 'Ready' || !media.hasProcessedVideo) return
    let disposed = false
    Promise.all([mediaApi.getReadUrl(media.id), mediaApi.getPosterReadUrl(media.id)])
      .then(([video, poster]) => { if (!disposed) { setPreviewUrl(video.url); setPosterUrl(poster.url) } })
      .catch(() => { if (!disposed) setError('Không thể tạo bản xem trước riêng tư.') })
    return () => { disposed = true }
  }, [media?.hasProcessedVideo, media?.id, media?.status])

  const chooseFile = async (file: File | undefined) => {
    if (!file) return
    if (!supportedVideoTypes.has(file.type) || file.size <= 0 || file.size > maximumVideoBytes) {
      setError('Chỉ hỗ trợ MP4/WebM tối đa 500 MB.')
      return
    }
    setError(null)
    setMedia(null)
    setPreviewUrl(null)
    setPosterUrl(null)
    setIsUploading(true)
    try {
      setMedia(await mediaApi.uploadFileWithMetadata(file, setProgress))
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Không thể tải video lên.')
    } finally {
      setIsUploading(false)
    }
  }

  const publish = async () => {
    if (!media || media.status !== 'Ready' || !media.hasProcessedVideo) return
    setIsPublishing(true)
    setError(null)
    try {
      onCreated(await reelsApi.create({ caption: caption.trim(), privacy, videoMediaId: media.id }))
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Không thể xuất bản Reel.')
    } finally {
      setIsPublishing(false)
    }
  }

  const status = isUploading ? `Đang tải lên ${progress}%` : media?.status === 'Processing' ? 'Đang xử lý video…' : media?.status === 'Failed' ? 'Xử lý video thất bại. Hãy chọn video khác.' : media?.status === 'Ready' ? 'Video đã sẵn sàng để xuất bản.' : 'Chọn một video MP4 hoặc WebM.'
  return <div className="fixed inset-0 z-[70] grid place-items-center bg-black/75 p-3"><div role="dialog" aria-modal="true" aria-label="Tạo Reel" className="max-h-[calc(100dvh-1.5rem)] w-full max-w-xl overflow-y-auto rounded-2xl border border-border bg-surface p-5 shadow-2xl"><div className="flex items-center justify-between"><h1 className="text-lg font-bold text-text">Tạo Reel</h1><button type="button" onClick={onClose} aria-label="Đóng" disabled={isUploading || isPublishing} className="border-0 bg-transparent text-lg text-text-muted">✕</button></div><div className="mt-4 rounded-xl border border-dashed border-border bg-surface-2 p-4 text-center"><input ref={inputRef} type="file" accept="video/mp4,video/webm" className="hidden" onChange={(event) => { void chooseFile(event.target.files?.[0]); event.target.value = '' }} /><button type="button" onClick={() => inputRef.current?.click()} disabled={isUploading || isPublishing} className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white">Chọn video</button><p className="mt-3 text-sm text-text-muted">{status}</p>{previewUrl && <video src={previewUrl} poster={posterUrl ?? undefined} controls className="mt-3 max-h-64 w-full rounded-lg bg-black" />}</div><textarea value={caption} maxLength={10_000} onChange={(event) => setCaption(event.target.value)} placeholder="Thêm chú thích (không bắt buộc)" className="mt-4 min-h-24 w-full rounded-lg border border-border bg-surface-2 p-3 text-sm text-text outline-none" /><select value={privacy} onChange={(event) => setPrivacy(event.target.value as typeof privacy)} className="mt-3 rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text"><option value="public">Công khai</option><option value="friends">Bạn bè</option><option value="onlyMe">Chỉ mình tôi</option></select>{error && <p className="mt-3 text-sm text-[#ff8a9b]">{error}</p>}<button type="button" onClick={() => void publish()} disabled={media?.status !== 'Ready' || !media.hasProcessedVideo || isPublishing} className="mt-4 w-full rounded-lg bg-primary py-2.5 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-40">{isPublishing ? 'Đang xuất bản…' : 'Xuất bản Reel'}</button></div></div>
}
