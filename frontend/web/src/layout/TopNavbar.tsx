import { useEffect, useState, type ReactNode } from 'react'
import { Link, NavLink, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useRealtime } from '../realtime/useRealtime'
import { PreferenceControls, usePreferences } from '../preferences'
import { searchApi, type SearchSuggestions } from '../api/search'

interface NavItem {
  path: string
  icon: ReactNode
  label: string
}

function HomeIcon() {
  return <svg viewBox="0 0 28 28" aria-hidden="true" className="h-7 w-7 fill-current"><path d="M25.825 12.29 14.743 2.47a1.12 1.12 0 0 0-1.486 0L2.175 12.29a1.12 1.12 0 0 0 .743 1.96h2.237v9.29c0 1.082.878 1.96 1.96 1.96h4.06v-6.227h5.65V25.5h4.06c1.082 0 1.96-.878 1.96-1.96v-9.29h2.237a1.12 1.12 0 0 0 .743-1.96Z" /></svg>
}

function ReelsIcon() {
  return <svg viewBox="0 0 28 28" aria-hidden="true" className="h-7 w-7 fill-none stroke-current" strokeWidth="2.3" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="4" width="22" height="20" rx="4" /><path d="m3.5 9.5 5-5M10 9.5l5-5M16.5 9.5l5-5M12 12.25l5.25 3.25L12 18.75v-6.5Z" /></svg>
}

function GroupsIcon() {
  return <svg viewBox="0 0 28 28" aria-hidden="true" className="h-7 w-7 fill-none stroke-current" strokeWidth="2.3" strokeLinecap="round" strokeLinejoin="round"><circle cx="14" cy="9" r="4" /><path d="M6 24c.55-4.12 3.12-6.5 8-6.5s7.45 2.38 8 6.5M4.5 12.75a3 3 0 1 1 3.08-5.99M23.5 12.75a3 3 0 1 0-3.08-5.99M3.25 22.25c.2-2.1 1.22-3.7 3.2-4.62M24.75 22.25c-.2-2.1-1.22-3.7-3.2-4.62" /></svg>
}

function GamesIcon() {
  return <svg viewBox="0 0 28 28" aria-hidden="true" className="h-7 w-7 fill-none stroke-current" strokeWidth="2.3" strokeLinecap="round" strokeLinejoin="round"><path d="M7.2 10.5h13.6c2.4 0 3.8 1.84 3.35 4.18l-1.05 5.43c-.42 2.14-2.48 3.22-4.37 2.28l-3.3-1.64a3.04 3.04 0 0 0-2.66 0l-3.3 1.64c-1.89.94-3.95-.14-4.37-2.28l-1.05-5.43C3.4 12.34 4.8 10.5 7.2 10.5Z" /><path d="M9 15h4M11 13v4M18.5 14.5h.01M21 17h.01" /></svg>
}

