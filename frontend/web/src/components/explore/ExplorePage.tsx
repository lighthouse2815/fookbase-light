import { useState } from 'react'
import { TRENDING_TOPICS, USERS, formatNumber } from '../../data/mockData'

const badgeColors: Record<string, string> = {
  root:   'text-cyber-danger  border-[rgba(255,0,64,0.4)]  bg-[rgba(255,0,64,0.08)]',
  anon:   'text-cyber-green   border-[rgba(0,255,65,0.4)]  bg-[rgba(0,255,65,0.08)]',
  cyborg: 'text-cyber-purple  border-[rgba(189,0,255,0.4)] bg-[rgba(189,0,255,0.08)]',
  neural: 'text-cyber-cyan    border-[rgba(0,255,255,0.4)] bg-[rgba(0,255,255,0.08)]',
  ghost:  'text-cyber-yellow  border-[rgba(255,215,0,0.4)] bg-[rgba(255,215,0,0.08)]',
}

export default function ExplorePage() {
  const [query, setQuery] = useState('')
  const [following, setFollowing] = useState<Set<string>>(new Set())

  const toggleFollow = (id: string) => {
    setFollowing((prev) => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })
  }

  const suggestedUsers = USERS.filter((u) => u.id !== 'u1')

  return (
    <div className="max-w-[760px] mx-auto p-4 flex flex-col gap-6" style={{ animation: 'fade-in 0.3s ease both' }}>

      {/* ── Header ─────────────────────────────────────────── */}
      <div className="flex items-center gap-2 py-2">
        <span className="font-mono text-[10px] text-cyber-cyan tracking-widest">◉</span>
        <h1 className="font-mono text-[13px] text-text-bright tracking-widest">EXPLORE // SCAN_NETWORK</h1>
      </div>

      {/* ── Search bar ─────────────────────────────────────── */}
      <div className="relative">
        <span className="absolute left-3 top-1/2 -translate-y-1/2 font-mono text-[13px] text-cyber-cyan">
          &gt;_
        </span>
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="search handles, tags, exploits..."
          className="w-full bg-bg-input border border-[rgba(0,255,255,0.2)] text-text-bright
                     font-mono text-[13px] pl-9 pr-4 py-2.5 rounded-[2px] outline-none
                     placeholder:text-text-dim transition-all duration-200
                     focus:border-cyber-cyan focus:shadow-[0_0_10px_rgba(0,255,255,0.15)]"
        />
        {query && (
          <span className="absolute right-3 top-1/2 -translate-y-1/2 font-mono text-[10px] text-text-dim">
            [esc]
          </span>
        )}
      </div>

      <div className="grid lg:grid-cols-[1fr_300px] gap-5">

        {/* ── Trending Topics ─────────────────────────────── */}
        <section>
          <div className="flex items-center gap-2 mb-3">
            <h2 className="font-mono text-[11px] text-text-bright tracking-widest">TRENDING_VECTORS</h2>
            <div className="flex-1 h-px bg-[rgba(0,255,255,0.1)]" />
          </div>

          <div className="border border-[rgba(0,255,255,0.12)] rounded-[4px] bg-bg-card overflow-hidden">
            {TRENDING_TOPICS.map((topic, i) => (
              <div
                key={topic.id}
                className="group flex items-center gap-3 px-4 py-3 cursor-pointer
                           border-b border-[rgba(0,255,255,0.07)] last:border-0
                           transition-all duration-200 hover:bg-[rgba(0,255,255,0.04)]"
                style={{ animation: `fade-in 0.3s ease ${i * 0.04}s both` }}
              >
                {/* Rank */}
                <span className="font-mono text-[11px] text-text-dim w-5 text-right shrink-0">
                  {i + 1}
                </span>

                {/* Trend icon */}
                <span className={`text-[13px] shrink-0 ${
                  topic.trend === 'hot' ? 'text-cyber-danger' :
                  topic.trend === 'up'  ? 'text-cyber-green'  : 'text-text-dim'
                }`}>
                  {topic.trend === 'hot' ? '🔥' : topic.trend === 'up' ? '▲' : '▼'}
                </span>

                {/* Tag + count */}
                <div className="flex-1">
                  <div className="font-mono text-[13px] text-cyber-cyan tracking-wide
                                  group-hover:[text-shadow:0_0_8px_rgba(0,255,255,0.4)] transition-all">
                    {topic.tag}
                  </div>
                  <div className="font-mono text-[9px] text-text-dim">
                    {formatNumber(topic.posts)} signals intercepted
                  </div>
                </div>

                {/* Infiltrate button */}
                <button
                  type="button"
                  className="opacity-0 group-hover:opacity-100 px-2.5 py-1 font-mono text-[9px]
                             tracking-widest uppercase border rounded-[2px] transition-all duration-200
                             border-[rgba(0,255,255,0.3)] text-cyber-cyan bg-[rgba(0,255,255,0.05)]
                             hover:border-cyber-cyan hover:bg-[rgba(0,255,255,0.1)] cursor-pointer"
                >
                  INFILTRATE
                </button>
              </div>
            ))}
          </div>
        </section>

        {/* ── User Suggestions ────────────────────────────── */}
        <section>
          <div className="flex items-center gap-2 mb-3">
            <h2 className="font-mono text-[11px] text-text-bright tracking-widest">KNOWN_AGENTS</h2>
            <div className="flex-1 h-px bg-[rgba(0,255,255,0.1)]" />
          </div>

          <div className="flex flex-col gap-2">
            {suggestedUsers.map((user, i) => {
              const isFollowing = following.has(user.id)
              return (
                <div
                  key={user.id}
                  className="border border-[rgba(0,255,255,0.12)] bg-bg-card rounded-[4px] p-3
                             flex items-start gap-3 transition-all duration-200
                             hover:border-[rgba(0,255,255,0.25)] hover:shadow-[0_0_10px_rgba(0,255,255,0.05)]"
                  style={{ animation: `slide-in-left 0.3s ease ${i * 0.06}s both` }}
                >
                  {/* Avatar */}
                  <div
                    className={`w-9 h-9 rounded-[2px] flex items-center justify-center font-mono
                                text-[10px] text-cyber-cyan border border-[rgba(0,255,255,0.3)] shrink-0
                                ${user.isOnline ? 'avatar-online' : ''}`}
                    style={{ background: user.avatarColor }}
                  >
                    {user.avatar}
                  </div>

                  {/* Info */}
                  <div className="flex-1 min-w-0">
                    <div className="flex flex-wrap gap-1 mb-0.5">
                      <span className="font-mono text-[11px] text-text-bright">{user.displayName}</span>
                      {user.badges.slice(0, 1).map((b) => (
                        <span
                          key={b}
                          className={`font-mono text-[8px] tracking-widest uppercase px-1 border rounded-[2px] ${badgeColors[b] ?? ''}`}
                        >
                          {b}
                        </span>
                      ))}
                    </div>
                    <p className="font-mono text-[9px] text-text-dim mb-1">@{user.handle}</p>
                    <p className="font-mono text-[9px] text-text-dim">
                      {formatNumber(user.followers)} followers
                    </p>
                  </div>

                  {/* Follow button */}
                  <button
                    type="button"
                    onClick={() => toggleFollow(user.id)}
                    className={[
                      'px-2.5 py-1 font-mono text-[9px] tracking-widest uppercase rounded-[2px]',
                      'border transition-all duration-200 cursor-pointer shrink-0',
                      isFollowing
                        ? 'border-[rgba(0,255,255,0.2)] text-text-dim bg-transparent hover:border-cyber-danger hover:text-cyber-danger'
                        : 'border-[rgba(0,255,255,0.3)] text-cyber-cyan bg-[rgba(0,255,255,0.05)] hover:bg-[rgba(0,255,255,0.1)] hover:border-cyber-cyan hover:shadow-[0_0_8px_rgba(0,255,255,0.15)]',
                    ].join(' ')}
                  >
                    {isFollowing ? 'UNLINK' : 'CONNECT'}
                  </button>
                </div>
              )
            })}
          </div>
        </section>
      </div>
    </div>
  )
}
