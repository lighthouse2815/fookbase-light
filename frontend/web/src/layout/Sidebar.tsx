import { useEffect, useState, type ReactNode } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { groupsApi, type Group } from '../api/groups'
import { resolveProfileImageUrl, usersApi } from '../api/users'
import { useAuth } from '../auth/useAuth'
import { usePreferences } from '../preferences'
import { Mascot } from 'page-mascot'

interface NavItem {
  path: string
  icon: SidebarIconName
  label: string
  external?: boolean
}

type SidebarIconName = 'friends' | 'groups' | 'memories' | 'saved' | 'reels' | 'games' | 'music' | 'feed' | 'messages' | 'ai' | 'birthdays' | 'events' | 'pages'

const musicForYouUrl = 'https://music-for-you.pages.dev/'

const primaryItems: NavItem[] = [
  { path: '/explore', icon: 'friends', label: 'Bạn bè' },
  { path: '/groups', icon: 'groups', label: 'Nhóm' },
  { path: '/memories', icon: 'memories', label: 'Kỷ niệm' },
  { path: '/saved', icon: 'saved', label: 'Đã lưu' },
  { path: '/reels', icon: 'reels', label: 'Thước phim' },
  { path: '/games', icon: 'games', label: 'Chơi game' },
  { path: musicForYouUrl, icon: 'music', label: 'Âm nhạc', external: true },
  { path: '/ai-chat', icon: 'ai', label: 'Trợ lý AI' },
]

const moreItems: NavItem[] = [
  { path: '/feed', icon: 'feed', label: 'Bảng feed' },
  { path: '/birthdays', icon: 'birthdays', label: 'Sinh nhật' },
  { path: '/events', icon: 'events', label: 'Sự kiện' },
  { path: '/pages', icon: 'pages', label: 'Trang' },
]

