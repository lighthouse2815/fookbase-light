import { useState } from 'react'
import { CURRENT_USER, POSTS, getUserById, formatNumber } from '../../data/mockData'
import PostCard from '../feed/PostCard'

const myPosts = POSTS.filter((p) => p.authorId === 'u1')

const badgeColors: Record<string, string> = {
  root:   'text-cyber-danger  border-[rgba(255,0,64,0.4)]  bg-[rgba(255,0,64,0.08)]',
  anon:   'text-cyber-green   border-[rgba(0,255,65,0.4)]  bg-[rgba(0,255,65,0.08)]',
  cyborg: 'text-cyber-purple  border-[rgba(189,0,255,0.4)] bg-[rgba(189,0,255,0.08)]',
  neural: 'text-cyber-cyan    border-[rgba(0,255,255,0.4)] bg-[rgba(0,255,255,0.08)]',
  ghost:  'text-cyber-yellow  border-[rgba(255,215,0,0.4)] bg-[rgba(255,215,0,0.08)]',
}

export default function ProfilePage() {
  const [tab, setTab] = useState<'posts' | 'reposts' | 'media'>('posts')
  const user = CURRENT_USER

  return (
    <div className="max-w-[720px] mx-auto" style={{ animation: 'fade-in 0.3s ease both' }}>

      {/* ── Hero Banner ────────────────────────────────────── */}
      <div className="relative h-36 matrix-bg border-b border-[rgba(0,255,255,0.15)] overflow-hidden">
        {/* Animated corner brackets */}
        <div className="absolute top-3 left-3 w-6 h-6 border-t-2 border-l-2 border-cyber-cyan opacity-60" />
        <div className="absolute top-3 right-3 w-6 h-6 border-t-2 border-r-2 border-cyber-cyan opacity-60" />
        <div className="absolute bottom-3 left-3 w-6 h-6 border-b-2 border-l-2 border-cyber-cyan opacity-60" />
        <div className="absolute bottom-3 right-3 w-6 h-6 border-b-2 border-r-2 border-cyber-cyan opacity-60" />

        {/* Decorative scan line */}
        <div className="absolute inset-x-0 h-px bg-cyber-cyan opacity-20 top-1/2" />

        {/* System text */}
        <div className="absolute top-4 right-8 font-mono text-[9px] text-cyber-green opacity-40 tracking-widest">
          ROOT@HACKERNET:~$ whoami
        </div>
        <div className="absolute bottom-4 left-8 font-mono text-[9px] text-cyber-green opacity-40 tracking-widest">
          ACCESS_LEVEL: ████████
        </div>
      </div>

      {/* ── Profile Header ─────────────────────────────────── */}
      <div className="px-5 pb-5 border-b border-[rgba(0,255,255,0.12)]">
        {/* Avatar overlapping banner */}
        <div className="flex items-end justify-between -mt-6 mb-4">
          <div
            className="avatar-online w-16 h-16 rounded-[3px] flex items-center justify-center
                       font-mono text-lg text-cyber-cyan border-2 border-cyber-cyan
                       shadow-[0_0_20px_rgba(0,255,255,0.3)]"
            style={{ background: user.avatarColor }}
          >
            {user.avatar}
          </div>

          <button
            type="button"
            className="px-4 py-1.5 font-mono text-[11px] tracking-widest uppercase
                       border border-[rgba(0,255,255,0.3)] text-cyber-cyan rounded-[2px]
                       bg-[rgba(0,255,255,0.05)] transition-all duration-200
                       hover:border-cyber-cyan hover:bg-[rgba(0,255,255,0.1)]
                       hover:shadow-[0_0_14px_rgba(0,255,255,0.2)] cursor-pointer"
          >
            ✎ EDIT_PROFILE
          </button>
        </div>

        {/* Name + handle */}
        <h1 className="font-mono text-xl text-text-bright tracking-widest mb-0.5 neon-cyan">
          {user.displayName}
        </h1>
        <p className="font-mono text-[12px] text-text-dim mb-3">@{user.handle}</p>

        {/* Badges */}
        <div className="flex flex-wrap gap-1.5 mb-3">
          {user.badges.map((b) => (
            <span
              key={b}
              className={`font-mono text-[9px] tracking-widest uppercase px-2 py-0.5 border rounded-[2px] ${badgeColors[b] ?? ''}`}
            >
              {b}
            </span>
          ))}
        </div>

        {/* Bio — terminal box */}
        <div className="bg-[rgba(0,0,0,0.4)] border border-[rgba(0,255,255,0.15)] rounded-[2px]
                        px-3 py-2.5 mb-4 border-l-2 border-l-cyber-cyan">
          <pre className="font-mono text-[12px] text-text-mid leading-relaxed whitespace-pre-wrap">
            {user.bio}
          </pre>
        </div>

        {/* Meta info */}
        <div className="flex flex-wrap gap-4 mb-4 font-mono text-[11px] text-text-dim">
          <span>📡 {user.location}</span>
          <span>📅 joined {user.joinDate}</span>
        </div>

        {/* Stats */}
        <div className="grid grid-cols-3 gap-3">
          {[
            { label: 'FOLLOWERS', value: user.followers },
            { label: 'FOLLOWING', value: user.following },
            { label: 'POSTS',     value: user.posts },
          ].map(({ label, value }) => (
            <div
              key={label}
              className="bg-bg-card border border-[rgba(0,255,255,0.12)] rounded-[3px]
                         px-3 py-2.5 text-center cursor-pointer transition-all duration-200
                         hover:border-[rgba(0,255,255,0.3)] hover:shadow-[0_0_10px_rgba(0,255,255,0.06)]"
            >
              <div className="font-mono text-lg text-cyber-cyan [text-shadow:0_0_10px_rgba(0,255,255,0.3)]">
                {formatNumber(value)}
              </div>
              <div className="font-mono text-[9px] text-text-dim tracking-widest">{label}</div>
            </div>
          ))}
        </div>
      </div>

      {/* ── Tabs ───────────────────────────────────────────── */}
      <div className="flex border-b border-[rgba(0,255,255,0.12)]">
        {(['posts', 'reposts', 'media'] as const).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => setTab(t)}
            className={[
              'flex-1 py-3 font-mono text-[11px] tracking-widest uppercase transition-all duration-200 cursor-pointer border-0',
              'border-b-2 bg-transparent',
              tab === t
                ? 'border-cyber-cyan text-cyber-cyan [text-shadow:0_0_8px_rgba(0,255,255,0.3)]'
                : 'border-transparent text-text-dim hover:text-text-bright',
            ].join(' ')}
          >
            {t}
          </button>
        ))}
      </div>

      {/* ── Post list ──────────────────────────────────────── */}
      <div className="divide-y divide-[rgba(0,255,255,0.07)]">
        {tab === 'posts' && myPosts.map((post, i) => {
          const author = getUserById(post.authorId)
          if (!author) return null
          return (
            <PostCard
              key={post.id}
              post={post}
              author={author}
              style={{ animation: `fade-in 0.3s ease ${i * 0.05}s both` }}
            />
          )
        })}

        {tab !== 'posts' && (
          <div className="flex flex-col items-center justify-center py-20 gap-3">
            <span className="font-mono text-4xl text-text-dim opacity-30">◎</span>
            <p className="font-mono text-[11px] text-text-dim tracking-widest uppercase">
              // no data in this sector
            </p>
          </div>
        )}
      </div>
    </div>
  )
}
