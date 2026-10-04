import { useEffect, useRef, useState } from 'react'
import { ApiError } from '../../../api/client'
import { groupsApi, type Group } from '../../../api/groups'
import { pagesApi, type Page } from '../../../api/pages'
import { postsApi } from '../../../api/posts'
import { useAuth } from '../../../auth/useAuth'
import AppDialog from '../../../shared/components/AppDialog'
import { showToast } from '../../../shared/toastState'

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
  const inFlightRef = useRef(false)
  const mountedRef = useRef(false)

  useEffect(() => {
    let active = true
    mountedRef.current = true
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
    return () => { active = false; mountedRef.current = false }
  }, [])

  const destinations: Destination[] = [
    { type: 'profile', id: session!.user.id, label: `Trang cá nhân của @${session!.user.username}` },
    ...groups.map((group) => ({ type: 'group' as const, id: group.id, label: `Nhóm: ${group.name}` })),
    ...pages.map((page) => ({ type: 'page' as const, id: page.id, label: `Trang: ${page.name}` })),
  ]

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const [destinationType, destinationId] = selected.split(':', 2) as [Destination['type'], string]
    if (!destinationId || inFlightRef.current || isLoading) return
    inFlightRef.current = true
    setError(null)
    setIsSharing(true)
    try {
      await postsApi.share(postId, { destinationType, destinationId, caption: caption.trim() || undefined })
      if (mountedRef.current) { onShared?.(); onClose() }
    } catch (requestError) {
      const message = requestError instanceof ApiError ? requestError.message : 'Không thể chia sẻ nội dung này.'
      if (mountedRef.current) setError(message)
      showToast(message)
    } finally {
      inFlightRef.current = false
      if (mountedRef.current) setIsSharing(false)
    }
  }

  return (
    <AppDialog title="Chia sẻ" className="max-w-lg" onClose={() => { if (!inFlightRef.current) onClose() }}>
      <form onSubmit={(event) => void submit(event)}>
        <button type="button" aria-label="Đóng chia sẻ" onClick={onClose} disabled={isSharing} className="post-action-focus absolute right-4 top-4 grid h-8 w-8 place-items-center rounded-full border-0 bg-surface-2 text-lg text-text-muted disabled:opacity-50">✕</button>
        <label className="mt-4 block text-sm font-semibold text-text">Chia sẻ đến
          <select data-dialog-initial-focus value={selected} onChange={(event) => setSelected(event.target.value)} disabled={isLoading || isSharing} className="mt-1.5 w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none focus:border-primary disabled:opacity-50">
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
    </AppDialog>
  )
}
