import { useEffect, useRef, useState } from 'react'
import type { AlbumPhoto, PhotoDetail } from '../../api/photos'
import { useDialogFocus } from '../../shared/useDialogFocus'

interface Props { item: AlbumPhoto; detail: PhotoDetail | null; loading: boolean; error: string; onRetry: () => void; onClose: () => void; onPrevious: () => void; onNext: () => void; hasPrevious: boolean; hasNext: boolean }
export default function PhotoViewer({ item, detail, loading, error, onRetry, onClose, onPrevious, onNext, hasPrevious, hasNext }: Props) {
  const dialogRef = useRef<HTMLDivElement>(null)
  const [failedUrl, setFailedUrl] = useState<string | null>(null)
  const url = detail?.url ?? item.accessUrl
  useDialogFocus(true, dialogRef, onClose)
  useEffect(() => {
    const listener = (event: KeyboardEvent) => {
      if (event.defaultPrevented || !dialogRef.current?.contains(document.activeElement)) return
      if (event.key === 'ArrowLeft' && hasPrevious) { event.preventDefault(); onPrevious() }
      if (event.key === 'ArrowRight' && hasNext) { event.preventDefault(); onNext() }
    }
    window.addEventListener('keydown', listener)
    return () => window.removeEventListener('keydown', listener)
  }, [onPrevious, onNext, hasPrevious, hasNext])
  return <div ref={dialogRef} tabIndex={-1} role="dialog" aria-label="Xem ảnh album" aria-modal="true" className="fixed inset-0 z-50 grid place-items-center bg-black/85 p-4">
    <button aria-label="Đóng ảnh" onClick={onClose} className="absolute right-5 top-5 text-2xl text-white">×</button>
    <div className="flex max-h-full max-w-5xl items-center gap-3">
      <button aria-label="Ảnh trước" disabled={!hasPrevious} onClick={onPrevious} className="rounded bg-white/15 px-3 py-2 text-white disabled:opacity-30">←</button>
      <div>
        {failedUrl !== url && <img src={url} onError={() => setFailedUrl(url)} alt={item.caption ?? ''} className="max-h-[75vh] max-w-[80vw] rounded object-contain" />}
        {loading && <p role="status" className="mt-3 text-center text-white">Đang tải ảnh đầy đủ…</p>}
        {(error || failedUrl === url) && <div role="alert" className="mt-3 text-center text-white">{error || 'Không thể hiển thị ảnh.'} <button type="button" disabled={loading} onClick={() => { setFailedUrl(null); onRetry() }} className="underline">Thử lại</button></div>}
        {item.caption && <p className="mt-3 text-center text-white">{item.caption}</p>}
      </div>
      <button aria-label="Ảnh tiếp" disabled={!hasNext} onClick={onNext} className="rounded bg-white/15 px-3 py-2 text-white disabled:opacity-30">→</button>
    </div>
  </div>
}
