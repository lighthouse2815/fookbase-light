import { CURRENT_USER } from '../data/mockData'

export type PageId = 'feed' | 'explore' | 'messages' | 'profile'

interface NavItem {
  id: PageId
  emoji: string
  label: string
  badge?: number
}

const NAV_ITEMS: NavItem[] = [
  { id: 'feed',     emoji: '🏠', label: 'Feed' },
  { id: 'explore',  emoji: '🔍', label: 'Explore' },
  { id: 'messages', emoji: '💬', label: 'Messages', badge: 2 },
  { id: 'profile',  emoji: '👤', label: 'Profile' },
]

interface SidebarProps {
  activePage: PageId
  onNavigate: (page: PageId) => void
}

export default function Sidebar({ activePage, onNavigate }: SidebarProps) {
  return (
    <aside className="fixed top-0 left-0 w-[240px] h-screen flex flex-col
                      bg-surface border-r border-border z-50
                      max-sm:w-16">

      {/* ── Logo ─────────────────────────────────────────── */}
      <div className="px-5 py-5 flex items-center gap-2.5 border-b border-border">
        <div className="w-8 h-8 rounded-xl gradient-primary flex items-center justify-center shrink-0">
          <span className="text-white text-sm font-bold">S</span>
        </div>
        <span className="font-heading font-bold text-[17px] gradient-text max-sm:hidden">
          SocialApp
        </span>
      </div>

      {/* ── Navigation ───────────────────────────────────── */}
      <nav className="flex flex-col gap-1 px-3 py-4 flex-1">
        {NAV_ITEMS.map((item) => {
          const isActive = activePage === item.id
          return (
            <button
              key={item.id}
              type="button"
              onClick={() => onNavigate(item.id)}
              className={[
                'group flex items-center gap-3 px-3 py-2.5 rounded-xl w-full text-left',
                'transition-all duration-200 cursor-pointer border-0 relative',
                'max-sm:justify-center max-sm:px-0',
                isActive
                  ? 'sidebar-item-active'
                  : 'bg-transparent hover:bg-surface-2',
              ].join(' ')}
            >
              <span className="text-xl shrink-0">{item.emoji}</span>

              <span className={[
                'font-semibold text-[14px] transition-colors max-sm:hidden',
                isActive ? 'text-primary' : 'text-text-muted group-hover:text-text',
              ].join(' ')}>
                {item.label}
              </span>

              {item.badge && (
                <span className="ml-auto text-[11px] font-bold px-1.5 py-0.5 rounded-full
                                 gradient-primary text-white min-w-[20px] text-center max-sm:hidden">
                  {item.badge}
                </span>
              )}
            </button>
          )
        })}
      </nav>

      {/* ── Divider ──────────────────────────────────────── */}
      <div className="h-px bg-border mx-4 mb-3" />

      {/* ── Current User ─────────────────────────────────── */}
      <div className="px-4 pb-5">
        <div className="flex items-center gap-3 p-3 rounded-xl hover:bg-surface-2
                        cursor-pointer transition-colors duration-200 max-sm:justify-center">
          <div className={`avatar-online w-9 h-9 rounded-full flex items-center justify-center
                           text-[12px] font-bold text-white shrink-0 ${CURRENT_USER.avatarColor}`}>
            {CURRENT_USER.avatar}
          </div>
          <div className="min-w-0 max-sm:hidden">
            <div className="text-[13px] font-semibold text-text truncate">
              {CURRENT_USER.displayName}
            </div>
            <div className="text-[11px] text-text-muted truncate">
              @{CURRENT_USER.handle}
            </div>
          </div>
          <span className="ml-auto text-text-light text-lg max-sm:hidden">···</span>
        </div>
      </div>
    </aside>
  )
}
