import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../../api/client'
import { mediaApi, type Media } from '../../../api/media'
import { storiesApi, type Story, type StoryPrivacy, type StoryTrayAuthor } from '../../../api/stories'
import StoryViewer from './StoryViewer'

const acceptedTypes = new Set(['image/jpeg', 'image/png', 'image/webp', 'video/mp4', 'video/webm'])
const maximumImageBytes = 20 * 1024 * 1024
const maximumVideoBytes = 500 * 1024 * 1024

export default function StoryTray() {
  const [groups, setGroups] = useState<StoryTrayAuthor[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [selection, setSelection] = useState<{ authorIndex: number; storyIndex: number } | null>(null)
  const trayRef = useRef<HTMLDivElement>(null)

  const load = useCallback(async () => {
    setError(null)
    try {
      const tray = await storiesApi.tray()
      setGroups(tray.items)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải Story.')
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => { void load() }, 0)
    return () => window.clearTimeout(timeoutId)
  }, [load])

  const updateStory = useCallback((updated: Story) => {
    setGroups((current) => current.map((group) => ({
      ...group,
      hasUnseenStories: group.stories.some((story) => story.id === updated.id
        ? !updated.isViewed && !updated.canManage
        : !story.isViewed && !story.canManage),
      stories: group.stories.map((story) => story.id === updated.id ? updated : story),
    })))
  }, [])

  const ownGroup = groups.find((group) => group.stories.some((story) => story.canManage))

  return <section className="relative">
    {error && <p className="mb-2 rounded-lg bg-danger/15 px-3 py-2 text-xs text-danger">{error} <button type="button" onClick={() => void load()} className="ml-1 underline">Thử lại</button></p>}
    <div ref={trayRef} className="flex gap-2 overflow-x-auto scroll-smooth pb-1 pr-1">
      <button type="button" onClick={() => ownGroup ? setSelection({ authorIndex: groups.indexOf(ownGroup), storyIndex: 0 }) : setIsCreateOpen(true)} className="relative h-40 w-[108px] shrink-0 overflow-hidden rounded-xl border border-border bg-surface-2 text-left shadow-sm">
        {ownGroup?.author.avatarUrl ? <img src={ownGroup.author.avatarUrl} className="h-full w-full object-cover opacity-75" alt="" /> : <span className="grid h-full place-items-center text-3xl">＋</span>}
        <span className="absolute inset-x-0 bottom-0 bg-linear-to-t from-black/80 to-transparent px-2 pb-2 pt-8 text-xs font-semibold text-white">{ownGroup ? 'Story của bạn' : 'Tạo Story'}</span>
        <span className="absolute left-1/2 top-2 grid h-7 w-7 -translate-x-1/2 place-items-center rounded-full border-2 border-white bg-primary text-sm text-white">＋</span>
      </button>
      {isLoading && Array.from({ length: 5 }, (_, index) => <div key={index} className="h-40 w-[108px] shrink-0 animate-pulse rounded-xl bg-surface-2" />)}
      {groups.map((group, authorIndex) => group !== ownGroup && <button key={group.author.userId} type="button" onClick={() => setSelection({ authorIndex, storyIndex: 0 })} className="relative h-40 w-[108px] shrink-0 overflow-hidden rounded-xl border bg-surface-2 text-left shadow-sm" style={{ borderColor: group.hasUnseenStories ? 'var(--color-primary)' : 'var(--color-border)' }}>
        {group.author.avatarUrl ? <img src={group.author.avatarUrl} className="h-full w-full object-cover opacity-75" alt="" /> : <span className="grid h-full place-items-center text-2xl text-text-muted">{group.author.displayName.slice(0, 2).toUpperCase()}</span>}
        <span className="absolute left-1.5 top-1.5 grid h-8 w-8 place-items-center overflow-hidden rounded-full border-2 border-primary bg-surface text-[10px] font-bold text-text">{group.author.avatarUrl ? <img src={group.author.avatarUrl} className="h-full w-full object-cover" alt="" /> : group.author.displayName.slice(0, 2).toUpperCase()}</span>
        <span className="absolute inset-x-0 bottom-0 bg-linear-to-t from-black/85 to-transparent px-2 pb-2 pt-8 text-xs font-semibold text-white line-clamp-2">{group.author.displayName}</span>
      </button>)}
    </div>
    {groups.length > 5 && <button type="button" onClick={() => trayRef.current?.scrollBy({ left: 360, behavior: 'smooth' })} className="absolute right-2 top-1/2 grid h-10 w-10 -translate-y-1/2 place-items-center rounded-full border-0 bg-surface-2 text-2xl text-text shadow-lg cursor-pointer" aria-label="Xem thêm Story">›</button>}
    {selection && <StoryViewer groups={groups} initialAuthorIndex={selection.authorIndex} initialStoryIndex={selection.storyIndex} onClose={() => setSelection(null)} onStoriesChanged={updateStory} />}
    {isCreateOpen && <CreateStoryDialog onClose={() => setIsCreateOpen(false)} onCreated={async () => { await load(); setIsCreateOpen(false) }} />}
  </section>
}

function CreateStoryDialog({ onClose, onCreated }: { onClose: () => void; onCreated: () => Promise<void> }) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [media, setMedia] = useState<Media | null>(null)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [caption, setCaption] = useState('')
  const [privacy, setPrivacy] = useState<StoryPrivacy>('friends')
  const [progress, setProgress] = useState(0)
  const [isUploading, setIsUploading] = useState(false)
  const [isPublishing, setIsPublishing] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => () => { if (previewUrl) URL.revokeObjectURL(previewUrl) }, [previewUrl])
  useEffect(() => {
    if (media?.status !== 'Processing') return
    const intervalId = window.setInterval(() => {
      void mediaApi.getMetadata(media.id).then(setMedia).catch(() => setError('Không thể kiểm tra trạng thái xử lý video.'))
    }, 2_000)
    return () => window.clearInterval(intervalId)
  }, [media?.id, media?.status])

  const choose = async (file: File | undefined) => {
    if (!file) return
    const maximum = file.type.startsWith('video/') ? maximumVideoBytes : maximumImageBytes
    if (!acceptedTypes.has(file.type) || file.size <= 0 || file.size > maximum) {
      setError('Chỉ hỗ trợ JPEG, PNG, WebP, MP4 hoặc WebM trong giới hạn upload.')
      return
    }
    setError(null)
    setProgress(0)
    setMedia(null)
    setPreviewUrl((current) => { if (current) URL.revokeObjectURL(current); return URL.createObjectURL(file) })
    setIsUploading(true)
    try {
      setMedia(await mediaApi.uploadFileWithMetadata(file, setProgress))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải media lên.')
    } finally {
      setIsUploading(false)
    }
  }

  const publish = async () => {
    if (!media || media.status !== 'Ready' || isPublishing) return
    setIsPublishing(true)
    setError(null)
    try {
      await storiesApi.create(media.id, caption.trim(), privacy)
      await onCreated()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể đăng Story.')
    } finally {
      setIsPublishing(false)
    }
  }

  const status = media?.status === 'Processing' ? 'Đang xử lý video…' : media?.status === 'Failed' ? 'Video xử lý thất bại.' : media?.status === 'Ready' ? 'Media đã sẵn sàng.' : isUploading ? `Đang tải lên ${progress}%…` : 'Chọn ảnh hoặc video (video tối đa 60 giây).'
  return <div className="fixed inset-0 z-[90] grid place-items-center bg-black/70 p-4" role="presentation"><section className="max-h-[calc(100dvh-2rem)] w-full max-w-lg overflow-y-auto rounded-2xl border border-border bg-surface p-5 shadow-2xl" role="dialog" aria-modal="true" aria-label="Tạo Story">
    <div className="flex items-center justify-between"><h2 className="font-heading text-lg font-bold text-text">Tạo Story</h2><button type="button" onClick={onClose} className="grid h-8 w-8 place-items-center rounded-full border-0 bg-surface-2 text-text">×</button></div>
    <input ref={inputRef} type="file" accept="image/jpeg,image/png,image/webp,video/mp4,video/webm" className="sr-only" onChange={(event) => { const file = event.target.files?.[0]; event.currentTarget.value = ''; void choose(file) }} />
    <button type="button" disabled={isUploading} onClick={() => inputRef.current?.click()} className="mt-4 flex h-52 w-full items-center justify-center overflow-hidden rounded-xl border border-dashed border-border bg-surface-2 text-sm text-text-muted disabled:opacity-60">
      {previewUrl && media?.mediaType === 'video' ? <video src={previewUrl} className="h-full w-full object-contain" muted controls /> : previewUrl ? <img src={previewUrl} className="h-full w-full object-contain" alt="Xem trước Story" /> : 'Chọn ảnh hoặc video'}
    </button>
    <p className="mt-2 text-xs text-text-muted">{status}</p>
    {error && <p className="mt-2 rounded-lg bg-danger/15 px-3 py-2 text-xs text-danger">{error}</p>}
    <label className="mt-4 block text-sm font-medium text-text">Chú thích<textarea value={caption} onChange={(event) => setCaption(event.target.value)} maxLength={2200} rows={3} className="mt-1 w-full resize-none rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none focus:border-primary" placeholder="Viết điều gì đó…" /></label>
    <label className="mt-3 block text-sm font-medium text-text">Quyền riêng tư<select value={privacy} onChange={(event) => setPrivacy(event.target.value as StoryPrivacy)} className="mt-1 w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none focus:border-primary"><option value="friends">Bạn bè</option><option value="public">Công khai</option><option value="onlyMe">Chỉ mình tôi</option></select></label>
    <div className="mt-5 flex justify-end gap-2"><button type="button" onClick={onClose} className="rounded-lg border border-border bg-transparent px-4 py-2 text-sm text-text">Hủy</button><button type="button" disabled={media?.status !== 'Ready' || isPublishing} onClick={() => void publish()} className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">{isPublishing ? 'Đang đăng…' : 'Đăng Story'}</button></div>
  </section></div>
}
