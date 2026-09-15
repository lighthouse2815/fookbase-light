import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useRealtime } from '../realtime/useRealtime'
import { PreferenceControls, usePreferences } from '../preferences'
import { searchApi, type SearchSuggestions } from '../api/search'
import { resolveProfileImageUrl, usersApi } from '../api/users'
import { getNotificationPresentation } from '../shared/notificationPresentation'
import { formatPostTimestamp } from '../shared/formatPostTimestamp'

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

function MenuIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-current"><circle cx="5" cy="5" r="2" /><circle cx="12" cy="5" r="2" /><circle cx="19" cy="5" r="2" /><circle cx="5" cy="12" r="2" /><circle cx="12" cy="12" r="2" /><circle cx="19" cy="12" r="2" /><circle cx="5" cy="19" r="2" /><circle cx="12" cy="19" r="2" /><circle cx="19" cy="19" r="2" /></svg>
}

function ZolaLightIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-current"><path d="M12 2.5C6.53 2.5 2.1 6.66 2.1 11.8c0 2.93 1.44 5.54 3.69 7.24v3.97l3.74-2.06c.8.22 1.63.34 2.47.34 5.47 0 9.9-4.16 9.9-9.29C21.9 6.66 17.47 2.5 12 2.5Zm1.08 12.58-2.52-2.69-4.92 2.72 5.42-5.75 2.6 2.7 4.81-2.72-5.39 5.74Z" /></svg>
}

function BellIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-current"><path d="M19.2 16.4v-5.1c0-3.68-2.2-6.16-5.2-6.75V3.5a2 2 0 1 0-4 0v1.05c-3 .59-5.2 3.07-5.2 6.75v5.1L3.2 18v1.5h17.6V18l-1.6-1.6ZM12 22a2.75 2.75 0 0 0 2.59-1.8H9.4A2.75 2.75 0 0 0 12 22Z" /></svg>
}

function ChevronDownIcon() {
  return <svg viewBox="0 0 16 16" aria-hidden="true" className="h-3 w-3 fill-current"><path d="m4.1 5.9 3.9 3.9 3.9-3.9 1.1 1.1L8 11.1 3 7l1.1-1.1Z" /></svg>
}

function ProfileMenuIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="2.3"><circle cx="12" cy="8" r="3.5" /><path d="M5.5 20c.65-4 2.9-6 6.5-6s5.85 2 6.5 6" /></svg>
}

function SettingsIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.7 1.7 0 0 0 .34 1.88l.06.06-2.18 2.18-.06-.06a1.7 1.7 0 0 0-1.88-.34 1.7 1.7 0 0 0-1.03 1.55V20.4h-3.08v-.13a1.7 1.7 0 0 0-1.03-1.55 1.7 1.7 0 0 0-1.88.34l-.06.06-2.18-2.18.06-.06A1.7 1.7 0 0 0 6.86 15a1.7 1.7 0 0 0-1.55-1.03h-.13v-3.08h.13A1.7 1.7 0 0 0 6.86 9.86 1.7 1.7 0 0 0 6.52 8l-.06-.06 2.18-2.18.06.06a1.7 1.7 0 0 0 1.88.34 1.7 1.7 0 0 0 1.03-1.55v-.13h3.08v.13a1.7 1.7 0 0 0 1.03 1.55 1.7 1.7 0 0 0 1.88-.34l.06-.06 2.18 2.18-.06.06a1.7 1.7 0 0 0-.34 1.88 1.7 1.7 0 0 0 1.55 1.03h.13v3.08h-.13A1.7 1.7 0 0 0 19.4 15Z" /></svg>
}

function HelpIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="2.3" strokeLinecap="round"><circle cx="12" cy="12" r="8.5" /><path d="M9.8 9.25a2.35 2.35 0 1 1 3.95 1.7c-.94.88-1.75 1.22-1.75 2.55M12 16.7h.01" /></svg>
}

function MoonIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-current"><path d="M20.65 15.42A8.75 8.75 0 0 1 8.58 3.35 8.75 8.75 0 1 0 20.65 15.42Z" /></svg>
}

function LanguageIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="2.1" strokeLinecap="round" strokeLinejoin="round"><path d="M4 5h10M9 3v2c0 5-2.25 8.5-5 10.5M5.5 10.5c1.3 1.55 3.1 2.85 5.5 3.75M14 19l3.25-9L20.5 19M15.3 16h3.9" /></svg>
}

function LogoutIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><path d="M10 4H5.7A1.7 1.7 0 0 0 4 5.7v12.6A1.7 1.7 0 0 0 5.7 20H10" /><path d="m14 8 4 4-4 4M18 12H9" /></svg>
}

function BackIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-6 w-6 fill-none stroke-current" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round"><path d="m14.5 5-7 7 7 7" /></svg>
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
    unreadMessageCount,
    unreadNotificationCount,
  } = useRealtime()
  const { language, setLanguage, setTheme, t, theme } = usePreferences()
  const location = useLocation()
  const navigate = useNavigate()
  const [searchQuery, setSearchQuery] = useState('')
  const [suggestions, setSuggestions] = useState<SearchSuggestions | null>(null)
  const [isSearchFocused, setIsSearchFocused] = useState(false)
  const [activeHeaderPopup, setActiveHeaderPopup] = useState<'menu' | 'notifications' | 'profile' | null>(null)
  const [notificationFilter, setNotificationFilter] = useState<'all' | 'unread'>('all')
  const [isNotificationMenuOpen, setIsNotificationMenuOpen] = useState(false)
  const [dismissedNotificationIds, setDismissedNotificationIds] = useState<ReadonlySet<string>>(() => new Set())
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null)
  const [displayName, setDisplayName] = useState(session!.user.username)
  const [isAppearanceOpen, setIsAppearanceOpen] = useState(false)
  const menuDropdownRef = useRef<HTMLDivElement>(null)
  const notificationDropdownRef = useRef<HTMLDivElement>(null)
  const profileDropdownRef = useRef<HTMLDivElement>(null)
  const initials = session!.user.username.slice(0, 2).toUpperCase()
  const zolaLightUrl = import.meta.env.VITE_ZOLA_LIGHT_URL ?? 'http://localhost:5175'
  const isNotificationsPage = location.pathname === '/notifications'
  const isMenuOpen = activeHeaderPopup === 'menu'
  const isNotificationsOpen = activeHeaderPopup === 'notifications'
  const isProfileOpen = activeHeaderPopup === 'profile'
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

  useEffect(() => {
    let isCurrent = true
    void usersApi.getCurrent()
      .then((profile) => {
        if (isCurrent) {
          setAvatarUrl(profile.avatarUrl)
          setDisplayName(profile.displayName)
        }
      })
      .catch(() => {
        if (isCurrent) {
          setAvatarUrl(null)
          setDisplayName(session!.user.username)
        }
      })

    return () => { isCurrent = false }
  }, [session?.user.id, session?.user.username])

  useEffect(() => {
    if (!activeHeaderPopup) return

    const activePopupRef = activeHeaderPopup === 'menu'
      ? menuDropdownRef
      : activeHeaderPopup === 'notifications'
        ? notificationDropdownRef
        : profileDropdownRef
    const closePopupWhenClickingOutside = (event: PointerEvent) => {
      if (!activePopupRef.current?.contains(event.target as Node)) {
        setActiveHeaderPopup(null)
        setIsNotificationMenuOpen(false)
        setIsAppearanceOpen(false)
      }
    }

    document.addEventListener('pointerdown', closePopupWhenClickingOutside, true)
    return () => document.removeEventListener('pointerdown', closePopupWhenClickingOutside, true)
  }, [activeHeaderPopup])

  const hasSuggestions = Boolean(suggestions &&
    (suggestions.people.length || suggestions.groups.length || suggestions.pages.length))

  const visibleNotifications = notificationFilter === 'unread'
    ? notifications.filter((notification) => !notification.isRead && !dismissedNotificationIds.has(notification.id))
    : notifications.filter((notification) => !dismissedNotificationIds.has(notification.id))

  const markAllNotificationsReadAndDismiss = () => {
    setDismissedNotificationIds((current) => new Set([...current, ...notifications.map((notification) => notification.id)]))
    markAllNotificationsRead()
    setIsNotificationMenuOpen(false)
  }

  return (
    <header className="fixed top-0 left-0 right-0 h-14 bg-surface border-b border-border flex items-center px-4 z-50">
      <div className="flex items-center gap-2 w-[280px] shrink-0 max-lg:hidden">
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

      <nav aria-label="Điều hướng chính" className="flex flex-1 items-center justify-center gap-1 self-stretch px-2 max-w-[680px] mx-auto max-lg:w-full">
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

      <div className="flex items-center gap-2 w-[280px] shrink-0 justify-end max-lg:hidden">
        <div ref={menuDropdownRef} className="relative">
          <button
            type="button"
            onClick={() => {
              setActiveHeaderPopup((current) => current === 'menu' ? null : 'menu')
              setIsNotificationMenuOpen(false)
            }}
            className="w-10 h-10 rounded-full bg-surface-2 flex items-center justify-center text-text hover:bg-[#4e4f50] transition-colors cursor-pointer border-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 focus-visible:ring-offset-surface"
            title="Menu"
            aria-label="Menu"
            aria-expanded={isMenuOpen}
          >
            <MenuIcon />
          </button>
          {isMenuOpen && <div className="absolute right-0 top-12 z-50 w-56 rounded-2xl border border-border bg-surface p-3 shadow-2xl">
            <p className="px-1 pb-2 text-sm font-semibold text-text">Tùy chỉnh</p>
            <div className="border-t border-border pt-3"><PreferenceControls /></div>
            <button type="button" onClick={() => void signOut()} className="mt-3 flex w-full items-center gap-2 rounded-lg border-0 bg-surface-2 px-3 py-2 text-left text-sm font-semibold text-text cursor-pointer transition-colors hover:bg-surface-hover">
              <span aria-hidden="true">↪</span>{t('signOut')}
            </button>
          </div>}
        </div>
        <a
          href={zolaLightUrl}
          className="relative flex h-10 w-10 items-center justify-center rounded-full border-0 bg-surface-2 text-text no-underline transition-colors hover:bg-[#4e4f50]"
          title="Zola Light"
          aria-label="Zola Light"
        >
            <ZolaLightIcon />
            {unreadMessageCount > 0 && <span className="absolute -top-1 -right-1 min-w-5 h-5 rounded-full bg-[#e41e3f] text-[10px] font-bold text-white flex items-center justify-center px-1">{unreadMessageCount > 99 ? '99+' : unreadMessageCount}</span>}
        </a>
        <div ref={notificationDropdownRef} className="relative">
          <button
            type="button"
            onClick={() => {
              if (isNotificationsPage) return
              setActiveHeaderPopup((current) => current === 'notifications' ? null : 'notifications')
              setIsNotificationMenuOpen(false)
            }}
            disabled={isNotificationsPage}
            className={`w-10 h-10 rounded-full flex items-center justify-center transition-colors border-none text-sm relative ${isNotificationsOpen || isNotificationsPage ? 'bg-primary text-white' : 'bg-surface-2 text-text hover:bg-[#4e4f50] cursor-pointer'} ${isNotificationsPage ? 'cursor-default' : ''}`}
            title={t('messageNotifications')}
            aria-expanded={isNotificationsOpen}
          >
            <BellIcon />
            {unreadNotificationCount > 0 && <span className="absolute -top-0.5 -right-0.5 min-w-5 h-5 rounded-full bg-[#e41e3f] text-[10px] font-bold text-white flex items-center justify-center px-1">{unreadNotificationCount > 99 ? '99+' : unreadNotificationCount}</span>}
          </button>
          {isNotificationsOpen && (
            <div className="absolute right-0 top-12 z-50 w-[min(24rem,calc(100vw-1rem))] overflow-hidden rounded-2xl border border-border bg-surface shadow-2xl">
              <div className="relative flex items-center justify-between px-4 pt-3"><h2 className="font-heading text-2xl font-bold text-text">{t('notifications')}</h2><button type="button" onClick={() => setIsNotificationMenuOpen((current) => !current)} className="grid h-9 w-9 place-items-center rounded-full border-0 bg-transparent text-xl text-text-muted cursor-pointer hover:bg-surface-2" title="Tùy chọn thông báo" aria-label="Tùy chọn thông báo" aria-expanded={isNotificationMenuOpen}>•••</button>{isNotificationMenuOpen && <div className="absolute right-4 top-12 z-10 w-56 rounded-xl border border-border bg-surface p-2 shadow-xl"><button type="button" onClick={markAllNotificationsReadAndDismiss} className="w-full rounded-lg border-0 bg-transparent px-3 py-2 text-left text-sm font-semibold text-text cursor-pointer hover:bg-surface-2">Đánh dấu tất cả là đã đọc</button></div>}</div>
              <div className="flex gap-2 px-4 pb-3 pt-2">
                <button type="button" onClick={() => setNotificationFilter('all')} className={`rounded-full border-0 px-3 py-2 text-sm font-semibold cursor-pointer ${notificationFilter === 'all' ? 'bg-primary/20 text-primary' : 'bg-transparent text-text hover:bg-surface-2'}`}>Tất cả</button>
                <button type="button" onClick={() => setNotificationFilter('unread')} className={`rounded-full border-0 px-3 py-2 text-sm font-semibold cursor-pointer ${notificationFilter === 'unread' ? 'bg-primary/20 text-primary' : 'bg-transparent text-text hover:bg-surface-2'}`}>Chưa đọc</button>
              </div>
              <div className="max-h-[calc(100vh-11rem)] overflow-y-auto px-2 pb-2">
                <div className="flex items-center justify-between px-2 pb-1"><h3 className="text-base font-bold text-text">Trước đó</h3><Link to="/notifications" onClick={() => setActiveHeaderPopup(null)} className="text-sm font-medium text-primary no-underline hover:underline">Xem tất cả</Link></div>
                {visibleNotifications.length === 0 ? <p className="px-4 py-6 text-center text-sm text-text-muted">{notificationFilter === 'unread' ? 'Bạn không có thông báo chưa đọc.' : t('allCaughtUp')}</p> : (
                  <>
                    {visibleNotifications.map((notification, index) => {
                      const presentation = getNotificationPresentation(notification)
                      const avatarTone = ['bg-[#87433b]', 'bg-[#5f7997]', 'bg-[#8e5b88]', 'bg-[#607b57]', 'bg-[#9b6c45]'][index % 5]
                      return <Link key={notification.id} to={presentation.destination} onClick={() => { markNotificationRead(notification.id); setActiveHeaderPopup(null) }} className={`relative flex gap-3 rounded-xl px-2 py-2.5 no-underline transition-colors hover:bg-surface-2 ${notification.isRead ? '' : 'bg-primary/10'}`}>
                        <span className={`relative flex h-14 w-14 shrink-0 items-center justify-center rounded-full text-sm font-bold text-white ${presentation.actor ? avatarTone : 'bg-surface-2 text-text-muted'}`}>{presentation.actor ? presentation.actor.slice(0, 2).toUpperCase() : '!'}<span className={`absolute -bottom-0.5 -right-0.5 grid h-6 w-6 place-items-center rounded-full border-2 border-surface text-[11px] font-bold text-white ${presentation.badge.className}`}>{presentation.badge.icon}</span></span>
                        <span className="min-w-0 flex-1 pr-4"><span className="block text-sm leading-5 text-text">{presentation.actor ? <><strong>{presentation.actor}</strong> {presentation.message}</> : presentation.text}</span><span className={`mt-0.5 block text-xs font-semibold ${notification.isRead ? 'text-text-light' : 'text-primary'}`}>{formatPostTimestamp(notification.createdAtUtc).compact}</span></span>
                        {!notification.isRead && <span className="absolute right-3 top-1/2 h-3 w-3 -translate-y-1/2 rounded-full bg-primary" />}
                      </Link>
                    })}
                    {hasMoreNotifications && <button type="button" onClick={loadMoreNotifications} disabled={isLoadingMoreNotifications} className="mt-2 w-full rounded-lg border-0 bg-surface-2 px-4 py-2.5 text-sm font-semibold text-text cursor-pointer hover:bg-surface-3 disabled:cursor-wait">{isLoadingMoreNotifications ? t('loading') : 'Xem thông báo trước đó'}</button>}
                  </>
                )}
              </div>
            </div>
          )}
        </div>
        <div ref={profileDropdownRef} className="relative">
          <button
            type="button"
            onClick={() => {
              setActiveHeaderPopup((current) => current === 'profile' ? null : 'profile')
              setIsNotificationMenuOpen(false)
              setIsAppearanceOpen(false)
            }}
            className={`relative flex h-10 w-10 shrink-0 items-center justify-center overflow-visible rounded-full border-none text-[11px] font-bold text-white transition ${isProfileOpen ? 'ring-2 ring-primary ring-offset-2 ring-offset-surface' : 'hover:brightness-110 cursor-pointer'} bg-primary`}
            title={t('profile')}
            aria-label={t('profile')}
            aria-expanded={isProfileOpen}
          >
            <span className="flex h-full w-full items-center justify-center overflow-hidden rounded-full">{avatarUrl ? <img src={resolveProfileImageUrl(avatarUrl)} alt="" className="h-full w-full object-cover" /> : initials}</span>
            <span className="absolute -bottom-0.5 -right-0.5 flex h-4 w-4 items-center justify-center rounded-full border-2 border-surface bg-surface-2 text-text"><ChevronDownIcon /></span>
          </button>
          {isProfileOpen && <div className="absolute right-0 top-12 z-50 w-[min(24rem,calc(100vw-1rem))] overflow-hidden rounded-2xl border border-border bg-surface shadow-2xl">
            <div className={`flex w-[200%] transition-transform duration-300 ease-out ${isAppearanceOpen ? '-translate-x-1/2' : 'translate-x-0'}`}>
              <section className="w-1/2 shrink-0 p-3">
                <Link to="/profile" onClick={() => setActiveHeaderPopup(null)} className="block rounded-xl p-1.5 no-underline hover:bg-surface-2">
                  <div className="flex items-center gap-3 rounded-xl border-2 border-primary p-2">
                    <span className="flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-sm font-bold text-white">{avatarUrl ? <img src={resolveProfileImageUrl(avatarUrl)} alt="" className="h-full w-full object-cover" /> : initials}</span>
                    <span className="min-w-0"><span className="block truncate text-base font-bold text-text">{displayName}</span><span className="block truncate text-sm text-text-muted">@{session!.user.username}</span></span>
                  </div>
                  <span className="mt-2 flex items-center justify-center gap-2 rounded-lg bg-surface-2 px-3 py-2 text-center text-sm font-semibold text-text"><ProfileMenuIcon />Xem trang cá nhân</span>
                </Link>
                <div className="my-2 border-t border-border" />
                <Link to="/settings/privacy" onClick={() => setActiveHeaderPopup(null)} className="flex items-center gap-3 rounded-xl px-2 py-2.5 text-sm text-text no-underline hover:bg-surface-2"><span className="grid h-9 w-9 place-items-center rounded-full bg-surface-2"><SettingsIcon /></span><span className="flex-1 font-medium">Cài đặt và quyền riêng tư</span><span className="text-2xl text-text-muted">›</span></Link>
                <Link to="/settings/security" onClick={() => setActiveHeaderPopup(null)} className="flex items-center gap-3 rounded-xl px-2 py-2.5 text-sm text-text no-underline hover:bg-surface-2"><span className="grid h-9 w-9 place-items-center rounded-full bg-surface-2"><HelpIcon /></span><span className="flex-1 font-medium">Trợ giúp và bảo mật</span><span className="text-2xl text-text-muted">›</span></Link>
                <button type="button" onClick={() => setIsAppearanceOpen(true)} className="flex w-full items-center gap-3 rounded-xl border-0 bg-transparent px-2 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span className="grid h-9 w-9 place-items-center rounded-full bg-surface-2"><MoonIcon /></span><span className="flex-1 font-medium">Màn hình và trợ năng</span><span className="text-2xl text-text-muted">›</span></button>
                <button type="button" onClick={() => void signOut()} className="flex w-full items-center gap-3 rounded-xl border-0 bg-transparent px-2 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span className="grid h-9 w-9 place-items-center rounded-full bg-surface-2"><LogoutIcon /></span><span className="font-medium">{t('signOut')}</span></button>
                <p className="px-2 pt-2 text-xs leading-4 text-text-light">Quyền riêng tư · Điều khoản · Quảng cáo · Cookie · Thêm</p>
              </section>
              <section className="w-1/2 shrink-0 p-3">
                <div className="mb-3 flex items-center gap-2"><button type="button" onClick={() => setIsAppearanceOpen(false)} className="grid h-10 w-10 place-items-center rounded-full border-2 border-primary bg-surface-2 text-text cursor-pointer hover:bg-surface-3" aria-label="Quay lại"><BackIcon /></button><h2 className="text-2xl font-bold text-text">Màn hình và trợ năng</h2></div>
                <div className="flex gap-3 px-1 py-2"><span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-surface-2"><MoonIcon /></span><div><h3 className="font-bold text-text">Chế độ tối</h3><p className="mt-1 text-sm leading-5 text-text-muted">Điều chỉnh giao diện để giảm độ chói và cho đôi mắt được nghỉ ngơi.</p></div></div>
                <div className="mt-2 space-y-1 px-1">
                  <button type="button" onClick={() => setTheme('light')} className="flex w-full items-center justify-between rounded-lg border-0 bg-transparent px-3 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span>Tắt</span><span className={`h-5 w-5 rounded-full border-2 ${theme === 'light' ? 'border-primary bg-primary shadow-[inset_0_0_0_3px_var(--color-surface)]' : 'border-text-light'}`} /></button>
                  <button type="button" onClick={() => setTheme('dark')} className="flex w-full items-center justify-between rounded-lg border-0 bg-transparent px-3 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span>Bật</span><span className={`h-5 w-5 rounded-full border-2 ${theme === 'dark' ? 'border-primary bg-primary shadow-[inset_0_0_0_3px_var(--color-surface)]' : 'border-text-light'}`} /></button>
                </div>
                <div className="my-3 border-t border-border" />
                <div className="flex gap-3 px-1 py-2"><span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-surface-2"><LanguageIcon /></span><div><h3 className="font-bold text-text">Ngôn ngữ</h3><p className="mt-1 text-sm leading-5 text-text-muted">Chọn ngôn ngữ hiển thị của Fookbase.</p></div></div>
                <div className="mt-2 space-y-1 px-1">
                  <button type="button" onClick={() => setLanguage('vi')} className="flex w-full items-center justify-between rounded-lg border-0 bg-transparent px-3 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span>Tiếng Việt</span><span className={`h-5 w-5 rounded-full border-2 ${language === 'vi' ? 'border-primary bg-primary shadow-[inset_0_0_0_3px_var(--color-surface)]' : 'border-text-light'}`} /></button>
                  <button type="button" onClick={() => setLanguage('en')} className="flex w-full items-center justify-between rounded-lg border-0 bg-transparent px-3 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span>English</span><span className={`h-5 w-5 rounded-full border-2 ${language === 'en' ? 'border-primary bg-primary shadow-[inset_0_0_0_3px_var(--color-surface)]' : 'border-text-light'}`} /></button>
                </div>
              </section>
            </div>
          </div>}
        </div>
      </div>
    </header>
  )
}
