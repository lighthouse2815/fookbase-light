import { useState } from 'react'
import { Link } from 'react-router-dom'
import { getNotificationPresentation } from '../../shared/notificationPresentation'
import { formatPostTimestamp } from '../../shared/formatPostTimestamp'
import { useRealtime } from '../../realtime/useRealtime'

export default function NotificationsPage() {
  const { notifications, unreadNotificationCount, markAllNotificationsRead, markNotificationRead, hasMoreNotifications, isLoadingMoreNotifications, loadMoreNotifications } = useRealtime()
  const [filter, setFilter] = useState<'all' | 'unread'>('all')
  const visibleNotifications = filter === 'unread' ? notifications.filter((notification) => !notification.isRead) : notifications

  return <main className="min-h-screen px-3 py-4 sm:px-4">
    <section className="mx-auto w-full max-w-[680px] rounded-xl border border-border bg-surface p-3 shadow-sm">
      <div className="flex items-center justify-between px-1"><h1 className="font-heading text-2xl font-bold text-text">Thông báo</h1><button type="button" onClick={markAllNotificationsRead} disabled={unreadNotificationCount === 0} className="grid h-9 w-9 place-items-center rounded-full border-0 bg-transparent text-xl text-text-muted cursor-pointer hover:bg-surface-2 disabled:cursor-default disabled:opacity-50" title="Đánh dấu tất cả là đã đọc">•••</button></div>
      <div className="flex gap-2 py-2"><button type="button" onClick={() => setFilter('all')} className={`rounded-full border-0 px-3 py-2 text-sm font-semibold cursor-pointer ${filter === 'all' ? 'bg-primary/20 text-primary' : 'bg-transparent text-text hover:bg-surface-2'}`}>Tất cả</button><button type="button" onClick={() => setFilter('unread')} className={`rounded-full border-0 px-3 py-2 text-sm font-semibold cursor-pointer ${filter === 'unread' ? 'bg-primary/20 text-primary' : 'bg-transparent text-text hover:bg-surface-2'}`}>Chưa đọc</button></div>
      <h2 className="px-1 pb-1 text-base font-bold text-text">Trước đó</h2>
      <div className="space-y-0.5">
        {visibleNotifications.length === 0 ? <p className="px-4 py-8 text-center text-sm text-text-muted">Không có thông báo để hiển thị.</p> : visibleNotifications.map((notification, index) => {
          const presentation = getNotificationPresentation(notification)
          const avatarTone = ['bg-[#87433b]', 'bg-[#5f7997]', 'bg-[#8e5b88]', 'bg-[#607b57]', 'bg-[#9b6c45]'][index % 5]
          const time = formatPostTimestamp(notification.createdAtUtc).compact
          return <Link key={notification.id} to={presentation.destination} onClick={() => markNotificationRead(notification.id)} className={`relative flex gap-3 rounded-xl px-2 py-2.5 no-underline transition-colors hover:bg-surface-2 ${notification.isRead ? '' : 'bg-primary/10'}`}>
            <span className={`relative flex h-14 w-14 shrink-0 items-center justify-center rounded-full text-sm font-bold text-white ${presentation.actor ? avatarTone : 'bg-surface-2 text-text-muted'}`}>{presentation.actor ? presentation.actor.slice(0, 2).toUpperCase() : '!' }<span className={`absolute -bottom-0.5 -right-0.5 grid h-6 w-6 place-items-center rounded-full border-2 border-surface text-[11px] font-bold text-white ${presentation.badge.className}`}>{presentation.badge.icon}</span></span>
            <span className="min-w-0 flex-1 pr-4"><span className="block text-sm leading-5 text-text">{presentation.actor ? <><strong>{presentation.actor}</strong> {presentation.message}</> : presentation.text}</span><span className={`mt-0.5 block text-xs font-semibold ${notification.isRead ? 'text-text-light' : 'text-primary'}`}>{time}</span></span>
            {!notification.isRead && <span className="absolute right-3 top-1/2 h-3 w-3 -translate-y-1/2 rounded-full bg-primary" />}
          </Link>
        })}
      </div>
      {hasMoreNotifications && <button type="button" onClick={loadMoreNotifications} disabled={isLoadingMoreNotifications} className="mt-3 w-full rounded-lg border-0 bg-surface-2 px-4 py-2.5 text-sm font-semibold text-text cursor-pointer hover:bg-surface-3 disabled:cursor-wait">{isLoadingMoreNotifications ? 'Đang tải…' : 'Xem thông báo trước đó'}</button>}
    </section>
  </main>
}
