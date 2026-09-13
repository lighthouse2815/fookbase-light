import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { photosApi, type AlbumPhoto, type PhotoAlbum } from '../../api/photos'
import PhotoViewer from './PhotoViewer'

export default function AlbumDetailPage() {
  const { albumId = '' } = useParams()
  const [album, setAlbum] = useState<PhotoAlbum | null>(null)
  const [items, setItems] = useState<AlbumPhoto[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [selected, setSelected] = useState<number | null>(null)
  const [detail, setDetail] = useState<Awaited<ReturnType<typeof photosApi.photo>> | null>(null)
  const loadMedia = useCallback(async (next?: string) => { const page = await photosApi.media(albumId, next); setItems((current) => next ? [...current, ...page.items] : page.items); setCursor(page.nextCursor) }, [albumId])
  const loadInitial = useCallback(async () => { const [nextAlbum, page] = await Promise.all([photosApi.get(albumId), photosApi.media(albumId)]); setAlbum(nextAlbum); setItems(page.items); setCursor(page.nextCursor) }, [albumId])
  useEffect(() => { const timer = window.setTimeout(() => { void loadInitial() }, 0); return () => window.clearTimeout(timer) }, [loadInitial])
  useEffect(() => { if (selected !== null) void photosApi.photo(albumId, items[selected].mediaId).then(setDetail) }, [albumId, items, selected])
  if (!album) return <main className="p-5 text-text-muted">Loading album…</main>
  const choose = (index: number) => { setDetail(null); setSelected(index) }
  const remove = async (mediaId: string) => { await photosApi.removeMedia(albumId, mediaId); await loadMedia() }
  const caption = async (mediaId: string) => { const value = window.prompt('Caption'); if (value !== null) { await photosApi.updateCaption(albumId, mediaId, value); await loadMedia() } }
  return <main className="mx-auto max-w-5xl p-5"><Link to="/photos" className="text-primary">← Photos</Link><h1 className="mt-3 font-heading text-3xl font-bold text-text">{album.name}</h1><p className="text-text-muted">{album.description}</p><div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-3">{items.map((item, index) => <div key={item.mediaId}><button onClick={() => choose(index)}><img src={item.accessUrl} alt={item.caption ?? ''} className="aspect-square w-full rounded-lg object-cover" /></button>{album.canManage && <div className="flex gap-2"><button onClick={() => void caption(item.mediaId)} className="text-xs text-primary">Caption</button><button onClick={() => void remove(item.mediaId)} className="text-xs text-red-400">Remove</button></div>}</div>)}</div>{cursor && <button onClick={() => void loadMedia(cursor)} className="mt-5 rounded bg-surface-2 px-4 py-2 text-text">Load more</button>}{selected !== null && <PhotoViewer item={items[selected]} detail={detail} onClose={() => setSelected(null)} onPrevious={() => choose(Math.max(0, selected - 1))} onNext={() => choose(Math.min(items.length - 1, selected + 1))} hasPrevious={selected > 0} hasNext={selected < items.length - 1} />}</main>
}