function ProfileIcon() {
  return <svg viewBox="0 0 28 28" aria-hidden="true" className="h-7 w-7 fill-none stroke-current" strokeWidth="2.3" strokeLinecap="round" strokeLinejoin="round"><circle cx="14" cy="9" r="4.25" /><path d="M5.25 24c.7-4.38 3.58-7 8.75-7s8.05 2.62 8.75 7" /></svg>
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
  const [suggestions, setSuggestions] = useState<SearchSuggestions | null>(null)
  const [isSearchFocused, setIsSearchFocused] = useState(false)
  const [isNotificationsOpen, setIsNotificationsOpen] = useState(false)
  const initials = session!.user.username.slice(0, 2).toUpperCase()
  const navItems: NavItem[] = [
    { path: '/feed', icon: <HomeIcon />, label: t('home') },
    { path: '/reels', icon: <ReelsIcon />, label: 'Reels' },
    { path: '/groups', icon: <GroupsIcon />, label: t('groups') },
    { path: '/games', icon: <GamesIcon />, label: t('games') },
    { path: '/profile', icon: <ProfileIcon />, label: t('profile') },
  ]

  const submitSearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const query = searchQuery.trim()
    setSuggestions(null)
    navigate(query ? `/search?q=${encodeURIComponent(query)}` : '/search')
  }

  useEffect(() => {
    const query = searchQuery.trim()
    if (query.length < 2) {
      return
    }

    const controller = new AbortController()
    const timeoutId = window.setTimeout(() => {
      void searchApi.suggestions(query, { signal: controller.signal })
        .then((next) => setSuggestions(next))
        .catch(() => {
          if (!controller.signal.aborted) setSuggestions(null)
        })
    }, 300)
    return () => {
      controller.abort()
      window.clearTimeout(timeoutId)
    }
  }, [searchQuery])

  const hasSuggestions = Boolean(suggestions &&
    (suggestions.people.length || suggestions.groups.length || suggestions.pages.length))

  const notificationDestination = (notification: typeof notifications[number]) => {
    if ((notification.type === 'FriendRequestReceived' || notification.type === 'FriendRequestAccepted' || notification.type === 'UserFollowed') && notification.actorUserId) {
      return '/profile/' + notification.actorUserId
    }
    if (notification.type === 'GroupInvite' || notification.type === 'GroupJoinApproved') {
      return '/groups'
    }
    if (notification.type === 'PageRoleInvite') {
      return '/pages'
    }
    if (notification.type === 'StoryReaction') {
      return '/feed'
    }

    if (notification.entityType === 'Page' || notification.entityType === 'PageRoleInvitation') {
      return '/pages'
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
      case 'UserFollowed':
        return actor + ' ' + t('startedFollowingYou')
      case 'PostReaction':
        return actor + ' reacted to your post.'
      case 'PostComment':
        return actor + ' commented on your post.'
      case 'CommentReaction':
        return actor + ' reacted to your comment.'
      case 'PostShared':
        return actor + ' shared your post.'
      case 'PostMention':
        return actor + ' mentioned you in a post.'
      case 'CommentMention':
        return actor + ' mentioned you in a comment.'
      case 'GroupInvite':
        return actor + ' invited you to a group.'
      case 'GroupJoinApproved':
        return actor + ' approved your group join request.'
      case 'StoryReaction':
        return actor + ' reacted to your Story.'
      case 'PageRoleInvite':
        return actor + ' invited you to manage a Page.'
      default:
        return actor + ' sent you a notification.'
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
            onChange={(event) => {
              const value = event.target.value
              setSearchQuery(value)
              if (value.trim().length < 2) setSuggestions(null)
            }}
            onFocus={() => setIsSearchFocused(true)}
            onBlur={() => window.setTimeout(() => setIsSearchFocused(false), 150)}
            placeholder={t('searchFookbase')}
            className="w-full bg-surface-2 border-none rounded-full text-[13px] text-text pl-9 pr-4 py-2 outline-none focus:input-focus transition-all placeholder:text-text-light"
          />
          {isSearchFocused && hasSuggestions && suggestions && <div className="absolute top-11 z-50 w-full overflow-hidden rounded-xl border border-border bg-surface shadow-xl">
            {suggestions.people.length > 0 && <div className="border-b border-border p-2 last:border-0"><p className="px-2 pb-1 text-[11px] font-semibold uppercase tracking-wide text-text-light">People</p>{suggestions.people.map((person) => <Link key={person.userId} to={`/profile/${person.userId}`} onClick={() => setSuggestions(null)} className="block rounded-lg px-2 py-1.5 text-sm text-text no-underline hover:bg-surface-2"><span className="font-semibold">{person.displayName}</span><span className="ml-1 text-text-muted">@{person.username}</span></Link>)}</div>}
            {suggestions.groups.length > 0 && <div className="border-b border-border p-2 last:border-0"><p className="px-2 pb-1 text-[11px] font-semibold uppercase tracking-wide text-text-light">Groups</p>{suggestions.groups.map((group) => <Link key={group.groupId} to={`/groups/${group.groupId}`} onClick={() => setSuggestions(null)} className="block rounded-lg px-2 py-1.5 text-sm text-text no-underline hover:bg-surface-2">{group.name}</Link>)}</div>}
            {suggestions.pages.length > 0 && <div className="p-2"><p className="px-2 pb-1 text-[11px] font-semibold uppercase tracking-wide text-text-light">Pages</p>{suggestions.pages.map((page) => <Link key={page.pageId} to={`/pages/${page.username}`} onClick={() => setSuggestions(null)} className="block rounded-lg px-2 py-1.5 text-sm text-text no-underline hover:bg-surface-2">{page.name}<span className="ml-1 text-text-muted">@{page.username}</span></Link>)}</div>}
          </div>}
        </form>
      </div>

      <nav aria-label="Điều hướng chính" className="flex flex-1 items-center justify-center gap-1 self-stretch px-2 max-w-[600px] mx-auto">
        {navItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) => [
              'flex flex-1 items-center justify-center self-stretch rounded-lg transition-colors duration-200 cursor-pointer relative max-w-[120px] no-underline',
              isActive ? 'text-primary' : 'text-text-muted hover:bg-surface-2',
            ].join(' ')}
            title={item.label}
            aria-label={item.label}
          >
            {({ isActive }) => (
              <>
                {item.icon}
                {isActive && <div className="absolute bottom-0 left-1 right-1 h-[3px] bg-primary rounded-t-full" />}
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
