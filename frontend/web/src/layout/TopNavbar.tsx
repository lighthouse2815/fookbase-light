import { useState } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { CURRENT_USER, INITIAL_NOTIFICATIONS, type Notification } from '../data/mockData'
import NotificationModal from '../shared/components/NotificationModal'

interface NavItem {
  path: string
  icon: string
  label: string
}

export default function TopNavbar() {
  const [isNotifOpen, setIsNotifOpen] = useState(false)
  const [notifications, setNotifications] = useState<Notification[]>(INITIAL_NOTIFICATIONS)

  const unreadCount = notifications.filter((n) => !n.isRead).length

  const handleMarkAllAsRead = () => {
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })))
  }

  const handleMarkAsRead = (id: string) => {
    setNotifications((prev) =>
      prev.map((n) => (n.id === id ? { ...n, isRead: true } : n))
    )
  }

  const handleUpdateFriendRequest = (id: string, status: 'accepted' | 'declined') => {
    setNotifications((prev) =>
      prev.map((n) =>
        n.id === id
          ? {
            ...n,
            isRead: true,
            actionData: {
              ...n.actionData,
              friendRequestStatus: status,
            },
          }
          : n
      )
    )
  }
  return (
    <header className="fixed top-0 left-0 right-0 h-14 bg-surface border-b border-border flex items-center px-4 z-50">
      {/* ── Left: Logo + Search ──────────────────────── */}
      <div className="flex items-center gap-2 w-[280px] shrink-0">
        {/* Facebook-style logo */}
        <Link
          to="/feed"
          className="w-10 h-10 rounded-full bg-primary flex items-center justify-center shrink-0 cursor-pointer border-none hover:brightness-110 transition no-underline"
          title={t('home')}
        >
          <span className="text-white text-xl font-bold">f</span>
        </Link>

        {/* Search */}
        <form onSubmit={submitSearch} className="relative flex-1 max-sm:hidden">
          <span className="absolute left-3 top-1/2 -translate-y-1/2 text-text-light text-sm">🔍</span>
          <input
            type="search"
            value={searchQuery}
            onChange={(event) => setSearchQuery(event.target.value)}
            placeholder={t('searchFookbase')}
            className="w-full bg-surface-2 border-none rounded-full text-[13px] text-text pl-9 pr-4 py-2 outline-none focus:input-focus transition-all placeholder:text-text-light"
          />
        </form>
      </div>

      {/* ── Center: Navigation Tabs ──────────────────── */}
      <nav className="flex-1 flex items-center justify-center gap-1 max-w-[600px] mx-auto">
        {navItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) =>
              [
                'flex-1 flex items-center justify-center py-2 rounded-lg transition-all duration-200 cursor-pointer relative max-w-[120px] text-2xl no-underline',
                isActive ? 'text-primary' : 'text-text-muted hover:bg-surface-2',
              ].join(' ')
            }
            title={item.label}
          >
            {({ isActive }) => (
              <>
                <span>{item.icon}</span>
                {isActive && (
                  <div className="absolute bottom-0 left-2 right-2 h-[3px] bg-primary rounded-t-full" />
                )}
              </>
            )}
          </NavLink>
        ))}
      </nav>

      {/* ── Right: Actions ───────────────────────────── */}
      <div className="flex items-center gap-2 w-[280px] shrink-0 justify-end">
        <PreferenceControls className="max-sm:hidden" />
        {/* Grid menu */}
        <button
          type="button"
          onClick={() => void signOut()}
          className="w-10 h-10 rounded-full bg-surface-2 flex items-center justify-center text-text hover:bg-[#4e4f50] transition-colors cursor-pointer border-none text-sm"
          title="Menu"
        >
          ⊞
        </button>
        {/* Notifications */}
        <button
          type="button"
          onClick={() => setIsNotifOpen((prev) => !prev)}
          className={`w-10 h-10 rounded-full flex items-center justify-center transition-colors cursor-pointer border-none text-sm relative ${isNotifOpen
              ? 'bg-primary/20 text-primary'
              : 'bg-surface-2 text-text hover:bg-[#4e4f50]'
            }`}
          title="Thông báo"
          aria-expanded={isNotifOpen}
        >
          🔔
          {unreadCount > 0 && (
            <span className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] px-1 rounded-full bg-[#e41e3f] text-[10px] font-bold text-white flex items-center justify-center shadow-xs">
              {unreadCount > 9 ? '9+' : unreadCount}
            </span>
          )}
        </button>

        {/* User avatar link to profile */}
        <Link
          to="/profile"
          className="w-10 h-10 rounded-full flex items-center justify-center text-[11px] font-bold text-white shrink-0 cursor-pointer border-none hover:brightness-110 transition no-underline bg-primary"
          title={t('profile')}
        >
          {initials}
        </Link>
      </div>

      {/* ── Notification Modal / Dropdown ─────────── */}
      <NotificationModal
        isOpen={isNotifOpen}
        onClose={() => setIsNotifOpen(false)}
        notifications={notifications}
        onMarkAllAsRead={handleMarkAllAsRead}
        onMarkAsRead={handleMarkAsRead}
        onUpdateFriendRequest={handleUpdateFriendRequest}
      />
    </header>
  )
}