function SidebarIcon({ name }: { name: SidebarIconName }) {
  const icons: Record<SidebarIconName, ReactNode> = {
    friends: <><circle cx="9" cy="8" r="4" fill="#1877f2" /><circle cx="17" cy="10" r="3" fill="#40c9b5" /><path d="M2.5 21c.55-4.25 3.18-6.5 6.5-6.5s5.95 2.25 6.5 6.5" fill="#1877f2" /><path d="M14 20.5c.3-2.95 2.02-4.55 4.75-4.55 1.45 0 2.47.42 3.25 1.17v3.38H14Z" fill="#40c9b5" /></>,
    groups: <><circle cx="12" cy="12" r="11" fill="#2d88ff" /><circle cx="12" cy="9" r="3" fill="white" /><circle cx="6.8" cy="10.2" r="2" fill="white" opacity=".9" /><circle cx="17.2" cy="10.2" r="2" fill="white" opacity=".9" /><path d="M5.3 18.3c.55-3.08 2.72-4.68 6.7-4.68s6.15 1.6 6.7 4.68" fill="white" /></>,
    memories: <><circle cx="12" cy="12" r="11" fill="#2d9bf0" /><circle cx="12" cy="12" r="7.3" fill="white" /><path d="M12 7.6v4.7l3 1.9" fill="none" stroke="#2d9bf0" strokeWidth="2" strokeLinecap="round" /></>,
    saved: <><path d="M5 2.5h14v19L12 17l-7 4.5v-19Z" fill="#ca43dd" /><path d="M5 2.5h14v7.7c-4.62.25-9.28-1.26-14-4.55V2.5Z" fill="#f05c9f" opacity=".8" /></>,
    reels: <><rect x="2" y="3" width="20" height="18" rx="4" fill="#f05285" /><path d="m3 8 5-5m2 5 5-5m2 5 4-4" stroke="white" strokeWidth="2" /><path d="m10 10 5 3-5 3v-6Z" fill="white" /></>,
    games: <><path d="M5.7 9.2h12.6c2.75 0 4.35 2.08 3.8 4.72l-1.05 4.82c-.42 1.94-2.36 2.9-4.08 2.03l-2.86-1.44a4.55 4.55 0 0 0-4.2 0l-2.86 1.44c-1.72.87-3.66-.1-4.08-2.03L1.9 13.92C1.35 11.28 2.95 9.2 5.7 9.2Z" fill="#1877f2" /><path d="M7 14h4m-2-2v4" stroke="white" strokeWidth="1.8" strokeLinecap="round" /><circle cx="17.2" cy="13.2" r="1.15" fill="#f7d046" /><circle cx="19.25" cy="15.25" r="1.15" fill="#f7d046" /></>,
    music: <><circle cx="12" cy="12" r="11" fill="#8b5cf6" /><path d="M15.8 5.5v9.15a3.15 3.15 0 1 1-1.7-2.8V8.25l5-1.2v6.4a3.15 3.15 0 1 1-1.7-2.8v-5.15l-1.6.4Z" fill="white" /></>,
    feed: <><rect x="2" y="3" width="20" height="18" rx="3" fill="#3d9df5" /><rect x="5" y="6" width="14" height="7" rx="1.5" fill="white" /><circle cx="7.4" cy="9.5" r="1.35" fill="#6db6f5" /><path d="M10 8h6m-6 2.7h4" stroke="#728294" strokeWidth="1.25" strokeLinecap="round" /><circle cx="17.5" cy="17" r="3.5" fill="#e8f3ff" /><path d="M17.5 15.2v1.95l1.2.72" fill="none" stroke="#3d9df5" strokeWidth="1.2" strokeLinecap="round" /></>,
    messages: <><circle cx="12" cy="12" r="11" fill="#1877f2" /><path d="M5.5 13.5 10 8.8l2.7 2.5 4.8-2.5-4.5 4.7-2.75-2.48-4.75 2.48Z" fill="white" /></>,
    ai: <><circle cx="12" cy="12" r="11" fill="#7654d9" /><path d="m12 4.6.9 3.55 3.55.9-3.55.9L12 13.5l-.9-3.55-3.55-.9 3.55-.9L12 4.6Zm5.2 8.25.53 2.1 2.1.53-2.1.53-.53 2.1-.53-2.1-2.1-.53 2.1-.53.53-2.1ZM7.1 14.9l.42 1.68 1.68.42-1.68.42-.42 1.68-.42-1.68-1.68-.42 1.68-.42.42-1.68Z" fill="white" /></>,
    birthdays: <><rect x="3" y="7" width="18" height="14" rx="2" fill="#2d9bf0" /><path d="M12 7v14M3 11.5h18" stroke="white" strokeWidth="2" /><path d="M12 7c-3.5-1.1-4.5-4.2-2.05-4.2 1.35 0 2.05 1.43 2.05 4.2Zm0 0c3.5-1.1 4.5-4.2 2.05-4.2C12.7 2.8 12 4.23 12 7Z" fill="#e84578" /></>,
    events: <><rect x="3" y="4" width="18" height="17" rx="2" fill="white" /><path d="M3 8h18" stroke="#ef496b" strokeWidth="4" /><path d="M7 2v4m10-4v4" stroke="#ef496b" strokeWidth="2" strokeLinecap="round" /><path d="m12 11 1.15 2.34 2.58.37-1.87 1.82.44 2.57L12 16.88 9.7 18.1l.44-2.57-1.87-1.82 2.58-.37L12 11Z" fill="#4e4f50" /></>,
    pages: <><path d="M4 3h8.5v17H4z" fill="#1877f2" /><path d="M12.5 5H21v14h-8.5z" fill="#ef7b35" /><path d="M5.5 3v19" stroke="white" strokeWidth="1.2" /></>,
  }

  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-8 w-8 shrink-0">{icons[name]}</svg>
}

function SidebarLink({ item }: { item: NavItem }) {
  if (item.external) {
    return <a
      href={item.path}
      target="_blank"
      rel="noopener noreferrer"
      className="group flex w-full items-center gap-3 rounded-lg px-2 py-2 text-left no-underline transition-colors hover:bg-surface-2"
    >
      <SidebarIcon name={item.icon} />
      <span className="text-[15px] font-semibold text-text">{item.label}</span>
    </a>
  }

  return <NavLink
    to={item.path}
    className={({ isActive }) => [
      'group flex w-full items-center gap-3 rounded-lg px-2 py-2 text-left no-underline transition-colors',
      isActive ? 'bg-surface-2' : 'hover:bg-surface-2',
    ].join(' ')}
  >
    <SidebarIcon name={item.icon} />
    <span className="text-[15px] font-semibold text-text">{item.label}</span>
  </NavLink>
}

export function SidebarLinks({ expanded = false }: { expanded?: boolean }) {
  const zolaLightUrl = import.meta.env.VITE_ZOLA_LIGHT_URL ?? 'http://localhost:5175'
  return <>
        {primaryItems.map((item) => <SidebarLink key={item.path} item={item} />)}

        {expanded && <>
          <a href={zolaLightUrl} className="group flex w-full items-center gap-3 rounded-lg px-2 py-2 text-left no-underline transition-colors hover:bg-surface-2">
            <SidebarIcon name="messages" />
            <span className="text-[15px] font-semibold text-text">Zola Light</span>
          </a>
          {moreItems.map((item) => <SidebarLink key={item.path} item={item} />)}
        </>}

  </>
}

