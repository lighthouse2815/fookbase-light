import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { postsApi, type MediaAccess } from '../../../api/posts'
import { resolveProfileImageUrl } from '../../../api/users'
import { getNeighborImageUrls } from './postPhotoLightbox'
import './postLightbox.css'

interface Props {
  postId: string
  media: readonly MediaAccess[]
  currentIndex: number
  onPrevious: () => void
  onNext: () => void
  onClose: () => void
  onMediaResolved: (updatedMedia: MediaAccess) => void
  altText: string
}

interface MediaView {
  mediaId: string
  url: string
  attempt: number
  status: 'loading' | 'ready' | 'error'
}

function needsFreshAccess(item: MediaAccess) {
  return !item.url.trim() || Date.parse(item.expiresAtUtc) <= Date.now()
}

function initialView(item: MediaAccess | undefined): MediaView {
  return { mediaId: item?.mediaId ?? '', url: item && !needsFreshAccess(item) ? resolveProfileImageUrl(item.url) : '', attempt: 0, status: 'loading' }
}

export default function PostLightboxMedia({ postId, media, currentIndex, onPrevious, onNext, onClose, onMediaResolved, altText }: Props) {
  const current = media[currentIndex]
  const [view, setView] = useState<MediaView>(() => initialView(current))
  const currentRef = useRef(current)
  const viewRef = useRef(view)
  const postIdRef = useRef(postId)
  const onResolvedRef = useRef(onMediaResolved)
  const generationRef = useRef(0)
  const mountedRef = useRef(false)
  const automaticallyRefreshedRef = useRef(false)
  const accessRequestsRef = useRef(new Map<string, Promise<MediaAccess>>())
  const imageRef = useRef<HTMLImageElement>(null)
  const videoRef = useRef<HTMLVideoElement>(null)
  useLayoutEffect(() => {
    currentRef.current = current
    viewRef.current = view
    postIdRef.current = postId
    onResolvedRef.current = onMediaResolved
  }, [current, view, postId, onMediaResolved])
  const activeView = view.mediaId === current?.mediaId ? view : null
  const isLoading = !activeView || activeView.status === 'loading'
  const isVideo = current?.mediaType === 'video'
  const mediaLabel = isVideo ? 'video' : 'ảnh'

  useEffect(() => {
    mountedRef.current = true
    return () => { mountedRef.current = false; generationRef.current += 1 }
  }, [])

  const refreshAccess = useCallback((item: MediaAccess, generation: number) => {
    setView((previous) => previous.mediaId === item.mediaId
      ? { ...previous, url: '', attempt: previous.attempt + 1, status: 'loading' }
      : previous)
    const requestKey = `${postId}:${item.mediaId}`
    let request = accessRequestsRef.current.get(requestKey)
    if (!request) {
      request = postsApi.getMediaAccess(postId, item.mediaId)
      accessRequestsRef.current.set(requestKey, request)
      const finished = () => { accessRequestsRef.current.delete(requestKey) }
      void request.then(finished, finished)
    }
    void request.then((resolved) => {
      if (!mountedRef.current || postIdRef.current !== postId) return
      onResolvedRef.current(resolved)
      if (generationRef.current !== generation || currentRef.current?.mediaId !== item.mediaId) return
      setView((previous) => ({ ...previous, mediaId: item.mediaId, url: resolveProfileImageUrl(resolved.url), status: resolved.url.trim() ? 'loading' : 'error' }))
    }).catch(() => {
      if (!mountedRef.current || generationRef.current !== generation || currentRef.current?.mediaId !== item.mediaId) return
      setView((previous) => ({ ...previous, status: 'error' }))
    })
  }, [postId])

  useEffect(() => {
    const generation = ++generationRef.current
    automaticallyRefreshedRef.current = false
    const item = currentRef.current
    setView(initialView(item))
    if (item && needsFreshAccess(item)) {
      automaticallyRefreshedRef.current = true
      refreshAccess(item, generation)
    }
    return () => { generationRef.current += 1 }
  }, [postId, current?.mediaId, refreshAccess])

  const previousNeighbor = media[currentIndex - 1]
  const nextNeighbor = media[currentIndex + 1]
  const neighborUrls = getNeighborImageUrls([
    previousNeighbor && !needsFreshAccess(previousNeighbor) ? previousNeighbor : undefined,
    current,
    nextNeighbor && !needsFreshAccess(nextNeighbor) ? nextNeighbor : undefined,
  ], 1)
  const previousNeighborUrl = neighborUrls[0]
  const nextNeighborUrl = neighborUrls[1]
  useEffect(() => {
    const images = [previousNeighborUrl, nextNeighborUrl].filter((url): url is string => Boolean(url)).map((url) => {
      const image = new Image()
      image.decoding = 'async'
      image.src = resolveProfileImageUrl(url)
      return image
    })
    return () => images.forEach((image) => { image.onload = null; image.onerror = null; image.removeAttribute('src') })
  }, [previousNeighborUrl, nextNeighborUrl, current?.mediaId])

  useEffect(() => {
    const video = videoRef.current
    return () => { video?.pause() }
  }, [current?.mediaId, activeView?.url, activeView?.attempt])

  useEffect(() => {
    const image = imageRef.current
    if (activeView?.status === 'loading' && image?.complete && image.naturalWidth > 0) {
      setView((previous) => ({ ...previous, status: 'ready' }))
    }
  }, [activeView?.mediaId, activeView?.url, activeView?.attempt, activeView?.status])

  const matchesCurrentSource = (element: HTMLImageElement | HTMLVideoElement) => Boolean(activeView &&
    currentRef.current?.mediaId === activeView.mediaId && viewRef.current.mediaId === activeView.mediaId &&
    viewRef.current.url === activeView.url && viewRef.current.attempt === activeView.attempt &&
    (element === imageRef.current || element === videoRef.current))
  const loaded = (element: HTMLImageElement | HTMLVideoElement) => {
    if (matchesCurrentSource(element)) setView((previous) => ({ ...previous, status: 'ready' }))
  }
  const failed = (element: HTMLImageElement | HTMLVideoElement) => {
    if (!matchesCurrentSource(element) || !currentRef.current) return
    if (!automaticallyRefreshedRef.current) {
      automaticallyRefreshedRef.current = true
      refreshAccess(currentRef.current, generationRef.current)
    } else setView((previous) => ({ ...previous, status: 'error' }))
  }
  const retry = () => {
    const item = currentRef.current
    if (!item || isLoading) return
    automaticallyRefreshedRef.current = true
    refreshAccess(item, generationRef.current)
  }

  return <div className="post-lightbox-media relative min-h-0 flex-1 overflow-hidden bg-black text-white">
    <div data-photo-stage data-lightbox-media-id={current?.mediaId} data-photo-index={currentIndex} data-photo-scale={1} aria-busy={isLoading} className="absolute inset-0 flex items-center justify-center overflow-hidden p-4 md:p-8">
      {activeView?.url && (isVideo ? <video key={`${activeView.mediaId}:${activeView.url}:${activeView.attempt}`} ref={videoRef} src={activeView.url} controls preload="metadata" playsInline aria-label={altText} onLoadedMetadata={(event) => loaded(event.currentTarget)} onError={(event) => failed(event.currentTarget)} className={`max-h-full max-w-full object-contain ${activeView.status === 'ready' ? 'post-lightbox-loaded' : 'opacity-0'}`} />
        : <img key={`${activeView.mediaId}:${activeView.url}:${activeView.attempt}`} ref={imageRef} data-photo-media-id={current?.mediaId} src={activeView.url} alt={altText} draggable={false} decoding="async" onLoad={(event) => loaded(event.currentTarget)} onError={(event) => failed(event.currentTarget)} className={`max-h-full max-w-full select-none object-contain ${activeView.status === 'ready' ? 'post-lightbox-loaded' : 'opacity-0'}`} />)}
      {isLoading && <div role="status" aria-label={`Đang tải ${mediaLabel}`} className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center gap-3 text-sm text-white/75"><span aria-hidden="true" className="h-14 w-14 rounded-xl border border-white/20 bg-white/5" /><span>Đang tải {mediaLabel}…</span></div>}
      {activeView?.status === 'error' && <div role="alert" className="absolute inset-0 flex flex-col items-center justify-center gap-3 px-16 text-center"><span aria-hidden="true" className="grid h-12 w-12 place-items-center rounded-full bg-white/10 text-xl">!</span><p className="font-semibold">Không thể tải {mediaLabel}</p><p className="text-sm text-white/65">Nội dung có thể không còn khả dụng. Bạn có thể thử tải lại.</p><button type="button" onClick={retry} className="post-lightbox-control min-h-11 rounded-full border border-white/30 bg-white/15 px-5 py-2 text-sm font-semibold hover:bg-white/25">Thử lại</button></div>}
    </div>
    <button type="button" data-dialog-initial-focus onClick={onClose} aria-label="Đóng ảnh" className="post-lightbox-control absolute left-3 top-3 z-10 grid h-11 w-11 place-items-center rounded-full border-0 bg-black/55 text-2xl leading-none hover:bg-black/80">×</button>
    {media.length > 1 && <>
      <button type="button" onClick={() => { if (currentIndex > 0) onPrevious() }} aria-label="Ảnh trước" aria-disabled={currentIndex <= 0} className="post-lightbox-control absolute left-3 top-1/2 z-10 grid h-11 w-11 -translate-y-1/2 place-items-center rounded-full border-0 bg-black/55 text-3xl leading-none hover:bg-black/80 aria-disabled:cursor-default aria-disabled:opacity-35">‹</button>
      <button type="button" onClick={() => { if (currentIndex < media.length - 1) onNext() }} aria-label="Ảnh tiếp theo" aria-disabled={currentIndex >= media.length - 1} className="post-lightbox-control absolute right-3 top-1/2 z-10 grid h-11 w-11 -translate-y-1/2 place-items-center rounded-full border-0 bg-black/55 text-3xl leading-none hover:bg-black/80 aria-disabled:cursor-default aria-disabled:opacity-35">›</button>
      <span role="status" aria-label={`Ảnh ${currentIndex + 1} trên ${media.length}`} className="pointer-events-none absolute bottom-4 left-1/2 -translate-x-1/2 rounded-full bg-black/55 px-3 py-1.5 text-xs font-medium tabular-nums">{currentIndex + 1} / {media.length}</span>
    </>}
  </div>
}
