import GlitchText from './effects/GlitchText'
import { CURRENT_USER } from '../data/mockData'

export type PageId = 'feed' | 'explore' | 'messages' | 'profile'

interface NavItem {
  id: PageId
  icon: string
  label: string
  badge?: number
}

const NAV_ITEMS: NavItem[] = [
  { id: 'feed',     icon: '◈', label: 'FEED' },
  { id: 'explore',  icon: '◉', label: 'EXPLORE' },
  { id: 'messages', icon: '◎', label: 'MESSAGES', badge: 2 },
  { id: 'profile',  icon: '◈', label: 'PROFILE' },
]

interface SidebarProps {
  activePage: PageId
  onNavigate: (page: PageId) => void
}

export default function Sidebar({ activePage, onNavigate }: SidebarProps) {
  return (
    <aside className="fixed top-0 left-0 w-[240px] h-screen flex flex-col py-5 z-[100]
                      bg-[rgba(10,14,20,0.97)] border-r border-[rgba(0,255,255,0.15)]
                      backdrop-blur-xl shadow-[4px_0_24px_rgba(0,0,0,0.5)]
                      max-sm:w-[60px]">

      {/* ── Logo ─────────────────────────────────────── */}
      <div className="flex items-center gap-2.5 px-5 pb-5 border-b border-[rgba(0,255,255,0.15)] max-sm:justify-center max-sm:px-0">
        <span className="text-2xl text-cyber-cyan [text-shadow:0_0_12px_rgba(0,255,255,0.5),0_0_24px_rgba(0,255,255,0.3)]"
              style={{ animation: 'flicker 6s infinite' }}>
          ⬡
        </span>
        <GlitchText
          text="HackerNet"
          tag="span"
          className="text-sm tracking-widest neon-cyan max-sm:hidden"
        />
      </div>

      {/* ── Secure status ────────────────────────────── */}
      <div className="flex items-center gap-2 px-5 pt-2.5 max-sm:justify-center max-sm:px-0">
        <span className="w-1.5 h-1.5 rounded-full bg-cyber-green shrink-0
                         shadow-[0_0_8px_rgba(0,255,65,0.6)]"
              style={{ animation: 'pulse-glow 2s infinite' }} />
        <span className="font-mono text-[9px] tracking-widest neon-green max-sm:hidden">
          SECURE_CONN ✓
        </span>
      </div>

      {/* ── Navigation ───────────────────────────────── */}
      <nav className="flex flex-col gap-0.5 px-3 py-2 flex-1">
        {NAV_ITEMS.map((item) => {
          const isActive = activePage === item.id
          return (
            <button
              key={item.id}
              type="button"
              onClick={() => onNavigate(item.id)}
              className={[
                'group flex items-center gap-3 px-3 py-2.5 rounded-[3px] border w-full text-left',
                'transition-all duration-200 cursor-pointer relative',
                'max-sm:justify-center max-sm:px-0',
                isActive
                  ? 'bg-[rgba(0,255,255,0.06)] border-[rgba(0,255,255,0.3)] shadow-[0_0_12px_rgba(0,255,255,0.1),inset_0_0_12px_rgba(0,255,255,0.03)] sidebar-active-indicator'
                  : 'bg-transparent border-transparent hover:bg-[rgba(0,255,255,0.04)] hover:border-[rgba(0,255,255,0.15)]',
              ].join(' ')}
            >
              <span className={[
                'text-base w-5 text-center shrink-0 transition-all duration-200',
                isActive
                  ? 'text-cyber-cyan [text-shadow:0_0_8px_rgba(0,255,255,0.5)]'
                  : 'text-text-dim group-hover:text-cyber-cyan',
              ].join(' ')}>
                {item.icon}
              </span>

              <span className={[
                'font-mono text-[11px] tracking-widest transition-colors duration-200 max-sm:hidden',
                isActive ? 'text-cyber-cyan' : 'text-text-mid group-hover:text-cyber-cyan',
              ].join(' ')}>
                {item.label}
              </span>

              {item.badge && (
                <span className="ml-auto font-mono text-[10px] px-1.5 py-px
                                 bg-[rgba(255,0,64,0.12)] text-cyber-danger
                                 border border-[rgba(255,0,64,0.4)] rounded-[2px]
                                 max-sm:hidden">
                  {item.badge}
                </span>
              )}
            </button>
          )
        })}
      </nav>

      {/* ── Divider ──────────────────────────────────── */}
      <div className="h-px bg-[rgba(0,255,255,0.12)] mx-3 my-1" />

      {/* ── System info ──────────────────────────────── */}
      <div className="px-5 py-2 flex flex-col gap-1 max-sm:hidden">
        {[
          { key: 'USER',  val: `@${CURRENT_USER.handle}`, cls: 'text-cyber-cyan-dim' },
          { key: 'MODE',  val: 'ANONYMOUS',                cls: 'text-cyber-green [text-shadow:0_0_6px_rgba(0,255,65,0.3)]' },
          { key: 'PROTO', val: 'TLSv1.3',                  cls: 'text-cyber-cyan-dim' },
          { key: 'NET',   val: 'TOR+VPN',                  cls: 'text-cyber-cyan-dim' },
        ].map(({ key, val, cls }) => (
          <div key={key} className="flex justify-between items-center">
            <span className="font-mono text-[9px] tracking-widest text-text-dim">{key}</span>
            <span className={`font-mono text-[9px] tracking-wide ${cls}`}>{val}</span>
          </div>
        ))}
      </div>

      {/* ── Divider ──────────────────────────────────── */}
      <div className="h-px bg-[rgba(0,255,255,0.12)] mx-3 my-1" />

      {/* ── User mini profile ────────────────────────── */}
      <div className="flex items-center gap-2.5 mx-3 px-3 py-2.5 rounded-[3px]
                      border border-[rgba(0,255,255,0.15)] bg-[rgba(0,255,255,0.03)]
                      cursor-pointer transition-all duration-200
                      hover:border-[rgba(0,255,255,0.3)] hover:shadow-[0_0_10px_rgba(0,255,255,0.08)]
                      max-sm:justify-center">
        <div
          className="avatar-online w-9 h-9 rounded-[2px] flex items-center justify-center
                     text-[11px] font-mono text-cyber-cyan border border-[rgba(0,255,255,0.3)] shrink-0"
          style={{ background: CURRENT_USER.avatarColor }}
        >
          {CURRENT_USER.avatar}
        </div>
        <div className="min-w-0 max-sm:hidden">
          <div className="font-mono text-[11px] text-text-bright truncate">
            {CURRENT_USER.displayName}
          </div>
          <div className="font-mono text-[9px] text-cyber-cyan-dim truncate">
            @{CURRENT_USER.handle}
          </div>
        </div>
      </div>
    </aside>
  )
}
