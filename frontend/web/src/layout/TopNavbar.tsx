import { useState } from 'react'
import { Link, NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useRealtime } from '../realtime/useRealtime'

interface NavItem {
  path: string
  icon: string
  label: string
}

const NAV_ITEMS: NavItem[] = [
  { path: '/feed', icon: '🏠', label: 'Home' },
  { path: '/explore', icon: '🔍', label: 'Explore' },
  { path: '/messages', icon: '💬', label: 'Messages' },
  { path: '/games', icon: '🎮', label: 'Games' },
  { path: '/profile', icon: '👤', label: 'Profile' },
]

export default function TopNavbar() {
  const { session, signOut } = useAuth()
  const { incomingMessages, incomingFriendNotifications, markFriendNotificationRead, unreadMessageCount } = useRealtime()
  const navigate = useNavigate()
  const [searchQuery, setSearchQuery] = useState('')
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false)
  const initials = session!.user.username.slice(0, 2).toUpperCase()

  const submitSearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const query = searchQuery.trim()
    navigate(query ? `/explore?q=${encodeURIComponent(query)}` : '/explore')
  }

  return (
    <header className="fixed top-0 left-0 right-0 h-14 bg-surface border-b border-border flex items-center px-4 z-50">
      {/* ── Left: Logo + Search ──────────────────────── */}
      <div className="flex items-center gap-2 w-[280px] shrink-0">
        {/* Facebook-style logo */}
        <Link
          to="/feed"
          className="w-10 h-10 rounded-full bg-primary flex items-center justify-center shrink-0 cursor-pointer border-none hover:brightness-110 transition no-underline"
          title="Fookbase Home"
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
            placeholder="Search Fookbase"
            className="w-full bg-surface-2 border-none rounded-full text-[13px] text-text pl-9 pr-4 py-2 outline-none focus:input-focus transition-all placeholder:text-text-light"
          />
        </form>
      </div>

      {/* ── Center: Navigation Tabs ──────────────────── */}
      <nav className="flex-1 flex items-center justify-center gap-1 max-w-[600px] mx-auto">
        {NAV_ITEMS.map((item) => (
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
        {/* Grid menu */}
        <button
          type="button"
          onClick={() => void signOut()}
          className="w-10 h-10 rounded-full bg-surface-2 flex items-center justify-center text-text hover:bg-[#4e4f50] transition-colors cursor-pointer border-none text-sm"
          title="Sign out"
        >
          ↪
        </button>
        <div className="relative">
          <button
            type="button"
            onClick={() => setIsNotificationsOpen((current) => !current)}
            className="w-10 h-10 rounded-full bg-surface-2 flex items-center justify-center text-text hover:bg-[#4e4f50] transition-colors cursor-pointer border-none text-sm relative"
            title="Message notifications"
            aria-expanded={isNotificationsOpen}
          >
            🔔
            {unreadMessageCount > 0 && <span className="absolute -top-0.5 -right-0.5 min-w-5 h-5 rounded-full bg-[#e41e3f] text-[10px] font-bold text-white flex items-center justify-center px-1">{unreadMessageCount > 99 ? '99+' : unreadMessageCount}</span>}
          </button>
          {isNotificationsOpen && (
            <div className="absolute right-0 top-12 z-50 w-[min(22rem,calc(100vw-2rem))] overflow-hidden rounded-2xl border border-border bg-surface shadow-2xl">
              <div className="flex items-center justify-between border-b border-border px-4 py-3"><h2 className="font-heading text-base font-bold text-text">Notifications</h2><Link to="/messages" onClick={() => setIsNotificationsOpen(false)} className="text-xs font-semibold text-primary no-underline hover:underline">Open messages</Link></div>
              <div className="max-h-96 overflow-y-auto">
                {incomingMessages.length + incomingFriendNotifications.length === 0 ? <p className="px-4 py-6 text-center text-sm text-text-muted">You are all caught up.</p> : (
                  <>
                    {incomingFriendNotifications.map((notification) => (
                      <Link key={notification.id} to={`/profile/${notification.actorUserId}`} onClick={() => { markFriendNotificationRead(notification.id); setIsNotificationsOpen(false) }} className="block border-b border-border px-4 py-3 no-underline transition-colors last:border-0 hover:bg-surface-2">
                        <p className="text-sm font-semibold text-text">{notification.type === 'friend_request' ? 'New friend request' : 'Friend request accepted'}</p>
                        <p className="mt-0.5 text-sm text-text-muted">View profile</p>
                        <p className="mt-1 text-xs text-text-light">{new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(new Date(notification.createdAtUtc))}</p>
                      </Link>
                    ))}
                    {incomingMessages.map((incoming) => (
                      <Link key={incoming.message.id} to={`/messages?conversation=${incoming.conversation.id}`} onClick={() => setIsNotificationsOpen(false)} className="block border-b border-border px-4 py-3 no-underline transition-colors last:border-0 hover:bg-surface-2">
                        <p className="text-sm font-semibold text-text">New message</p>
                        <p className="mt-0.5 truncate text-sm text-text-muted">{incoming.message.content}</p>
                        <p className="mt-1 text-xs text-text-light">{new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(new Date(incoming.message.createdAtUtc))}</p>
                      </Link>
                    ))}
                  </>
                )}
              </div>
            </div>
          )}
        </div>
        {/* User avatar link to profile */}
        <Link
          to="/profile"
          className="w-10 h-10 rounded-full flex items-center justify-center text-[11px] font-bold text-white shrink-0 cursor-pointer border-none hover:brightness-110 transition no-underline bg-primary"
          title={`${session!.user.username}'s Profile`}
        >
          {initials}
        </Link>
      </div>
    </header>
  )
}
