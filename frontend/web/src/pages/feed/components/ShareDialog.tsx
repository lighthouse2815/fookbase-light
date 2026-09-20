import { useEffect, useState } from 'react'
import { ApiError } from '../../../api/client'
import { groupsApi, type Group } from '../../../api/groups'
import { pagesApi, type Page } from '../../../api/pages'
import { postsApi } from '../../../api/posts'
import { useAuth } from '../../../auth/useAuth'

type Destination =
  | { type: 'profile'; id: string; label: string }
  | { type: 'group'; id: string; label: string }
  | { type: 'page'; id: string; label: string }

async function loadAll<T>(loadPage: (cursor?: string) => Promise<{ items: T[]; nextCursor: string | null }>) {
  const items: T[] = []
  let cursor: string | undefined
  do {
    const page = await loadPage(cursor)
    items.push(...page.items)
    cursor = page.nextCursor ?? undefined
  } while (cursor)
  return items
}

export default function ShareDialog({ postId, onClose, onShared }: {
  postId: string
  onClose: () => void
  onShared?: () => void
}) {
  const { session } = useAuth()
  const [groups, setGroups] = useState<Group[]>([])
  const [pages, setPages] = useState<Page[]>([])
  const [selected, setSelected] = useState(`profile:${session!.user.id}`)
  const [caption, setCaption] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSharing, setIsSharing] = useState(false)

  useEffect(() => {
    let active = true
    void Promise.all([loadAll(groupsApi.getMine), loadAll(pagesApi.mine)])
      .then(([groupItems, pageItems]) => {
        if (!active) return
        setGroups(groupItems)
        setPages(pageItems.filter((page) => page.viewerRole === 'owner' || page.viewerRole === 'admin' || page.viewerRole === 'editor'))
      })
      .catch((requestError) => {
        if (active) setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải nơi chia sẻ.')
      })
      .finally(() => { if (active) setIsLoading(false) })
    return () => { active = false }
  }, [])

  const destinations: Destination[] = [
    { type: 'profile', id: session!.user.id, label: `Trang cá nhân của @${session!.user.username}` },
    ...groups.map((group) => ({ type: 'group' as const, id: group.id, label: `Nhóm: ${group.name}` })),
    ...pages.map((page) => ({ type: 'page' as const, id: page.id, label: `Trang: ${page.name}` })),
  ]

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const [destinationType, destinationId] = selected.split(':', 2) as [Destination['type'], string]
    if (!destinationId || isSharing) return
    setError(null)
    setIsSharing(true)
    try {
      await postsApi.share(postId, { destinationType, destinationId, caption: caption.trim() || undefined })
      onShared?.()
      onClose()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể chia sẻ nội dung này.')
    } finally {
      setIsSharing(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/70 p-3" role="dialog" aria-modal="true" aria-labelledby="share-dialog-title">
      <form onSubmit={(event) => void submit(event)} className="max-h-[calc(100dvh-1.5rem)] w-full max-w-lg overflow-y-auto rounded-2xl border border-border bg-surface p-5 shadow-2xl">
        <div className="flex items-center justify-between gap-3">
          <h2 id="share-dialog-title" className="text-lg font-bold text-text">Chia sẻ</h2>
          <button type="button" onClick={onClose} disabled={isSharing} className="border-0 bg-transparent text-lg text-text-muted cursor-pointer">✕</button>
        </div>
        <label className="mt-4 block text-sm font-semibold text-text">Chia sẻ đến
          <select value={selected} onChange={(event) => setSelected(event.target.value)} disabled={isLoading || isSharing} className="mt-1.5 w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none disabled:opacity-50">
            {destinations.map((destination) => <option key={`${destination.type}:${destination.id}`} value={`${destination.type}:${destination.id}`}>{destination.label}</option>)}
          </select>
        </label>
        <label className="mt-4 block text-sm font-semibold text-text">Lời nhắn (không bắt buộc)
          <textarea value={caption} onChange={(event) => setCaption(event.target.value)} maxLength={10_000} disabled={isSharing} rows={4} className="mt-1.5 w-full resize-y rounded-lg border border-border bg-surface-2 p-3 text-sm text-text outline-none disabled:opacity-50" />
        </label>
        {error && <p role="alert" className="mt-3 text-sm text-[#ff8a9b]">{error}</p>}
        <button type="submit" disabled={isLoading || isSharing} className="mt-5 w-full rounded-lg bg-primary py-2.5 text-sm font-semibold text-white cursor-pointer disabled:cursor-not-allowed disabled:opacity-50">
          {isSharing ? 'Đang chia sẻ…' : 'Chia sẻ'}
        </button>
      </form>
    </div>
  )
}
