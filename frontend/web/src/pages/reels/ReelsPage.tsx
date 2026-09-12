import { useCallback, useEffect, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { mediaApi, type Media } from '../../api/media'
import { postsApi, type Comment } from '../../api/posts'
import { reelsApi, type Reel } from '../../api/reels'
import TextWithReferences from '../../shared/components/TextWithReferences'
import ShareDialog from '../feed/components/ShareDialog'

const supportedVideoTypes = new Set(['video/mp4', 'video/webm'])
const maximumVideoBytes = 500 * 1024 * 1024

export default function ReelsPage() {
  const [searchParams] = useSearchParams()
  const requestedReelId = searchParams.get('reel')
  const [reels, setReels] = useState<Reel[]>([])
  const [activeIndex, setActiveIndex] = useState(0)
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const listRef = useRef<HTMLDivElement>(null)

  const load = useCallback(async (cursor?: string, append = false) => {
    if (append) setIsLoadingMore(true)
    else setIsLoading(true)
    setError(null)
    try {
      const page = requestedReelId && !cursor
        ? { items: [await reelsApi.get(requestedReelId)], nextCursor: null }
        : await reelsApi.getFeed(cursor)
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
  }, [requestedReelId])

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
    <div className="relative h-[calc(100vh-56px)] overflow-hidden bg-[#0d0e10]">
      <div ref={listRef} className="h-full snap-y snap-mandatory overflow-y-auto scroll-smooth">
        {isLoading && <div className="grid h-full place-items-center text-sm text-text-muted">Đang tải Reels…</div>}
        {error && <div className="grid h-full place-items-center p-6"><div className="max-w-md rounded-xl border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-4 text-sm text-[#ff8a9b]"><p>{error}</p><button type="button" onClick={() => void load()} className="mt-3 rounded-lg bg-primary px-3 py-2 text-white">Thử lại</button></div></div>}
        {!isLoading && !error && reels.length === 0 && <div className="grid h-full place-items-center text-center"><div><p className="text-lg font-semibold text-text">Chưa có Reel để xem</p><button type="button" onClick={() => setIsCreateOpen(true)} className="mt-3 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white">Tạo Reel đầu tiên</button></div></div>}
        {reels.map((reel, index) => (
          <ReelCard
            key={reel.id}
            reel={reel}
            active={activeIndex === index}
            shouldPreload={Math.abs(activeIndex - index) <= 1}
            index={index}
            onActivate={() => setActiveIndex(index)}
            onUpdated={updateReel}
          />
        ))}
        {isLoadingMore && <div className="py-4 text-center text-xs text-text-muted">Đang tải thêm…</div>}
      </div>

      {reels.length > 0 && <div className="absolute right-3 top-1/2 z-10 flex -translate-y-1/2 flex-col gap-2">
        <button type="button" disabled={activeIndex === 0} onClick={() => selectIndex(activeIndex - 1)} className="h-10 w-10 rounded-full border-0 bg-surface/90 text-xl text-text shadow-lg disabled:opacity-30" aria-label="Reel trước">↑</button>
        <button type="button" disabled={activeIndex >= reels.length - 1} onClick={() => selectIndex(activeIndex + 1)} className="h-10 w-10 rounded-full border-0 bg-surface/90 text-xl text-text shadow-lg disabled:opacity-30" aria-label="Reel tiếp theo">↓</button>
        {nextCursor && activeIndex >= reels.length - 1 && <button type="button" disabled={isLoadingMore} onClick={() => void load(nextCursor, true)} className="rounded-full bg-primary px-3 py-2 text-xs font-semibold text-white disabled:opacity-50">Thêm</button>}
      </div>}
      <button type="button" onClick={() => setIsCreateOpen(true)} className="absolute right-4 top-4 z-10 rounded-full bg-primary px-4 py-2 text-sm font-semibold text-white shadow-lg">＋ Tạo Reel</button>
      {isCreateOpen && <CreateReelDialog onClose={() => setIsCreateOpen(false)} onCreated={(reel) => { setReels((current) => [reel, ...current]); setActiveIndex(0); setIsCreateOpen(false) }} />}
    </div>
  )
}

function ReelCard({ reel, active, shouldPreload, index, onActivate, onUpdated }: {
  reel: Reel
  active: boolean
  shouldPreload: boolean
  index: number
  onActivate: () => void
  onUpdated: (reel: Reel) => void
}) {
  const cardRef = useRef<HTMLElement>(null)
  const videoRef = useRef<HTMLVideoElement>(null)
  const [videoUrl, setVideoUrl] = useState<string | null>(null)
  const [posterUrl, setPosterUrl] = useState<string | null>(null)
  const [isMuted, setIsMuted] = useState(true)
  const [isPaused, setIsPaused] = useState(false)
  const [isCommentsOpen, setIsCommentsOpen] = useState(false)
  const [comments, setComments] = useState<Comment[]>([])
  const [commentText, setCommentText] = useState('')
  const [hasRecordedThreshold, setHasRecordedThreshold] = useState(false)
  const [hasRecordedCompletion, setHasRecordedCompletion] = useState(false)
  const [isSavingComment, setIsSavingComment] = useState(false)
  const [currentMs, setCurrentMs] = useState(0)
  const [isSaved, setIsSaved] = useState(false)
  const [isShareOpen, setIsShareOpen] = useState(false)

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

  const openComments = async () => {
    setIsCommentsOpen((current) => !current)
    if (!isCommentsOpen) {
      try {
        const page = await reelsApi.getComments(reel.id)
        setComments(page.items)
      } catch {
        setComments([])
      }
    }
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

  const submitComment = async () => {
    const content = commentText.trim()
    if (!content || isSavingComment) return
    setIsSavingComment(true)
    try {
      const created = await postsApi.createComment(reel.id, content)
      setComments((current) => [...current, created])
      setCommentText('')
      onUpdated({ ...reel, commentCount: reel.commentCount + 1 })
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
  return (
    <section ref={cardRef} data-reel-index={index} className="relative flex min-h-full snap-start items-center justify-center bg-black px-2 py-3 sm:px-4" onMouseEnter={onActivate}>
      <div className="relative h-[min(92vh,900px)] w-full max-w-[560px] overflow-hidden rounded-2xl bg-surface shadow-2xl">
        {videoUrl ? <video ref={videoRef} src={videoUrl} poster={posterUrl ?? undefined} muted={isMuted} playsInline className="h-full w-full object-contain" onPlay={() => { onActivate(); setIsPaused(false) }} onTimeUpdate={() => { const milliseconds = Math.floor((videoRef.current?.currentTime ?? 0) * 1000); setCurrentMs(milliseconds); const threshold = Math.min(3_000, reel.video.durationMs * 0.25); if (milliseconds >= threshold) recordThreshold(false) }} onEnded={() => { setIsPaused(true); recordThreshold(true) }} /> : <div className="grid h-full place-items-center text-sm text-text-muted">Đang chuẩn bị video…</div>}
        <button type="button" onClick={() => { const video = videoRef.current; if (!video) return; if (video.paused) { if (video.ended) video.currentTime = 0; void video.play(); setIsPaused(false) } else { video.pause(); setIsPaused(true) } }} className="absolute inset-0 border-0 bg-transparent" aria-label={isPaused ? 'Phát Reel' : 'Tạm dừng Reel'} />
        <div className="absolute inset-x-0 bottom-0 bg-linear-to-t from-black/90 via-black/45 to-transparent px-4 pb-4 pt-20 text-white pointer-events-none">
          <div className="flex items-end gap-3 pointer-events-auto">
            <div className="h-10 w-10 shrink-0 rounded-full bg-primary text-center text-xs font-bold leading-10">{initial}</div>
            <div className="min-w-0 flex-1"><p className="font-semibold">{reel.author.displayName} <span className="font-normal text-white/70">@{reel.author.username}</span></p>{reel.caption && <TextWithReferences content={reel.caption} mentions={reel.mentions} className="mt-1 whitespace-pre-wrap text-sm leading-relaxed" />}<p className="mt-2 text-xs text-white/70">{reel.viewCount.toLocaleString()} lượt xem</p></div>
          </div>
          <div className="mt-3 flex items-center gap-2 pointer-events-auto">
            <button type="button" onClick={toggleReaction} className="rounded-full bg-white/15 px-3 py-2 text-sm text-white">{reel.viewerReaction ? '♥ Đã thích' : '♡ Thích'} {reel.reactionCount > 0 ? reel.reactionCount : ''}</button>
            <button type="button" onClick={() => void openComments()} className="rounded-full bg-white/15 px-3 py-2 text-sm text-white">💬 {reel.commentCount}</button>
            <button type="button" onClick={() => void saveReel()} className="rounded-full bg-white/15 px-3 py-2 text-sm text-white">🔖 {isSaved ? 'Đã lưu' : 'Lưu'}</button>
            <button type="button" onClick={() => setIsShareOpen(true)} className="rounded-full bg-white/15 px-3 py-2 text-sm text-white">↗ Chia sẻ</button>
            <button type="button" onClick={() => setIsMuted((current) => !current)} className="ml-auto rounded-full bg-white/15 px-3 py-2 text-sm text-white">{isMuted ? '🔇' : '🔊'}</button>
          </div>
          <input aria-label="Tiến trình Reel" type="range" min="0" max={reel.video.durationMs} value={Math.min(currentMs, reel.video.durationMs)} onChange={(event) => { const next = Number(event.target.value); if (videoRef.current) videoRef.current.currentTime = next / 1000; setCurrentMs(next) }} className="mt-3 w-full accent-primary pointer-events-auto" />
        </div>
      </div>
      {isCommentsOpen && <aside className="absolute bottom-4 left-1/2 z-20 w-[min(34rem,calc(100%-1rem))] -translate-x-1/2 rounded-xl border border-border bg-surface p-3 shadow-2xl"><div className="flex items-center justify-between"><h2 className="font-semibold text-text">Bình luận ({reel.commentCount})</h2><button type="button" onClick={() => setIsCommentsOpen(false)} className="border-0 bg-transparent text-text-muted">✕</button></div><div className="mt-2 max-h-44 space-y-2 overflow-y-auto">{comments.map((comment) => <TextWithReferences key={comment.id} content={comment.content} mentions={comment.mentions} className="block rounded-lg bg-surface-2 px-3 py-2 text-sm text-text" />)}{comments.length === 0 && <p className="py-2 text-sm text-text-muted">Chưa có bình luận.</p>}</div><div className="mt-3 flex gap-2"><input value={commentText} onChange={(event) => setCommentText(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') void submitComment() }} placeholder="Viết bình luận…" className="min-w-0 flex-1 rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none" /><button type="button" disabled={!commentText.trim() || isSavingComment} onClick={() => void submitComment()} className="rounded-lg bg-primary px-3 text-sm font-semibold text-white disabled:opacity-40">Gửi</button></div></aside>}
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
  return <div className="fixed inset-0 z-50 grid place-items-center bg-black/75 p-3"><div className="w-full max-w-xl rounded-2xl border border-border bg-surface p-5 shadow-2xl"><div className="flex items-center justify-between"><h1 className="text-lg font-bold text-text">Tạo Reel</h1><button type="button" onClick={onClose} disabled={isUploading || isPublishing} className="border-0 bg-transparent text-lg text-text-muted">✕</button></div><div className="mt-4 rounded-xl border border-dashed border-border bg-surface-2 p-4 text-center"><input ref={inputRef} type="file" accept="video/mp4,video/webm" className="hidden" onChange={(event) => { void chooseFile(event.target.files?.[0]); event.target.value = '' }} /><button type="button" onClick={() => inputRef.current?.click()} disabled={isUploading || isPublishing} className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white">Chọn video</button><p className="mt-3 text-sm text-text-muted">{status}</p>{previewUrl && <video src={previewUrl} poster={posterUrl ?? undefined} controls className="mt-3 max-h-64 w-full rounded-lg bg-black" />}</div><textarea value={caption} maxLength={10_000} onChange={(event) => setCaption(event.target.value)} placeholder="Thêm chú thích (không bắt buộc)" className="mt-4 min-h-24 w-full rounded-lg border border-border bg-surface-2 p-3 text-sm text-text outline-none" /><select value={privacy} onChange={(event) => setPrivacy(event.target.value as typeof privacy)} className="mt-3 rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text"><option value="public">Công khai</option><option value="friends">Bạn bè</option><option value="onlyMe">Chỉ mình tôi</option></select>{error && <p className="mt-3 text-sm text-[#ff8a9b]">{error}</p>}<button type="button" onClick={() => void publish()} disabled={media?.status !== 'Ready' || !media.hasProcessedVideo || isPublishing} className="mt-4 w-full rounded-lg bg-primary py-2.5 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-40">{isPublishing ? 'Đang xuất bản…' : 'Xuất bản Reel'}</button></div></div>
}
