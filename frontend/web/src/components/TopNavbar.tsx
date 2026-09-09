import { CURRENT_USER } from '../data/mockData'
import type { PageId } from './Sidebar'

interface TopNavbarProps {
  activePage: PageId
  onNavigate: (page: PageId) => void
}

const NAV_ITEMS: { id: PageId; icon: string; label: string }[] = [
  { id: 'feed', icon: '🏠', label: 'Home' },
  { id: 'explore', icon: '🔍', label: 'Explore' },
  { id: 'messages', icon: '💬', label: 'Messages' },
  { id: 'profile', icon: '👤', label: 'Profile' },
]

export default function TopNavbar({ activePage, onNavigate }: TopNavbarProps) {
  return (
    <header className="fixed top-0 left-0 right-0 h-14 bg-surface border-b border-border
                        flex items-center px-4 z-50">

      {/* ── Left: Logo + Search ──────────────────────── */}
      <div className="flex items-center gap-2 w-[280px] shrink-0">
        {/* Facebook-style logo */}
        <button type="button" onClick={() => onNavigate('feed')}
          className="w-10 h-10 rounded-full bg-primary flex items-center justify-center
                     shrink-0 cursor-pointer border-none hover:brightness-110 transition">
          <span className="text-white text-xl font-bold">f</span>
        </button>

        {/* Search */}
        <div className="relative flex-1 max-sm:hidden">
          <span className="absolute left-3 top-1/2 -translate-y-1/2 text-text-light text-sm">🔍</span>
          <input type="text" placeholder="Search Fookbase"
            className="w-full bg-surface-2 border-none rounded-full
                       text-[13px] text-text pl-9 pr-4 py-2 outline-none
                       focus:input-focus transition-all placeholder:text-text-light" />
        </div>
      </div>

      {/* ── Center: Navigation Tabs ──────────────────── */}
      <nav className="flex-1 flex items-center justify-center gap-1 max-w-[600px] mx-auto">
        {NAV_ITEMS.map((item) => {
          const isActive = activePage === item.id
          return (
            <button
              key={item.id}
              type="button"
              onClick={() => onNavigate(item.id)}
              className={[
                'flex-1 flex items-center justify-center py-2 rounded-lg',
                'transition-all duration-200 cursor-pointer border-0 relative',
                'max-w-[120px] text-2xl',
                isActive
                  ? 'text-primary'
                  : 'text-text-muted hover:bg-surface-2',
              ].join(' ')}
              title={item.label}
            >
              {item.icon}
              {/* Active indicator line */}
              {isActive && (
                <div className="absolute bottom-0 left-2 right-2 h-[3px] bg-primary rounded-t-full" />
              )}
            </button>
          )
        })}
      </nav>

      {/* ── Right: Actions ───────────────────────────── */}
      <div className="flex items-center gap-2 w-[280px] shrink-0 justify-end">
        {/* Grid menu */}
        <button type="button"
          className="w-10 h-10 rounded-full bg-surface-2 flex items-center justify-center
                     text-text hover:bg-[#4e4f50] transition-colors cursor-pointer border-none text-sm">
          ⊞
        </button>
        {/* Notifications */}
        <button type="button"
          className="w-10 h-10 rounded-full bg-surface-2 flex items-center justify-center
                     text-text hover:bg-[#4e4f50] transition-colors cursor-pointer border-none text-sm relative">
          🔔
          <span className="absolute -top-0.5 -right-0.5 w-5 h-5 rounded-full bg-[#e41e3f]
                           text-[10px] font-bold text-white flex items-center justify-center">
            3
          </span>
        </button>
        {/* User avatar */}
        <button type="button"
          className={`w-10 h-10 rounded-full flex items-center justify-center
                       text-[11px] font-bold text-white shrink-0 cursor-pointer border-none
                       hover:brightness-110 transition ${CURRENT_USER.avatarColor}`}>
          {CURRENT_USER.avatar}
        </button>
      </div>
    </header>
  )
}

