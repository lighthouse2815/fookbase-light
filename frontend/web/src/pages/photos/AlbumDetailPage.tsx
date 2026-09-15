import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { photosApi, type AlbumPhoto, type PhotoAlbum } from '../../api/photos'
import AppDialog from '../../shared/components/AppDialog'
import PhotoViewer from './PhotoViewer'

export default function AlbumDetailPage() {
  const { albumId = '' } = useParams()
  const [album, setAlbum] = useState<PhotoAlbum | null>(null)
  const [items, setItems] = useState<AlbumPhoto[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [selected, setSelected] = useState<number | null>(null)
  const [detail, setDetail] = useState<Awaited<ReturnType<typeof photosApi.photo>> | null>(null)
  const [captionTarget, setCaptionTarget] = useState<AlbumPhoto | null>(null)
  const [captionDraft, setCaptionDraft] = useState('')
  const loadMedia = useCallback(async (next?: string) => { const page = await photosApi.media(albumId, next); setItems((current) => next ? [...current, ...page.items] : page.items); setCursor(page.nextCursor) }, [albumId])
  const loadInitial = useCallback(async () => { const [nextAlbum, page] = await Promise.all([photosApi.get(albumId), photosApi.media(albumId)]); setAlbum(nextAlbum); setItems(page.items); setCursor(page.nextCursor) }, [albumId])
  useEffect(() => { const timer = window.setTimeout(() => { void loadInitial() }, 0); return () => window.clearTimeout(timer) }, [loadInitial])
  useEffect(() => { if (selected !== null) void photosApi.photo(albumId, items[selected].mediaId).then(setDetail) }, [albumId, items, selected])
  if (!album) return <main className="p-5 text-text-muted">Đang tải album…</main>
  const choose = (index: number) => { setDetail(null); setSelected(index) }
  const remove = async (mediaId: string) => { await photosApi.removeMedia(albumId, mediaId); await loadMedia() }
  const saveCaption = async () => {
    if (!captionTarget) return
    await photosApi.updateCaption(albumId, captionTarget.mediaId, captionDraft.trim())
    setCaptionTarget(null)
    setCaptionDraft('')
    await loadMedia()
  }
  return <main className="mx-auto max-w-5xl p-5"><Link to="/photos" className="text-primary">← Ảnh</Link><h1 className="mt-3 font-heading text-3xl font-bold text-text">{album.name}</h1><p className="text-text-muted">{album.description}</p><div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-3">{items.map((item, index) => <div key={item.mediaId}><button type="button" onClick={() => choose(index)} aria-label="Xem ảnh"><img src={item.accessUrl} alt={item.caption ?? ''} loading="lazy" decoding="async" className="aspect-square w-full rounded-lg object-cover" /></button>{album.canManage && <div className="flex gap-2"><button type="button" onClick={() => { setCaptionTarget(item); setCaptionDraft(item.caption ?? '') }} className="text-xs text-primary">Chú thích</button><button type="button" onClick={() => void remove(item.mediaId)} className="text-xs text-red-400">Xóa</button></div>}</div>)}</div>{cursor && <button type="button" onClick={() => void loadMedia(cursor)} className="mt-5 rounded bg-surface-2 px-4 py-2 text-text">Xem thêm</button>}{selected !== null && <PhotoViewer item={items[selected]} detail={detail} onClose={() => setSelected(null)} onPrevious={() => choose(Math.max(0, selected - 1))} onNext={() => choose(Math.min(items.length - 1, selected + 1))} hasPrevious={selected > 0} hasNext={selected < items.length - 1} />}{captionTarget && <AppDialog title="Chỉnh sửa chú thích" onClose={() => { setCaptionTarget(null); setCaptionDraft('') }}><form onSubmit={(event) => { event.preventDefault(); void saveCaption() }}><label className="mt-4 block text-sm font-semibold text-text">Chú thích ảnh<textarea data-dialog-initial-focus value={captionDraft} onChange={(event) => setCaptionDraft(event.target.value)} maxLength={1_000} rows={4} className="mt-1.5 w-full resize-y rounded-lg border border-border bg-surface-2 p-3 text-sm text-text outline-none focus:border-primary" /></label><div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => { setCaptionTarget(null); setCaptionDraft('') }} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button type="submit" className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white">Lưu</button></div></form></AppDialog>}</main>
}
