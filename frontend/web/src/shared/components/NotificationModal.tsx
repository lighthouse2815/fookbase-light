import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import type { Notification, NotificationType } from '../../data/mockData'
import { getUserById, formatTimestamp } from '../../data/mockData'

interface NotificationModalProps {
  isOpen: boolean
  onClose: () => void
  notifications: Notification[]
  onMarkAllAsRead: () => void
  onMarkAsRead: (id: string) => void
  onUpdateFriendRequest?: (id: string, status: 'accepted' | 'declined') => void
}

const typeIconConfig: Record<
  NotificationType,
  { icon: string; bg: string; label: string }
> = {
  like: { icon: '👍', bg: 'bg-[#2374e1]', label: 'Thích' },
  comment: { icon: '💬', bg: 'bg-[#31a24c]', label: 'Bình luận' },
  friend: { icon: '👥', bg: 'bg-[#2374e1]', label: 'Kết bạn' },
  mention: { icon: '@', bg: 'bg-[#a855f7]', label: 'Nhắc đến' },
  security: { icon: '🛡️', bg: 'bg-[#e41e3f]', label: 'Bảo mật' },
  group: { icon: '🏷️', bg: 'bg-[#e7a33e]', label: 'Nhóm' },
}

export default function NotificationModal({
  isOpen,
  onClose,
  notifications,
  onMarkAllAsRead,
  onMarkAsRead,
  onUpdateFriendRequest,
}: NotificationModalProps) {
  const [filter, setFilter] = useState<'all' | 'unread'>('all')
  const dropdownRef = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()

  const unreadCount = notifications.filter((n) => !n.isRead).length

  // Close when clicking outside or pressing Escape
  useEffect(() => {
    if (!isOpen) return

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }

    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        onClose()
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    // Use timeout to prevent instant close on trigger click
    const timer = setTimeout(() => {
      window.addEventListener('click', handleClickOutside)
    }, 10)

    return () => {
      window.removeEventListener('keydown', handleKeyDown)
      window.removeEventListener('click', handleClickOutside)
      clearTimeout(timer)
    }
  }, [isOpen, onClose])

  if (!isOpen) return null

  const displayedNotifications = notifications.filter((n) => {
    if (filter === 'unread') return !n.isRead
    return true
  })

  const handleNotificationClick = (notif: Notification) => {
    onMarkAsRead(notif.id)
    if (notif.targetUrl) {
      navigate(notif.targetUrl)
      onClose()
    }
  }

  return (
    <>
      {/* Semi-transparent backdrop on small screens */}
      <div
        className="fixed inset-0 z-40 bg-black/40 sm:bg-transparent backdrop-blur-xs sm:backdrop-blur-none"
        aria-hidden="true"
      />

      {/* ── Notification Dropdown Container ───────────── */}
      <div
        ref={dropdownRef}
        role="dialog"
        aria-modal="true"
        aria-label="Thông báo"
        className="fixed top-14 right-2 sm:right-4 z-50 w-[calc(100vw-16px)] sm:w-[400px] max-w-[420px] max-h-[calc(100vh-68px)] bg-surface border border-border rounded-2xl shadow-2xl flex flex-col overflow-hidden"
        style={{ animation: 'scale-in 0.18s ease both' }}
      >
        {/* ── Header ────────────────────────────────────── */}
        <div className="p-4 pb-2">
          <div className="flex items-center justify-between mb-3">
            <h2 className="text-xl font-bold font-heading text-text">Thông báo</h2>

            <div className="flex items-center gap-1">
              {unreadCount > 0 && (
                <button
                  type="button"
                  onClick={onMarkAllAsRead}
                  className="text-[13px] text-primary hover:text-primary-light font-medium px-2.5 py-1 rounded-lg hover:bg-surface-2 transition-colors cursor-pointer border-none bg-transparent"
                  title="Đánh dấu tất cả là đã đọc"
                >
                  Đánh dấu đã đọc
                </button>
              )}
              <button
                type="button"
                onClick={onClose}
                className="w-8 h-8 rounded-full bg-surface-2 hover:bg-surface-3 flex items-center justify-center text-text-muted hover:text-text transition-colors cursor-pointer border-none text-sm"
                title="Đóng (Esc)"
              >
                ✕
              </button>
            </div>
          </div>

          {/* Filter Tabs */}
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setFilter('all')}
              className={`px-3.5 py-1.5 rounded-full text-[13px] font-semibold transition-colors cursor-pointer border-none ${filter === 'all'
                  ? 'bg-primary text-white'
                  : 'bg-surface-2 text-text-muted hover:bg-surface-3 hover:text-text'
                }`}
            >
              Tất cả
            </button>
            <button
              type="button"
              onClick={() => setFilter('unread')}
              className={`px-3.5 py-1.5 rounded-full text-[13px] font-semibold transition-colors cursor-pointer border-none flex items-center gap-1.5 ${filter === 'unread'
                  ? 'bg-primary text-white'
                  : 'bg-surface-2 text-text-muted hover:bg-surface-3 hover:text-text'
                }`}
            >
              <span>Chưa đọc</span>
              {unreadCount > 0 && (
                <span
                  className={`text-[11px] px-1.5 py-0.2 rounded-full font-bold ${filter === 'unread' ? 'bg-white/20 text-white' : 'bg-[#e41e3f] text-white'
                    }`}
                >
                  {unreadCount}
                </span>
              )}
            </button>
          </div>
        </div>

        {/* ── Notification List ─────────────────────────── */}
        <div className="flex-1 overflow-y-auto scroll-smooth px-2 py-2 space-y-1">
          {displayedNotifications.length === 0 ? (
            <div className="py-12 px-4 text-center">
              <div className="text-4xl mb-2">🔔</div>
              <p className="text-[14px] text-text-muted font-medium">
                {filter === 'unread' ? 'Không có thông báo chưa đọc nào' : 'Bạn chưa có thông báo mới nào'}
              </p>
            </div>
          ) : (
            displayedNotifications.map((notif) => {
              const user = getUserById(notif.userId)
              const badge = typeIconConfig[notif.type] ?? typeIconConfig.like

              return (
                <div
                  key={notif.id}
                  onClick={() => handleNotificationClick(notif)}
                  className={`group relative p-3 rounded-xl flex items-start gap-3 transition-all cursor-pointer ${notif.isRead ? 'hover:bg-surface-2/70 bg-transparent' : 'bg-primary/10 hover:bg-primary/15'
                    }`}
                >
                  {/* Avatar with type badge */}
                  <div className="relative shrink-0 mt-0.5">
                    {user ? (
                      <div
                        className={`w-12 h-12 rounded-full flex items-center justify-center text-[13px] font-bold text-white ${user.avatarColor}`}
                      >
                        {user.avatar}
                      </div>
                    ) : (
                      <div className="w-12 h-12 rounded-full bg-surface-2 border border-border flex items-center justify-center text-xl text-text">
                        🛡️
                      </div>
                    )}
                    {/* Badge Icon at bottom-right of avatar */}
                    <div
                      className={`absolute -bottom-1 -right-1 w-5 h-5 rounded-full ${badge.bg} text-white flex items-center justify-center text-[10px] border-2 border-surface shadow-sm`}
                      title={badge.label}
                    >
                      {badge.icon}
                    </div>
                  </div>

                  {/* Notification Content */}
                  <div className="flex-1 min-w-0 pr-3">
                    <p className="text-[13px] leading-snug text-text">
                      {user && (
                        <span className="font-bold text-text hover:underline mr-1">
                          {user.displayName}
                        </span>
                      )}
                      <span>{notif.title}</span>
                    </p>

                    {notif.content && (
                      <p className="text-[12px] text-text-muted line-clamp-2 mt-1 leading-relaxed italic">
                        {notif.content}
                      </p>
                    )}

                    {/* Friend request interactive actions */}
                    {notif.type === 'friend' && notif.actionData && (
                      <div className="mt-2.5">
                        {notif.actionData.friendRequestStatus === 'pending' ? (
                          <div
                            className="flex items-center gap-2"
                            onClick={(e) => e.stopPropagation()}
                          >
                            <button
                              type="button"
                              onClick={() => {
                                onUpdateFriendRequest?.(notif.id, 'accepted')
                                onMarkAsRead(notif.id)
                              }}
                              className="px-3.5 py-1.5 rounded-lg bg-primary hover:bg-primary-dark text-white font-semibold text-[12px] transition-colors cursor-pointer border-none shadow-xs"
                            >
                              Chấp nhận
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                onUpdateFriendRequest?.(notif.id, 'declined')
                                onMarkAsRead(notif.id)
                              }}
                              className="px-3.5 py-1.5 rounded-lg bg-surface-2 hover:bg-surface-3 text-text font-semibold text-[12px] transition-colors cursor-pointer border-none"
                            >
                              Xóa
                            </button>
                          </div>
                        ) : notif.actionData.friendRequestStatus === 'accepted' ? (
                          <span className="text-[12px] font-semibold text-[#31a24c]">
                            ✓ Đã chấp nhận lời mời kết bạn
                          </span>
                        ) : (
                          <span className="text-[12px] text-text-light">
                            Đã xóa lời mời kết bạn
                          </span>
                        )}
                      </div>
                    )}

                    {/* Timestamp */}
                    <div className="flex items-center gap-2 mt-1">
                      <span
                        className={`text-[11px] font-medium ${notif.isRead ? 'text-text-light' : 'text-primary font-semibold'
                          }`}
                      >
                        {formatTimestamp(notif.timestamp)}
                      </span>
                    </div>
                  </div>

                  {/* Unread indicator dot */}
                  {!notif.isRead && (
                    <div
                      className="w-2.5 h-2.5 rounded-full bg-primary shrink-0 self-center"
                      title="Chưa đọc"
                    />
                  )}
                </div>
              )
            })
          )}
        </div>

        {/* ── Footer ────────────────────────────────────── */}
        <div className="p-2 border-t border-border bg-surface text-center">
          <button
            type="button"
            onClick={onClose}
            className="w-full py-1.5 text-[13px] font-semibold text-text-muted hover:text-text hover:bg-surface-2 rounded-lg transition-colors cursor-pointer border-none bg-transparent"
          >
            Đóng thông báo
          </button>
        </div>
      </div>
    </>
  )
}

