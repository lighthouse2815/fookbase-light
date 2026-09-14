import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useRealtime } from '../realtime/useRealtime'
import { PreferenceControls, usePreferences } from '../preferences'
import { searchApi, type SearchSuggestions } from '../api/search'
import { messagesApi, type Conversation } from '../api/messages'
import { resolveProfileImageUrl, usersApi, type UserProfile } from '../api/users'

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

function formatMessageTime(value: string) {
  const minutes = Math.floor((Date.now() - new Date(value).getTime()) / 60_000)
  if (minutes < 1) return 'Vừa xong'
  if (minutes < 60) return `${minutes} phút`
  if (minutes < 1_440) return `${Math.floor(minutes / 60)} giờ`
  if (minutes < 10_080) return `${Math.floor(minutes / 1_440)} ngày`
  return new Intl.DateTimeFormat('vi-VN', { day: 'numeric', month: 'numeric' }).format(new Date(value))
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
  const { t } = usePreferences()
  const location = useLocation()
  const navigate = useNavigate()
  const [searchQuery, setSearchQuery] = useState('')
  const [suggestions, setSuggestions] = useState<SearchSuggestions | null>(null)
  const [isSearchFocused, setIsSearchFocused] = useState(false)
  const [activeHeaderPopup, setActiveHeaderPopup] = useState<'menu' | 'messages' | 'notifications' | null>(null)
  const [notificationFilter, setNotificationFilter] = useState<'all' | 'unread'>('all')
  const [isNotificationMenuOpen, setIsNotificationMenuOpen] = useState(false)
  const [dismissedNotificationIds, setDismissedNotificationIds] = useState<ReadonlySet<string>>(() => new Set())
  const [messageConversations, setMessageConversations] = useState<Conversation[]>([])
  const [messageProfiles, setMessageProfiles] = useState<Record<string, UserProfile>>({})
  const [messageSearch, setMessageSearch] = useState('')
  const [messageFilter, setMessageFilter] = useState<'all' | 'unread'>('all')
  const [isMessagesLoading, setIsMessagesLoading] = useState(false)
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null)
  const menuDropdownRef = useRef<HTMLDivElement>(null)
  const zolaLightDropdownRef = useRef<HTMLDivElement>(null)
  const notificationDropdownRef = useRef<HTMLDivElement>(null)
  const initials = session!.user.username.slice(0, 2).toUpperCase()
  const zolaLightUrl = import.meta.env.VITE_ZOLA_LIGHT_URL ?? 'http://localhost:5175'
  const isNotificationsPage = location.pathname === '/notifications'
  const isMenuOpen = activeHeaderPopup === 'menu'
  const isMessagesOpen = activeHeaderPopup === 'messages'
  const isNotificationsOpen = activeHeaderPopup === 'notifications'
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
        if (isCurrent) setAvatarUrl(profile.avatarUrl)
      })
      .catch(() => {
        if (isCurrent) setAvatarUrl(null)
      })

    return () => { isCurrent = false }
  }, [session?.user.id])

  useEffect(() => {
    if (!isMessagesOpen) return

    let isCurrent = true
    void messagesApi.getConversations()
      .then(async (page) => {
        if (!isCurrent) return
        setMessageConversations(page.items)

        const participantUserIds = [...new Set(page.items.map((conversation) => conversation.participantUserId))]
        const profiles = await Promise.allSettled(participantUserIds.map((userId) => usersApi.getById(userId)))
        if (!isCurrent) return
        setMessageProfiles((current) => ({
          ...current,
          ...Object.fromEntries(profiles.flatMap((result) => result.status === 'fulfilled' ? [[result.value.userId, result.value]] : [])),
        }))
      })
      .catch(() => {
        if (isCurrent) setMessageConversations([])
      })
      .finally(() => {
        if (isCurrent) setIsMessagesLoading(false)
      })

    return () => { isCurrent = false }
  }, [isMessagesOpen])

  useEffect(() => {
    if (!activeHeaderPopup) return

    const activePopupRef = activeHeaderPopup === 'menu'
      ? menuDropdownRef
      : activeHeaderPopup === 'messages'
        ? zolaLightDropdownRef
        : notificationDropdownRef
    const closePopupWhenClickingOutside = (event: PointerEvent) => {
      if (!activePopupRef.current?.contains(event.target as Node)) {
        setActiveHeaderPopup(null)
        setIsNotificationMenuOpen(false)
      }
    }

    document.addEventListener('pointerdown', closePopupWhenClickingOutside, true)
    return () => document.removeEventListener('pointerdown', closePopupWhenClickingOutside, true)
  }, [activeHeaderPopup])

  const hasSuggestions = Boolean(suggestions &&
    (suggestions.people.length || suggestions.groups.length || suggestions.pages.length))

  const visibleMessageConversations = useMemo(() => {
    const query = messageSearch.trim().toLocaleLowerCase('vi-VN')
    return messageConversations.filter((conversation) => {
      if (messageFilter === 'unread' && conversation.unreadCount === 0) return false
      const profile = messageProfiles[conversation.participantUserId]
      return !query || [profile?.displayName, profile?.username, conversation.lastMessage?.content]
        .filter(Boolean)
        .some((value) => value!.toLocaleLowerCase('vi-VN').includes(query))
    })
  }, [messageConversations, messageFilter, messageProfiles, messageSearch])

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
    switch (notification.type) {
      case 'FriendRequestReceived':
        return 'đã gửi cho bạn lời mời kết bạn.'
      case 'FriendRequestAccepted':
        return 'đã chấp nhận lời mời kết bạn của bạn.'
      case 'UserFollowed':
        return t('startedFollowingYou')
      case 'PostReaction':
        return 'đã bày tỏ cảm xúc về bài viết của bạn.'
      case 'PostComment':
        return 'đã bình luận về bài viết của bạn.'
      case 'CommentReaction':
        return 'đã bày tỏ cảm xúc về bình luận của bạn.'
      case 'PostShared':
        return 'đã chia sẻ bài viết của bạn.'
      case 'PostMention':
        return 'đã nhắc đến bạn trong một bài viết.'
      case 'CommentMention':
        return 'đã nhắc đến bạn trong một bình luận.'
      case 'GroupInvite':
        return 'đã mời bạn tham gia một nhóm.'
      case 'GroupJoinApproved':
        return 'đã chấp nhận yêu cầu tham gia nhóm của bạn.'
      case 'StoryReaction':
        return 'đã bày tỏ cảm xúc về Story của bạn.'
      case 'PageRoleInvite':
        return 'đã mời bạn quản lý một Trang.'
      default:
        return 'đã gửi cho bạn một thông báo.'
    }
  }

  const notificationBadge = (notification: typeof notifications[number]) => {
    if (notification.type === 'PostReaction' || notification.type === 'CommentReaction' || notification.type === 'StoryReaction') return { icon: '♥', className: 'bg-[#f02849]' }
    if (notification.type === 'PostComment' || notification.type === 'CommentMention') return { icon: '●', className: 'bg-[#1877f2]' }
    if (notification.type === 'FriendRequestReceived' || notification.type === 'FriendRequestAccepted' || notification.type === 'UserFollowed') return { icon: '♟', className: 'bg-[#31a24c]' }
    return { icon: '●', className: 'bg-[#1877f2]' }
  }

  const notificationTime = (createdAtUtc: string) => {
    return new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(new Date(createdAtUtc))
  }

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
        <div ref={zolaLightDropdownRef} className="relative">
          <button
            type="button"
            onClick={() => {
              const willOpenMessages = activeHeaderPopup !== 'messages'
              setActiveHeaderPopup(willOpenMessages ? 'messages' : null)
              if (willOpenMessages) setIsMessagesLoading(true)
              setIsNotificationMenuOpen(false)
            }}
            className={`relative flex h-10 w-10 items-center justify-center rounded-full border-0 text-text transition-colors ${isMessagesOpen ? 'bg-primary text-white' : 'bg-surface-2 hover:bg-[#4e4f50] cursor-pointer'}`}
            title={t('messages')}
            aria-label={t('messages')}
            aria-expanded={isMessagesOpen}
          >
            <ZolaLightIcon />
            {unreadMessageCount > 0 && <span className="absolute -top-1 -right-1 min-w-5 h-5 rounded-full bg-[#e41e3f] text-[10px] font-bold text-white flex items-center justify-center px-1">{unreadMessageCount > 99 ? '99+' : unreadMessageCount}</span>}
          </button>
          {isMessagesOpen && <div className="absolute right-0 top-12 z-50 flex h-[min(42rem,calc(100vh-5rem))] w-[min(25rem,calc(100vw-1rem))] flex-col overflow-hidden rounded-2xl border border-border bg-surface shadow-2xl">
            <div className="flex items-center justify-between px-4 pb-2 pt-3">
              <h2 className="font-heading text-2xl font-bold text-text">Đoạn chat</h2>
              <div className="flex items-center gap-1 text-text-muted">
                <button type="button" className="grid h-9 w-9 place-items-center rounded-full border-0 bg-transparent text-xl cursor-pointer hover:bg-surface-2" title="Tùy chọn">•••</button>
                <a href={zolaLightUrl} className="grid h-9 w-9 place-items-center rounded-full text-lg text-text-muted no-underline hover:bg-surface-2" title="Mở Zola Light">↗</a>
                <a href={`${zolaLightUrl}?new=1`} className="grid h-9 w-9 place-items-center rounded-full text-lg text-text-muted no-underline hover:bg-surface-2" title="Tin nhắn mới">✎</a>
              </div>
            </div>
            <div className="px-3 pb-3">
              <label className="relative block">
                <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-light">⌕</span>
                <input type="search" value={messageSearch} onChange={(event) => setMessageSearch(event.target.value)} placeholder="Tìm kiếm trên Zola Light" className="h-10 w-full rounded-full border-0 bg-surface-2 py-2 pl-9 pr-3 text-sm text-text outline-none placeholder:text-text-light focus:input-focus" />
              </label>
            </div>
            <div className="flex gap-2 px-4 pb-2">
              <button type="button" onClick={() => setMessageFilter('all')} className={`rounded-full border-0 px-3 py-2 text-sm font-semibold cursor-pointer ${messageFilter === 'all' ? 'bg-primary/20 text-primary' : 'bg-transparent text-text hover:bg-surface-2'}`}>Tất cả</button>
              <button type="button" onClick={() => setMessageFilter('unread')} className={`rounded-full border-0 px-3 py-2 text-sm font-semibold cursor-pointer ${messageFilter === 'unread' ? 'bg-primary/20 text-primary' : 'bg-transparent text-text hover:bg-surface-2'}`}>Chưa đọc</button>
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto px-2 pb-2">
              {isMessagesLoading && <p className="px-3 py-6 text-center text-sm text-text-muted">Đang tải đoạn chat...</p>}
              {!isMessagesLoading && visibleMessageConversations.length === 0 && <p className="px-3 py-6 text-center text-sm text-text-muted">Không có đoạn chat phù hợp.</p>}
              {!isMessagesLoading && visibleMessageConversations.map((conversation) => {
                const profile = messageProfiles[conversation.participantUserId]
                const name = profile?.displayName ?? profile?.username ?? 'Người dùng'
                const preview = conversation.lastMessage?.content ?? 'Bắt đầu cuộc trò chuyện'
                return <a key={conversation.id} href={`${zolaLightUrl}?conversation=${conversation.id}`} onClick={() => setActiveHeaderPopup(null)} className="relative flex items-center gap-3 rounded-xl px-2 py-2.5 text-text no-underline hover:bg-surface-2">
                  <span className="flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-full bg-surface-3 text-sm font-bold text-text-muted">{profile?.avatarUrl ? <img src={resolveProfileImageUrl(profile.avatarUrl)} alt="" className="h-full w-full object-cover" /> : name.slice(0, 2).toUpperCase()}</span>
                  <span className="min-w-0 flex-1"><span className={`block truncate text-sm ${conversation.unreadCount > 0 ? 'font-bold text-text' : 'font-medium text-text-muted'}`}>{name}</span><span className={`block truncate text-xs ${conversation.unreadCount > 0 ? 'font-semibold text-text' : 'text-text-light'}`}>{preview} · {formatMessageTime(conversation.lastMessageAtUtc)}</span></span>
                  {conversation.unreadCount > 0 && <span className="h-3 w-3 shrink-0 rounded-full bg-primary" />}
                </a>
              })}
            </div>
            <a href={zolaLightUrl} onClick={() => setActiveHeaderPopup(null)} className="border-t border-border px-4 py-3 text-center text-sm font-semibold text-primary no-underline hover:bg-surface-2">Xem tất cả trong Zola Light</a>
          </div>}
        </div>
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
                      const actor = notification.actorDisplayName ?? notification.actorUsername ?? t('user')
                      const badge = notificationBadge(notification)
                      const avatarTone = ['bg-[#87433b]', 'bg-[#5f7997]', 'bg-[#8e5b88]', 'bg-[#607b57]', 'bg-[#9b6c45]'][index % 5]
                      return <Link key={notification.id} to={notificationDestination(notification)} onClick={() => { markNotificationRead(notification.id); setActiveHeaderPopup(null) }} className={`relative flex gap-3 rounded-xl px-2 py-2.5 no-underline transition-colors hover:bg-surface-2 ${notification.isRead ? '' : 'bg-primary/10'}`}>
                        <span className={`relative flex h-14 w-14 shrink-0 items-center justify-center rounded-full text-sm font-bold text-white ${avatarTone}`}>{actor.slice(0, 2).toUpperCase()}<span className={`absolute -bottom-0.5 -right-0.5 grid h-6 w-6 place-items-center rounded-full border-2 border-surface text-[11px] font-bold text-white ${badge.className}`}>{badge.icon}</span></span>
                        <span className="min-w-0 flex-1 pr-4"><span className="block text-sm leading-5 text-text"><strong>{actor}</strong> {notificationText(notification)}</span><span className={`mt-0.5 block text-xs font-semibold ${notification.isRead ? 'text-text-light' : 'text-primary'}`}>{notificationTime(notification.createdAtUtc)}</span></span>
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
        <Link
          to="/profile"
          className="relative w-10 h-10 rounded-full flex items-center justify-center overflow-visible text-[11px] font-bold text-white shrink-0 cursor-pointer border-none hover:brightness-110 transition no-underline bg-primary"
          title={t('profile')}
        >
          <span className="flex h-full w-full items-center justify-center overflow-hidden rounded-full">{avatarUrl ? <img src={resolveProfileImageUrl(avatarUrl)} alt="" className="h-full w-full object-cover" /> : initials}</span>
          <span className="absolute -bottom-0.5 -right-0.5 flex h-4 w-4 items-center justify-center rounded-full border-2 border-surface bg-surface-2 text-text"><ChevronDownIcon /></span>
        </Link>
      </div>
    </header>
  )
}