export default function Sidebar({ alignWithCenteredFeed = false }: { alignWithCenteredFeed?: boolean }) {
  const { session } = useAuth()
  const { t } = usePreferences()
  const [isExpanded, setIsExpanded] = useState(false)
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null)
  const fallbackDisplayName = session!.user.username.includes('@') ? 'Tài khoản của bạn' : session!.user.username
  const [displayName, setDisplayName] = useState(fallbackDisplayName)
  const [shortcuts, setShortcuts] = useState<Group[]>([])
  const [failedShortcutCoverIds, setFailedShortcutCoverIds] = useState<Set<string>>(new Set())
  const initials = displayName.slice(0, 2).toUpperCase()

  useEffect(() => {
    let isCurrent = true
    void Promise.all([usersApi.getCurrent(), groupsApi.getMine()])
      .then(([profile, groups]) => {
        if (!isCurrent) return
        setAvatarUrl(profile.avatarUrl)
        setDisplayName(profile.displayName)
        setShortcuts(groups.items.slice(0, 5))
        setFailedShortcutCoverIds(new Set())
      })
      .catch(() => undefined)

    return () => { isCurrent = false }
  }, [session?.user.id])

  return (
    <aside className={`fixed ${alignWithCenteredFeed ? 'left-[max(1rem,calc(50%-700px))]' : 'left-[max(1rem,calc(50%-520px))]'} top-14 z-40 flex h-[calc(100vh-56px)] w-[280px] flex-col overflow-y-auto bg-bg px-3 max-xl:hidden`}>
      <nav className="flex flex-col gap-0.5 py-3" aria-label="Lối tắt">
        <Link
          to="/profile"
          className="group flex w-full items-center gap-3 rounded-lg px-2 py-2 no-underline transition-colors hover:bg-surface-2"
        >
          <span className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-[11px] font-bold text-white">
            {avatarUrl ? <img src={resolveProfileImageUrl(avatarUrl)} alt="" className="h-full w-full object-cover" /> : initials}
          </span>
          <span className="text-[15px] font-semibold text-text">{displayName}</span>
        </Link>

        <SidebarLinks expanded={isExpanded} />

        <button
          type="button"
          onClick={() => setIsExpanded((current) => !current)}
          className="flex w-full items-center gap-3 rounded-lg border-0 bg-transparent px-2 py-2 text-left text-text cursor-pointer transition-colors hover:bg-surface-2"
          aria-expanded={isExpanded}
        >
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-surface-2 text-lg" aria-hidden="true">{isExpanded ? '⌃' : '⌄'}</span>
          <span className="text-[15px] font-semibold">{isExpanded ? 'Ẩn bớt' : 'Xem thêm'}</span>
        </button>
      </nav>

      {shortcuts.length > 0 && <section className="border-t border-border py-3">
        <h2 className="px-2 pb-1 text-[17px] font-bold text-text-muted">Lối tắt của bạn</h2>
        <div className="flex flex-col gap-0.5">
          {shortcuts.map((group) => {
            const coverUrl = group.coverUrl && !failedShortcutCoverIds.has(group.id) ? resolveProfileImageUrl(group.coverUrl) : null

            return <Link key={group.id} to={`/groups/${group.id}`} className="flex items-center gap-3 rounded-lg px-2 py-2 no-underline transition-colors hover:bg-surface-2">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-surface-2 text-base text-text">{coverUrl ? <img src={coverUrl} alt="" className="h-full w-full object-cover" onError={() => setFailedShortcutCoverIds((current) => new Set(current).add(group.id))} /> : '👥'}</span>
              <span className="line-clamp-2 text-[15px] font-semibold leading-5 text-text">{group.name}</span>
            </Link>
          })}
        </div>
      </section>}

      <div className="mx-1 my-2 flex items-center gap-3 rounded-2xl border border-border bg-surface p-2.5 shadow-sm transition hover:border-primary/40">
        <Mascot
          directions="/mascots/fox-directions.webp"
          reactions="/mascots/fox-reactions.webp"
          size={52}
          label="Fooky bạn đồng hành"
        />
        <div className="min-w-0 flex-1">
          <p className="text-xs font-bold text-text">Fooky bạn đồng hành</p>
          <p className="truncate text-[11px] text-text-muted">Nhấn để chạm Fooky ✨</p>
        </div>
      </div>

      <footer className="mt-auto px-2 py-4 text-[11px] leading-relaxed text-text-light">
        {t('appearance')} · {t('language')} · © 2026 Fookbase · Quyền riêng tư · Điều khoản
      </footer>
    </aside>
  )
}
