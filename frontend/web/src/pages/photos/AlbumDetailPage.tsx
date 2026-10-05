import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { photosApi, type AlbumPhoto, type PhotoAlbum, type PhotoDetail } from '../../api/photos'
import { ApiError } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import AppDialog from '../../shared/components/AppDialog'
import PhotoViewer from './PhotoViewer'

export default function AlbumDetailPage() {
  const { albumId = '' } = useParams()
  const { session } = useAuth()
  return <AlbumContent key={`${session?.user.id}:${albumId}`} albumId={albumId} />
}

function AlbumContent({ albumId }: { albumId: string }) {
  const [album, setAlbum] = useState<PhotoAlbum | null>(null)
  const [items, setItems] = useState<AlbumPhoto[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [retryCursor, setRetryCursor] = useState<string | undefined>()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<PhotoDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [detailError, setDetailError] = useState('')
  const [captionTarget, setCaptionTarget] = useState<AlbumPhoto | null>(null)
  const [captionDraft, setCaptionDraft] = useState('')
  const [deleteTarget, setDeleteTarget] = useState<AlbumPhoto | null>(null)
  const [writeError, setWriteError] = useState('')
  const [pending, setPending] = useState(false)
  const active = useRef(false)
  const readId = useRef(0)
  const detailId = useRef(0)
  const pendingRef = useRef(false)
  const uncertainDelete = useRef<string | null>(null)
  useEffect(() => { active.current = true; return () => { active.current = false } }, [])
  const load = useCallback((next?: string) => {
    const request = ++readId.current
    return Promise.all([next ? Promise.resolve(null) : photosApi.get(albumId), photosApi.media(albumId, next)])
      .then(([nextAlbum, page]) => {
        if (!active.current || request !== readId.current) return
        if (next) {
          setItems(current => {
            const merged = new Map(current.map(item => [item.mediaId, item]))
            for (const item of page.items) merged.set(item.mediaId, item)
            return [...merged.values()]
          })
        } else {
          setAlbum(nextAlbum); setItems(page.items)
        }
        setCursor(page.nextCursor)
      })
      .catch(() => { if (active.current && request === readId.current) setLoadError(next ? 'Không thể tải thêm ảnh. Các ảnh đã tải vẫn được giữ lại.' : 'Không thể tải album. Vui lòng thử lại.') })
      .finally(() => { if (active.current && request === readId.current) setLoading(false) })
  }, [albumId])
  useEffect(() => { void load() }, [load])
  const reload = async (next?: string) => {
    if (pendingRef.current) return
    setLoading(true); setLoadError(''); setRetryCursor(next)
    await load(next)
  }
  const loadDetail = useCallback(async (mediaId: string) => {
    const request = ++detailId.current
    try {
      const nextDetail = await photosApi.photo(albumId, mediaId)
      if (active.current && request === detailId.current) setDetail(nextDetail)
    } catch { if (active.current && request === detailId.current) setDetailError('Không thể tải ảnh đầy đủ. Bạn có thể thử lại.') }
    finally { if (active.current && request === detailId.current) setDetailLoading(false) }
  }, [albumId])
  const retryDetail = () => { setDetailLoading(true); setDetailError(''); if (selectedId) void loadDetail(selectedId) }
  const closeCaption = () => { if (pendingRef.current) return; setCaptionTarget(null); setCaptionDraft(''); setWriteError('') }
  const closeDelete = () => { if (pendingRef.current) return; setDeleteTarget(null); setWriteError('') }
  const saveCaption = async () => {
    if (!captionTarget || pendingRef.current || !album?.canManage) return
    if (captionDraft.trim().length > 1_000) { setWriteError('Chú thích không được vượt quá 1.000 ký tự.'); return }
    pendingRef.current = true; setPending(true); setWriteError('')
    try {
      const updated = await photosApi.updateCaption(albumId, captionTarget.mediaId, captionDraft.trim())
      if (!active.current) return
      setItems(current => current.map(item => item.mediaId === updated.mediaId ? updated : item))
      setCaptionTarget(null); setCaptionDraft('')
    } catch { if (active.current) setWriteError('Không thể lưu chú thích. Nội dung của bạn vẫn được giữ lại; hãy thử lại.') }
    finally { if (active.current) { pendingRef.current = false; setPending(false) } }
  }
  const remove = async () => {
    if (!deleteTarget || pendingRef.current || !album?.canManage) return
    const mediaId = deleteTarget.mediaId
    const alreadyUncertain = uncertainDelete.current === mediaId
    pendingRef.current = true; setPending(true); setWriteError('')
    try {
      uncertainDelete.current = mediaId
      try { await photosApi.removeMedia(albumId, mediaId) }
      catch (failure) { if (!alreadyUncertain || !(failure instanceof ApiError) || failure.status !== 404) throw failure }
      if (!active.current) return
      uncertainDelete.current = null
      setItems(current => current.filter(item => item.mediaId !== mediaId))
      setAlbum(current => current && { ...current, photoCount: Math.max(0, current.photoCount - 1) })
      if (selectedId === mediaId) setSelectedId(null)
      setDeleteTarget(null)
    } catch { if (active.current) setWriteError('Không thể xác nhận xóa ảnh. Ảnh vẫn được hiển thị; hãy thử lại để kiểm tra.') }
    finally { if (active.current) { pendingRef.current = false; setPending(false) } }
  }
  const choose = (mediaId: string | null) => { detailId.current++; setDetail(null); setDetailError(''); setDetailLoading(Boolean(mediaId)); setSelectedId(mediaId); if (mediaId) void loadDetail(mediaId) }
  const selectedIndex = items.findIndex(item => item.mediaId === selectedId)
  const selected = items[selectedIndex]
  return <main className="mx-auto max-w-5xl p-5">
    <Link to="/photos" className="text-primary">← Ảnh</Link>
    {album && <><h1 className="mt-3 font-heading text-3xl font-bold text-text">{album.name}</h1><p className="text-text-muted">{album.description}</p></>}
    {loading && <p role="status" className="mt-4 text-text-muted">Đang tải album…</p>}
    {loadError && <div role="alert" className="mt-4 text-red-400">{loadError} <button type="button" disabled={loading || pending} onClick={() => void reload(retryCursor)}>Thử lại</button></div>}
    {album && !loading && !loadError && items.length === 0 && <p className="mt-5 text-text-muted">Album chưa có ảnh.</p>}
    <div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-3">{items.map(item => <div key={item.mediaId}>
      <button type="button" onClick={() => choose(item.mediaId)} aria-label="Xem ảnh"><img src={item.accessUrl} alt={item.caption ?? ''} loading="lazy" decoding="async" className="aspect-square w-full rounded-lg object-cover" /></button>
      {album?.canManage && <div className="flex gap-2"><button type="button" disabled={pending || loading} onClick={() => { if (pendingRef.current) return; setWriteError(''); setCaptionTarget(item); setCaptionDraft(item.caption ?? '') }} className="text-xs text-primary">Chú thích</button><button type="button" disabled={pending || loading} onClick={() => { if (pendingRef.current) return; setWriteError(''); setDeleteTarget(item) }} className="text-xs text-red-400">Xóa</button></div>}
    </div>)}</div>
    {cursor && !loadError && <button type="button" disabled={loading || pending} onClick={() => void reload(cursor)} className="mt-5 rounded bg-surface-2 px-4 py-2 text-text">Xem thêm</button>}
    {selected && <PhotoViewer item={selected} detail={detail} loading={detailLoading} error={detailError} onRetry={retryDetail} onClose={() => choose(null)} onPrevious={() => { if (selectedIndex > 0) choose(items[selectedIndex - 1].mediaId) }} onNext={() => { if (selectedIndex < items.length - 1) choose(items[selectedIndex + 1].mediaId) }} hasPrevious={selectedIndex > 0} hasNext={selectedIndex < items.length - 1} />}
    {captionTarget && <AppDialog title="Chỉnh sửa chú thích" onClose={closeCaption}>
      <form onSubmit={event => { event.preventDefault(); void saveCaption() }} aria-busy={pending}>
        <label className="mt-4 block text-sm font-semibold text-text">Chú thích ảnh<textarea data-dialog-initial-focus disabled={pending} value={captionDraft} onChange={event => setCaptionDraft(event.target.value)} maxLength={1_000} rows={4} className="mt-1.5 w-full resize-y rounded-lg border border-border bg-surface-2 p-3 text-sm text-text outline-none focus:border-primary" /></label>
        {writeError && <p role="alert" className="mt-3 text-red-400">{writeError}</p>}
        <div className="mt-5 flex justify-end gap-2"><button type="button" disabled={pending} onClick={closeCaption} className="rounded-lg bg-surface-2 px-4 py-2 text-text">Hủy</button><button type="submit" disabled={pending} className="rounded-lg bg-primary px-4 py-2 text-white">{pending ? 'Đang lưu…' : 'Lưu'}</button></div>
      </form>
    </AppDialog>}
    {deleteTarget && <AppDialog title="Xóa ảnh khỏi album?" onClose={closeDelete}>
      <p className="mt-4 text-text">Bạn muốn xóa ảnh này khỏi album?</p>
      {writeError && <p role="alert" className="mt-3 text-red-400">{writeError}</p>}
      <div className="mt-5 flex justify-end gap-2"><button data-dialog-initial-focus type="button" disabled={pending} onClick={closeDelete} className="rounded-lg bg-surface-2 px-4 py-2 text-text">Hủy</button><button type="button" disabled={pending} onClick={() => void remove()} className="rounded-lg bg-red-600 px-4 py-2 text-white">{pending ? 'Đang xóa…' : 'Xóa ảnh'}</button></div>
    </AppDialog>}
  </main>
}
