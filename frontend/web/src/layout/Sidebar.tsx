import { Link, NavLink } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'

interface NavItem {
  path: string
  emoji: string
  label: string
  badge?: number
}

const NAV_ITEMS: NavItem[] = [
  { path: '/feed', emoji: '🏠', label: 'Feed' },
  { path: '/explore', emoji: '🔍', label: 'Explore' },
  { path: '/messages', emoji: '💬', label: 'Messages', badge: 2 },
  { path: '/profile', emoji: '👤', label: 'Profile' },
]

interface ShortcutItem {
  emoji: string
  label: string
}

const SHORTCUTS: ShortcutItem[] = [
  { emoji: '🛡️', label: 'Security Hub' },
  { emoji: '🎮', label: 'Hack Kingdom Club' },
  { emoji: '🏆', label: 'CTF Championship' },
  { emoji: '🎯', label: 'Bug Bounty Group' },
  { emoji: '💻', label: 'Dev Community' },
]

export default function Sidebar() {
  const { session } = useAuth()
  const initials = session!.user.username.slice(0, 2).toUpperCase()

  return (
    <aside className="fixed top-14 left-0 w-[280px] h-[calc(100vh-56px)] flex flex-col bg-bg z-40 max-lg:hidden overflow-y-auto scroll-smooth">
      {/* ── Navigation ───────────────────────────────────── */}
      <nav className="flex flex-col gap-0.5 px-2 py-3">
        {/* User profile link */}
        <Link
          to="/profile"
          className="group flex items-center gap-3 px-2 py-2 rounded-lg w-full text-left transition-all duration-200 cursor-pointer border-0 bg-transparent hover:bg-surface-2 no-underline"
        >
          <div
            className="w-9 h-9 rounded-full flex items-center justify-center text-[11px] font-bold text-white shrink-0 bg-primary"
          >
            {initials}
          </div>
          <span className="font-semibold text-[15px] text-text">
            {session!.user.username}
          </span>
        </Link>

        {NAV_ITEMS.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) =>
              [
                'group flex items-center gap-3 px-2 py-2 rounded-lg w-full text-left transition-all duration-200 cursor-pointer border-0 no-underline',
                isActive ? 'bg-surface-2' : 'bg-transparent hover:bg-surface-2',
              ].join(' ')
            }
          >
            {({ isActive }) => (
              <>
                <span
                  className={[
                    'w-9 h-9 rounded-full flex items-center justify-center text-xl shrink-0',
                    isActive ? 'bg-primary text-white' : 'bg-surface-2 text-text',
                  ].join(' ')}
                >
                  {item.emoji}
                </span>

                <span
                  className={[
                    'font-semibold text-[15px] transition-colors',
                    isActive ? 'text-text' : 'text-text-muted group-hover:text-text',
                  ].join(' ')}
                >
                  {item.label}
                </span>

                {item.badge && (
                  <span className="ml-auto text-[11px] font-bold px-1.5 py-0.5 rounded-full bg-[#e41e3f] text-white min-w-[20px] text-center">
                    {item.badge}
                  </span>
                )}
              </>
            )}
          </NavLink>
        ))}
      </nav>

      {/* ── Divider ──────────────────────────────────────── */}
      <div className="h-px bg-border mx-4 my-1" />

      {/* ── Shortcuts ────────────────────────────────────── */}
      <div className="px-2 py-2">
        <h3 className="text-[13px] font-semibold text-text-muted px-2 mb-1">Your shortcuts</h3>
        <div className="flex flex-col gap-0.5">
          {SHORTCUTS.map((item) => (
            <button
              key={item.label}
              type="button"
              className="flex items-center gap-3 px-2 py-2 rounded-lg w-full text-left transition-colors duration-200 cursor-pointer border-0 bg-transparent hover:bg-surface-2"
            >
              <span className="w-9 h-9 rounded-lg bg-surface-2 flex items-center justify-center text-lg shrink-0">
                {item.emoji}
              </span>
              <span className="text-[14px] text-text-muted font-medium">{item.label}</span>
            </button>
          ))}
        </div>
      </div>

      {/* ── Footer ───────────────────────────────────────── */}
      <div className="mt-auto px-4 pb-4 pt-2">
        <p className="text-[11px] text-text-light leading-relaxed">
          Privacy · Terms · Advertising · Cookies · © 2026 Fookbase
        </p>
      </div>
    </aside>
  )
}
