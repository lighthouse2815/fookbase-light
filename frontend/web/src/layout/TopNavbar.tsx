import { useState } from 'react'
import { Link, NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useRealtime } from '../realtime/useRealtime'
import { PreferenceControls, usePreferences } from '../preferences'

interface NavItem {
  path: string
  icon: string
  label: string
}

export default function TopNavbar() {
  const { session, signOut } = useAuth()
  const {
    notifications,
    markAllNotificationsRead,
    markNotificationRead,
    hasMoreNotifications,
    isLoadingMoreNotifications,
    loadMoreNotifications,
    unreadNotificationCount,
  } = useRealtime()
  const { t } = usePreferences()
  const navigate = useNavigate()
  const [searchQuery, setSearchQuery] = useState('')
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false)
  const initials = session!.user.username.slice(0, 2).toUpperCase()
  const navItems: NavItem[] = [
    { path: '/feed', icon: '🏠', label: t('home') },
    { path: '/explore', icon: '🔍', label: t('explore') },
    { path: '/messages', icon: '💬', label: t('messages') },
    { path: '/games', icon: '🎮', label: t('games') },
    { path: '/profile', icon: '👤', label: t('profile') },
  ]

  const submitSearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const query = searchQuery.trim()
    navigate(query ? `/explore?q=${encodeURIComponent(query)}` : '/explore')
  }

  const notificationDestination = (notification: typeof notifications[number]) => {
    if ((notification.type === 'FriendRequestReceived' || notification.type === 'FriendRequestAccepted') && notification.actorUserId) {
      return '/profile/' + notification.actorUserId
    }

    return notification.entityId ? '/feed?post=' + notification.entityId : '/feed'
  }

  const notificationText = (notification: typeof notifications[number]) => {
    const actor = notification.actorDisplayName ?? notification.actorUsername ?? t('user')
    switch (notification.type) {
      case 'FriendRequestReceived':
        return actor + ' sent you a friend request.'
      case 'FriendRequestAccepted':
        return actor + ' accepted your friend request.'
      case 'PostReaction':
        return actor + ' reacted to your post.'
      case 'PostComment':
        return actor + ' commented on your post.'
      case 'CommentReaction':
        return actor + ' reacted to your comment.'
      case 'PostMention':
        return actor + ' mentioned you in a post.'
      case 'CommentMention':
        return actor + ' mentioned you in a comment.'
    }
  }

  return (
    <header className="fixed top-0 left-0 right-0 h-14 bg-surface border-b border-border flex items-center px-4 z-50">
      <div className="flex items-center gap-2 w-[280px] shrink-0">
        <Link
          to="/feed"
          className="w-10 h-10 rounded-full bg-primary flex items-center justify-center shrink-0 cursor-pointer border-none hover:brightness-110 transition no-underline"
          title={t('home')}
        >
          <span className="text-white text-xl font-bold">f</span>
        </Link>
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

      <nav className="flex-1 flex items-center justify-center gap-1 max-w-[600px] mx-auto">
        {navItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) => [
              'flex-1 flex items-center justify-center py-2 rounded-lg transition-all duration-200 cursor-pointer relative max-w-[120px] text-2xl no-underline',
              isActive ? 'text-primary' : 'text-text-muted hover:bg-surface-2',
            ].join(' ')}
            title={item.label}
          >
            {({ isActive }) => (
              <>
                <span>{item.icon}</span>
                {isActive && <div className="absolute bottom-0 left-2 right-2 h-[3px] bg-primary rounded-t-full" />}
              </>
            )}
          </NavLink>
        ))}
      </nav>

      <div className="flex items-center gap-2 w-[280px] shrink-0 justify-end">
        <PreferenceControls className="max-sm:hidden" />
        <button
          type="button"
          onClick={() => void signOut()}
          className="w-10 h-10 rounded-full bg-surface-2 flex items-center justify-center text-text hover:bg-[#4e4f50] transition-colors cursor-pointer border-none text-sm"
          title={t('signOut')}
        >
          ↪
        </button>
        <div className="relative">
          <button
            type="button"
            onClick={() => setIsNotificationsOpen((current) => !current)}
            className="w-10 h-10 rounded-full bg-surface-2 flex items-center justify-center text-text hover:bg-[#4e4f50] transition-colors cursor-pointer border-none text-sm relative"
            title={t('messageNotifications')}
            aria-expanded={isNotificationsOpen}
          >
            🔔
            {unreadNotificationCount > 0 && <span className="absolute -top-0.5 -right-0.5 min-w-5 h-5 rounded-full bg-[#e41e3f] text-[10px] font-bold text-white flex items-center justify-center px-1">{unreadNotificationCount > 99 ? '99+' : unreadNotificationCount}</span>}
          </button>
          {isNotificationsOpen && (
            <div className="absolute right-0 top-12 z-50 w-[min(22rem,calc(100vw-2rem))] overflow-hidden rounded-2xl border border-border bg-surface shadow-2xl">
              <div className="flex items-center justify-between border-b border-border px-4 py-3"><h2 className="font-heading text-base font-bold text-text">{t('notifications')}</h2>{unreadNotificationCount > 0 && <button type="button" onClick={markAllNotificationsRead} className="border-0 bg-transparent text-xs font-semibold text-primary cursor-pointer hover:underline">Mark all read</button>}</div>
              <div className="max-h-96 overflow-y-auto">
                {notifications.length === 0 ? <p className="px-4 py-6 text-center text-sm text-text-muted">{t('allCaughtUp')}</p> : (
                  <>
                    {notifications.map((notification) => (
                      <Link key={notification.id} to={notificationDestination(notification)} onClick={() => { markNotificationRead(notification.id); setIsNotificationsOpen(false) }} className="block border-b border-border px-4 py-3 no-underline transition-colors last:border-0 hover:bg-surface-2">
                        <p className={notification.isRead ? 'text-sm text-text-muted' : 'text-sm font-semibold text-text'}>{notificationText(notification)}</p>
                        <p className="mt-1 text-xs text-text-light">{new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(new Date(notification.createdAtUtc))}</p>
                      </Link>
                    ))}
                    {hasMoreNotifications && <button type="button" onClick={loadMoreNotifications} disabled={isLoadingMoreNotifications} className="w-full border-0 bg-surface-2 px-4 py-3 text-sm font-semibold text-primary cursor-pointer disabled:cursor-wait">{isLoadingMoreNotifications ? t('loading') : 'Load more'}</button>}
                  </>
                )}
              </div>
            </div>
          )}
        </div>
        <Link
          to="/profile"
          className="w-10 h-10 rounded-full flex items-center justify-center text-[11px] font-bold text-white shrink-0 cursor-pointer border-none hover:brightness-110 transition no-underline bg-primary"
          title={t('profile')}
        >
          {initials}
        </Link>
      </div>
    </header>
  )
}
