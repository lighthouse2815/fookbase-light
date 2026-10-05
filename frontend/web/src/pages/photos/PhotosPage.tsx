import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Mascot } from 'page-mascot'
import { photosApi, type PhotoAlbum } from '../../api/photos'
import { ApiError } from '../../api/client'
import { useAuth } from '../../auth/useAuth'
import { mediaApi } from '../../api/media'

const maximumImageBytes = 20 * 1024 * 1024
const privacyLabels = { public: 'Công khai', friends: 'Bạn bè', onlyme: 'Chỉ mình tôi' } as const
interface UploadEntry { file: File; mediaId?: string; added: boolean; checkMembership?: boolean; progress: number }
export default function PhotosPage() {
  const { session } = useAuth()
  const [params] = useSearchParams()
  const ownerId = params.get('userId') ?? session!.user.id
  return <PhotosContent key={`${session!.user.id}:${ownerId}`} ownerId={ownerId} mine={ownerId === session!.user.id} />
}

function PhotosContent({ ownerId, mine }: { ownerId: string; mine: boolean }) {
  const [items, setItems] = useState<PhotoAlbum[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [listError, setListError] = useState('')
  const [retryCursor, setRetryCursor] = useState<string | undefined>()
  const [name, setName] = useState('')
  const [privacy, setPrivacy] = useState<PhotoAlbum['privacy']>('public')
  const [files, setFiles] = useState<File[]>([])
  const [entries, setEntries] = useState<UploadEntry[]>([])
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [pending, setPending] = useState(false)
  const [hasBatch, setHasBatch] = useState(false)
  const [uncertainCreate, setUncertainCreate] = useState(false)
  const [createVerified, setCreateVerified] = useState(false)
  const [fileInputKey, setFileInputKey] = useState(0)
  const active = useRef(false)
  const pendingRef = useRef(false)
  const readId = useRef(0)
  const batch = useRef<{ album: PhotoAlbum; entries: UploadEntry[] } | null>(null)
  const savedAlbums = useRef(new Map<string, PhotoAlbum>())
  useEffect(() => { active.current = true; return () => { active.current = false } }, [])
  const load = useCallback(async (next?: string) => {
    const request = ++readId.current
    try {
      const page = await photosApi.userAlbums(ownerId, next)
      if (!active.current || request !== readId.current) return
      setItems(current => {
        const merged = new Map((next ? current : []).map(item => [item.id, item]))
        for (const item of page.items) merged.set(item.id, item)
        for (const [id, saved] of savedAlbums.current) merged.set(id, saved)
        return [...merged.values()]
      })
      setCursor(page.nextCursor)
      return true
    } catch { if (active.current && request === readId.current) setListError('Không thể tải danh sách album. Các album đã tải vẫn được giữ lại.') }
    finally { if (active.current && request === readId.current) setLoading(false) }
  }, [ownerId])
  useEffect(() => { void load() }, [load])
  const reload = async (next?: string) => {
    setLoading(true); setListError(''); setRetryCursor(next); setCreateVerified(false)
    const loaded = await load(next)
    if (active.current && loaded && uncertainCreate) setCreateVerified(true)
  }
  const saveKnownAlbum = (album: PhotoAlbum) => {
    savedAlbums.current.set(album.id, album)
    setItems(current => [album, ...current.filter(item => item.id !== album.id)])
  }
  const create = async (event: React.FormEvent) => {
    event.preventDefault()
    if (pendingRef.current || uncertainCreate || !mine) return
    if (!batch.current && (!name.trim() || name.trim().length > 160)) { setError('Tên album cần có từ 1 đến 160 ký tự.'); return }
    if (!batch.current && (files.length > 20 || files.some(file => !['image/jpeg', 'image/png', 'image/webp'].includes(file.type) || file.size === 0 || file.size > maximumImageBytes))) { setError('Chọn tối đa 20 ảnh JPEG, PNG hoặc WebP có dữ liệu, mỗi ảnh không vượt quá 20 MB.'); return }
    pendingRef.current = true; setPending(true); setError(''); setSuccess('')
    try {
      if (!batch.current) {
        let album: PhotoAlbum
        try { album = await photosApi.create(name.trim(), '', privacy) }
        catch (failure) {
          if (!active.current) return
          if (!(failure instanceof ApiError) || failure.status === 0 || failure.status >= 500) {
            setUncertainCreate(true); setCreateVerified(false)
            setError('Chưa xác nhận được album đã tạo hay chưa. Hãy tải lại danh sách để kiểm tra trước khi tạo album mới.')
          } else setError('Không thể tạo album. Vui lòng kiểm tra tên và quyền truy cập rồi thử lại.')
          return
        }
        if (!active.current) return
        batch.current = { album, entries: files.map(file => ({ file, added: false, progress: 0 })) }
        setHasBatch(true); setEntries([...batch.current.entries]); saveKnownAlbum(album)
      }
      const current = batch.current
      for (const entry of current.entries) {
        if (!active.current) return
        if (entry.added) continue
        if (!entry.mediaId) {
          entry.mediaId = await mediaApi.uploadFile(entry.file, progress => {
            if (!active.current) return
            entry.progress = progress; setEntries([...current.entries])
          })
          if (!active.current) return
        }
        // A failed add may have committed on the server. Resolve membership before retrying it.
        if (entry.checkMembership) {
          try { await photosApi.photo(current.album.id, entry.mediaId); entry.added = true }
          catch (failure) { if (!(failure instanceof ApiError) || failure.status !== 404) throw failure }
          if (!active.current) return
          entry.checkMembership = false
        }
        if (!entry.added) {
          entry.checkMembership = true
          await photosApi.addMedia(current.album.id, entry.mediaId)
          if (!active.current) return
          entry.checkMembership = false; entry.added = true
        }
        entry.progress = 100
        current.album = { ...current.album, photoCount: current.entries.filter(item => item.added).length }
        saveKnownAlbum(current.album); setEntries([...current.entries])
      }
      if (!active.current) return
      setSuccess(`Đã tạo album “${current.album.name}” với ${current.entries.length} ảnh.`)
      batch.current = null; setHasBatch(false); setEntries([]); setName(''); setFiles([]); setFileInputKey(value => value + 1)
      await reload()
    } catch { if (active.current) setError('Chưa tải xong ảnh. Album và các ảnh đã thêm được giữ lại; thử lại để tiếp tục phần còn lại.') }
    finally { if (active.current) { pendingRef.current = false; setPending(false) } }
  }
  const startNewAlbum = () => {
    if (pendingRef.current || loading || !createVerified) return
    batch.current = null; setHasBatch(false); setEntries([])
    setUncertainCreate(false); setCreateVerified(false); setError(''); setSuccess('')
    setName(''); setFiles([]); setFileInputKey(value => value + 1)
  }
  const locked = pending || hasBatch || uncertainCreate
  return <main className="mx-auto max-w-5xl p-5">
    <div className="flex items-center gap-4"><Mascot directions="/mascots/koala-directions.webp" reactions="/mascots/koala-reactions.webp" size={72} label="Photos Koala Mascot" /><h1 className="font-heading text-3xl font-bold text-text">Ảnh và album</h1></div>
    {error && <p role="alert" className="mt-3 text-red-400">{error}</p>}
    {success && <p role="status" className="mt-3 text-text">{success}</p>}
    {mine && <form onSubmit={event => void create(event)} className="mt-5 flex flex-wrap gap-2" aria-busy={pending}>
      <input aria-label="Tên album" value={name} disabled={locked} maxLength={160} onChange={event => setName(event.target.value)} placeholder="Tên album" className="min-w-48 flex-1 rounded border border-border bg-surface p-2 text-text" />
      <select aria-label="Quyền riêng tư album" disabled={locked} value={privacy} onChange={event => setPrivacy(event.target.value as PhotoAlbum['privacy'])} className="rounded border border-border bg-surface p-2 text-text">{Object.entries(privacyLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
      <input key={fileInputKey} aria-label="Ảnh trong album" disabled={locked} type="file" multiple accept="image/jpeg,image/png,image/webp" onChange={event => setFiles(Array.from(event.target.files ?? []))} />
      <button disabled={pending || uncertainCreate} className="rounded bg-primary px-4 py-2 text-white disabled:opacity-50">{pending ? 'Đang lưu…' : hasBatch ? 'Thử lại ảnh còn lại' : 'Tạo album'}</button>
    </form>}
    {entries.length > 0 && <div className="mt-3 text-sm text-text" aria-live="polite"><p>Đã thêm {entries.filter(entry => entry.added).length}/{entries.length} ảnh</p>{entries.map((entry, index) => <p key={index}>{entry.file.name}: {entry.added ? 'Đã thêm' : `${entry.progress}%`}</p>)}</div>}
    {uncertainCreate && <button type="button" disabled={loading} onClick={() => void reload()} className="mt-3 text-primary">Tải lại danh sách</button>}
    {uncertainCreate && createVerified && <div className="mt-3 text-sm text-text"><p>Kiểm tra danh sách bên dưới để tránh tạo trùng album. Nếu cần một album khác, hãy bắt đầu lại.</p><button type="button" disabled={loading || pending} onClick={startNewAlbum} className="mt-2 text-primary">Bắt đầu album mới</button></div>}
    {listError && <div role="alert" className="mt-4 text-red-400">{listError} <button type="button" disabled={loading} onClick={() => void reload(retryCursor)}>Thử lại</button></div>}
    {loading && <p role="status" className="mt-4 text-text-muted">Đang tải album…</p>}
    {!loading && !listError && items.length === 0 && <p className="mt-4 text-text-muted">Chưa có album ảnh.</p>}
    <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{items.map(item => <Link key={item.id} to={`/albums/${item.id}`} className="rounded-xl border border-border bg-surface p-4 no-underline"><p className="font-bold text-text">{item.name}</p><p className="text-sm text-text-muted">{item.photoCount} ảnh · {privacyLabels[item.privacy]}</p></Link>)}</div>
    {cursor && !listError && <button type="button" disabled={loading || pending} onClick={() => void reload(cursor)} className="mt-5 rounded bg-surface-2 px-4 py-2 text-text">Xem thêm</button>}
  </main>
}
