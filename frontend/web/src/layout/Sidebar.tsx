import { Link, NavLink } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useRealtime } from '../realtime/useRealtime'
import { usePreferences } from '../preferences'

interface NavItem {
  path: string
  emoji: string
  label: string
}

export default function Sidebar() {
  const { session } = useAuth()
  const { unreadMessageCount } = useRealtime()
  const { t } = usePreferences()
  const initials = session!.user.username.slice(0, 2).toUpperCase()
  const navItems: NavItem[] = [
    { path: '/feed', emoji: '🏠', label: t('feed') },
    { path: '/explore', emoji: '🔍', label: t('explore') },
    { path: '/messages', emoji: '💬', label: t('messages') },
    { path: '/games', emoji: '🎮', label: t('games') },
    { path: '/profile', emoji: '👤', label: t('profile') },
  ]

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

        {navItems.map((item) => (
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

                {item.path === '/messages' && unreadMessageCount > 0 && (
                  <span className="ml-auto text-[11px] font-bold px-1.5 py-0.5 rounded-full bg-[#e41e3f] text-white min-w-[20px] text-center">
                    {unreadMessageCount > 99 ? '99+' : unreadMessageCount}
                  </span>
                )}
              </>
            )}
          </NavLink>
        ))}
      </nav>

      {/* ── Footer ───────────────────────────────────────── */}
      <div className="mt-auto px-4 pb-4 pt-2">
        <p className="text-[11px] text-text-light leading-relaxed">
          {t('appearance')} · {t('language')} · © 2026 Fookbase
        </p>
      </div>
    </aside>
  )
}
