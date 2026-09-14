import { useEffect, useState } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { groupsApi, type Group } from '../api/groups'
import { resolveProfileImageUrl, usersApi } from '../api/users'
import { useAuth } from '../auth/useAuth'
import { usePreferences } from '../preferences'

interface NavItem {
  path: string
  emoji: string
  label: string
}

const primaryItems: NavItem[] = [
  { path: '/explore', emoji: '👥', label: 'Bạn bè' },
  { path: '/groups', emoji: '👥', label: 'Nhóm' },
  { path: '/memories', emoji: '🕘', label: 'Kỷ niệm' },
  { path: '/saved', emoji: '🔖', label: 'Đã lưu' },
  { path: '/reels', emoji: '🎞️', label: 'Reels' },
  { path: '/games', emoji: '🎮', label: 'Chơi game' },
]

const moreItems: NavItem[] = [
  { path: '/pages', emoji: '📣', label: 'Trang' },
  { path: '/events', emoji: '📅', label: 'Sự kiện' },
  { path: '/stories/archive', emoji: '🕘', label: 'Kho lưu trữ tin' },
  { path: '/photos', emoji: '🖼️', label: 'Ảnh' },
  { path: '/birthdays', emoji: '🎂', label: 'Sinh nhật' },
  { path: '/settings/privacy', emoji: '🔒', label: 'Quyền riêng tư' },
  { path: '/settings/security', emoji: '🛡️', label: 'Bảo mật' },
]

function SidebarLink({ item }: { item: NavItem }) {
  return <NavLink
    to={item.path}
    className={({ isActive }) => [
      'group flex w-full items-center gap-3 rounded-lg px-2 py-2 text-left no-underline transition-colors',
      isActive ? 'bg-surface-2' : 'hover:bg-surface-2',
    ].join(' ')}
  >
    <span className="flex h-9 w-9 shrink-0 items-center justify-center text-[25px] leading-none" aria-hidden="true">{item.emoji}</span>
    <span className="text-[15px] font-semibold text-text">{item.label}</span>
  </NavLink>
}

export default function Sidebar() {
  const { session } = useAuth()
  const { t } = usePreferences()
  const [isExpanded, setIsExpanded] = useState(false)
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null)
  const [shortcuts, setShortcuts] = useState<Group[]>([])
  const initials = session!.user.username.slice(0, 2).toUpperCase()

  useEffect(() => {
    let isCurrent = true
    void Promise.all([usersApi.getCurrent(), groupsApi.getMine()])
      .then(([profile, groups]) => {
        if (!isCurrent) return
        setAvatarUrl(profile.avatarUrl)
        setShortcuts(groups.items.slice(0, 5))
      })
      .catch(() => undefined)

    return () => { isCurrent = false }
  }, [session?.user.id])

  return (
    <aside className="fixed left-0 top-14 z-40 flex h-[calc(100vh-56px)] w-[360px] flex-col overflow-y-auto bg-bg px-3 max-lg:hidden">
      <nav className="flex flex-col gap-0.5 py-3" aria-label="Lối tắt">
        <Link
          to="/profile"
          className="group flex w-full items-center gap-3 rounded-lg px-2 py-2 no-underline transition-colors hover:bg-surface-2"
        >
          <span className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-[11px] font-bold text-white">
            {avatarUrl ? <img src={resolveProfileImageUrl(avatarUrl)} alt="" className="h-full w-full object-cover" /> : initials}
          </span>
          <span className="text-[15px] font-semibold text-text">{session!.user.username}</span>
        </Link>

        {primaryItems.map((item) => <SidebarLink key={item.path} item={item} />)}

        {isExpanded && moreItems.map((item) => <SidebarLink key={item.path} item={item} />)}

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
          {shortcuts.map((group) => <Link key={group.id} to={`/groups/${group.id}`} className="flex items-center gap-3 rounded-lg px-2 py-2 no-underline transition-colors hover:bg-surface-2">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-surface-2 text-base text-text">{group.coverUrl ? <img src={group.coverUrl} alt="" className="h-full w-full object-cover" /> : '👥'}</span>
            <span className="line-clamp-2 text-[15px] font-semibold leading-5 text-text">{group.name}</span>
          </Link>)}
        </div>
      </section>}

      <footer className="mt-auto px-2 py-4 text-[11px] leading-relaxed text-text-light">
        {t('appearance')} · {t('language')} · © 2026 Fookbase · Quyền riêng tư · Điều khoản
      </footer>
    </aside>
  )
}
