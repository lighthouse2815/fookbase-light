import { useLayoutEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useRealtime } from '../../realtime/useRealtime'
import { formatPostTimestamp } from '../formatPostTimestamp'
import { getNotificationPresentation } from '../notificationPresentation'
import NotificationAvatar, { NotificationIcon } from './NotificationAvatar'

export default function NotificationList({ filter, onOpen, scrollable = false }: { filter: 'all' | 'unread'; onOpen?: () => void; scrollable?: boolean }) {
  const { notifications, unreadNotificationCount, isLoadingNotifications, notificationsError, reloadNotifications, hasMoreNotifications, isLoadingMoreNotifications, loadMoreNotificationsError, loadMoreNotifications, markNotificationRead, recentNotificationIds, isMarkingAllNotificationsRead } = useRealtime()
  const visible = filter === 'unread' ? notifications.filter((item) => !item.isRead) : notifications
  const scrollRef = useRef<HTMLDivElement>(null)
  const previousScroll = useRef({ head: '', height: 0, top: 0 })
  const head = visible[0]?.id ?? ''
  const [cutoff] = useState(() => Date.now() - 24 * 60 * 60 * 1000)
  useLayoutEffect(() => {
    const element = scrollRef.current
    if (!element || !scrollable) return
    const previous = previousScroll.current
    // Keep the content under the reader in place when realtime prepends a row.
    if (previous.top > 0 && previous.head !== head && recentNotificationIds.has(head)) {
      element.scrollTop = previous.top + element.scrollHeight - previous.height
    }
    previousScroll.current = { head, height: element.scrollHeight, top: element.scrollTop }
  }, [head, visible.length, recentNotificationIds, scrollable])

  const groups = [
    { label: 'Mới', items: visible.filter((item) => !item.isRead || Date.parse(item.createdAtUtc) >= cutoff) },
    { label: 'Trước đó', items: visible.filter((item) => item.isRead && Date.parse(item.createdAtUtc) < cutoff) },
  ]
  return <div ref={scrollRef} className={`notification-scroll min-h-0 px-2 pb-2 ${scrollable ? 'overflow-y-auto overscroll-contain' : ''}`} aria-busy={isLoadingNotifications || isLoadingMoreNotifications} onScroll={(event) => {
    previousScroll.current.top = event.currentTarget.scrollTop
    previousScroll.current.height = event.currentTarget.scrollHeight
  }}>
    {isLoadingNotifications && notifications.length === 0 ? <div role="status" aria-label="Đang tải thông báo" className="space-y-1 py-2">
      {Array.from({ length: 5 }, (_, index) => <div key={index} aria-hidden="true" className="flex gap-3 rounded-xl px-2 py-3 motion-safe:animate-pulse"><span className="h-14 w-14 shrink-0 rounded-full bg-surface-2" /><span className="flex flex-1 flex-col justify-center gap-2"><span className="h-3 w-4/5 rounded bg-surface-2" /><span className="h-3 w-3/5 rounded bg-surface-2" /><span className="h-2.5 w-16 rounded bg-surface-2" /></span></div>)}
    </div> : <>
      {notificationsError && <div role="status" className="rounded-xl px-4 py-6 text-center"><p className="font-semibold text-text">Không thể tải thông báo</p><button type="button" onClick={reloadNotifications} disabled={isLoadingNotifications || isMarkingAllNotificationsRead} className="notification-focus mt-3 rounded-lg bg-primary/10 px-4 py-2 text-sm font-semibold text-primary disabled:opacity-50">{isLoadingNotifications ? 'Đang tải…' : 'Thử lại'}</button></div>}
      {!notificationsError && visible.length === 0 && <div className="flex flex-col items-center px-5 py-8 text-center"><span className="mb-3 grid h-16 w-16 place-items-center rounded-full bg-primary/10 p-4 text-primary"><NotificationIcon icon="bell" /></span><p className="font-semibold text-text">{filter === 'unread' ? unreadNotificationCount > 0 ? `Còn ${unreadNotificationCount} thông báo chưa đọc` : 'Bạn đã xem hết thông báo' : 'Chưa có thông báo'}</p><p className="mt-1 max-w-64 text-sm leading-5 text-text-muted">{filter === 'unread' ? unreadNotificationCount > 0 ? hasMoreNotifications ? 'Xem thông báo trước đó để tiếp tục.' : 'Thử tải lại để xem thông báo chưa đọc.' : 'Thông báo chưa đọc sẽ xuất hiện ở đây.' : 'Khi có hoạt động mới, thông báo sẽ xuất hiện ở đây.'}</p>{filter === 'unread' && unreadNotificationCount > 0 && !hasMoreNotifications && <button type="button" onClick={reloadNotifications} disabled={isLoadingNotifications || isMarkingAllNotificationsRead} className="notification-focus mt-3 rounded-lg bg-primary/10 px-4 py-2 text-sm font-semibold text-primary disabled:opacity-50">Thử lại</button>}</div>}
      {groups.map(({ label, items }) => items.length > 0 && <section key={label} aria-label={label}>
        <h3 className="px-2 pb-1 pt-2 text-base font-bold text-text">{label}</h3>
        <ul className="space-y-0.5">
          {items.map((notification) => {
            const presentation = getNotificationPresentation(notification)
            const time = formatPostTimestamp(notification.createdAtUtc)
            return <li key={notification.id}><Link data-notification-id={notification.id} to={presentation.destination} onClick={() => { markNotificationRead(notification.id); onOpen?.() }} className={`notification-focus relative flex cursor-pointer items-start gap-3 rounded-xl px-2 py-2.5 no-underline transition-colors hover:bg-surface-2 ${notification.isRead ? '' : 'bg-primary/10'} ${recentNotificationIds.has(notification.id) ? 'notification-enter' : ''}`}>
              <NotificationAvatar notification={notification} />
              <span className="min-w-0 flex-1"><span className="block text-sm leading-5 text-text [overflow-wrap:anywhere]">{presentation.actor ? <><strong className="font-semibold">{presentation.actor}</strong> {presentation.message}</> : presentation.text}</span><time dateTime={notification.createdAtUtc} title={time.absolute} className={`mt-1 block text-xs font-medium ${notification.isRead ? 'text-text-muted' : 'text-primary'}`}>{time.compact}</time><span className="sr-only">{notification.isRead ? 'Đã đọc' : 'Chưa đọc'}</span></span>
              {!notification.isRead && <span aria-hidden="true" className="my-auto h-2.5 w-2.5 shrink-0 rounded-full bg-primary" />}
            </Link></li>
          })}
        </ul>
      </section>)}
      {loadMoreNotificationsError && <p role="status" className="px-3 pt-3 text-center text-sm text-text-muted">Không thể tải thêm thông báo.</p>}
      {hasMoreNotifications && <button type="button" onClick={loadMoreNotifications} disabled={isLoadingMoreNotifications || isMarkingAllNotificationsRead || isLoadingNotifications} className="notification-focus mt-3 w-full rounded-lg border-0 bg-surface-2 px-4 py-2.5 text-sm font-semibold text-text hover:bg-surface-3 disabled:cursor-wait disabled:opacity-60">{isLoadingMoreNotifications ? 'Đang tải…' : loadMoreNotificationsError ? 'Thử lại' : 'Xem thông báo trước đó'}</button>}
    </>}
  </div>
}
