import { useEffect, useId, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useRealtime } from '../../realtime/useRealtime'
import NotificationList from './NotificationList'
import './notifications.css'

export default function NotificationCenter({ onOpen, popover = false }: { onOpen?: () => void; popover?: boolean }) {
  const { unreadNotificationCount, markAllNotificationsRead, isMarkingNotificationsRead, isLoadingNotifications, isLoadingMoreNotifications } = useRealtime()
  const [filter, setFilter] = useState<'all' | 'unread'>('all')
  const [menuOpen, setMenuOpen] = useState(false)
  const titleId = useId()
  const menuId = useId()
  const centerRef = useRef<HTMLElement>(null)
  const menuRef = useRef<HTMLDivElement>(null)
  const menuButtonRef = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    if (popover) centerRef.current?.focus({ preventScroll: true })
  }, [popover])
  useEffect(() => {
    if (!menuOpen) return
    menuRef.current?.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus({ preventScroll: true })
    const outside = (event: PointerEvent) => {
      if (!menuRef.current?.contains(event.target as Node)) setMenuOpen(false)
    }
    document.addEventListener('pointerdown', outside)
    return () => document.removeEventListener('pointerdown', outside)
  }, [menuOpen])
  const Heading = popover ? 'h2' : 'h1'
  return <section ref={centerRef} tabIndex={popover ? -1 : undefined} role={popover ? 'dialog' : undefined} aria-labelledby={titleId} className={`notification-center flex min-h-0 flex-col outline-none ${popover ? 'notification-popover header-popover w-[min(25rem,calc(100vw-1rem))] rounded-2xl border border-border bg-surface shadow-2xl' : ''}`}>
    <div className="flex shrink-0 items-center justify-between gap-2 px-4 pt-3">
      <Heading id={titleId} className="font-heading text-2xl font-bold text-text">Thông báo</Heading>
      <div ref={menuRef} className="relative" onKeyDown={(event) => {
        if (event.key === 'Escape' && menuOpen) {
          event.preventDefault()
          event.stopPropagation()
          setMenuOpen(false)
          menuButtonRef.current?.focus()
        }
      }}>
        <button ref={menuButtonRef} type="button" aria-label="Tùy chọn thông báo" aria-expanded={menuOpen} aria-controls={menuId} onClick={() => setMenuOpen((current) => !current)} className="notification-focus grid h-9 w-9 place-items-center rounded-full border-0 bg-transparent text-xl text-text-muted hover:bg-surface-2">•••</button>
        {menuOpen && <div id={menuId} className="absolute right-0 top-10 z-10 w-[min(16rem,calc(100vw-3rem))] rounded-xl border border-border bg-surface p-1.5 shadow-xl"><button type="button" disabled={unreadNotificationCount === 0 || isMarkingNotificationsRead || isLoadingNotifications || isLoadingMoreNotifications} onClick={() => { markAllNotificationsRead(); setMenuOpen(false); menuButtonRef.current?.focus() }} className="notification-focus w-full rounded-lg border-0 bg-transparent px-3 py-2.5 text-left text-sm font-semibold text-text hover:bg-surface-2 disabled:opacity-50">Đánh dấu tất cả là đã đọc</button></div>}
      </div>
    </div>
    <div className="flex shrink-0 items-center gap-2 px-4 pb-2 pt-2" role="group" aria-label="Lọc thông báo">
      {(['all', 'unread'] as const).map((value) => <button key={value} type="button" aria-pressed={filter === value} onClick={() => setFilter(value)} className={`notification-focus rounded-full border-0 px-3 py-2 text-sm font-semibold ${filter === value ? 'bg-primary/15 text-primary' : 'bg-transparent text-text hover:bg-surface-2'}`}>{value === 'all' ? 'Tất cả' : 'Chưa đọc'}</button>)}
      {popover && <Link to="/notifications" onClick={onOpen} className="notification-focus ml-auto rounded px-1 py-2 text-xs font-medium text-primary no-underline hover:underline">Xem tất cả</Link>}
    </div>
    <NotificationList filter={filter} onOpen={onOpen} scrollable={popover} />
  </section>
}
