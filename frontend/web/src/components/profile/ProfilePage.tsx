import { useState } from 'react'
import { CURRENT_USER, POSTS, getUserById, formatNumber } from '../../data/mockData'
import PostCard from '../feed/PostCard'

const myPosts = POSTS.filter((p) => p.authorId === 'u1')

const badgeClass: Record<string, string> = {
  root: 'badge-root', anon: 'badge-anon',
  cyborg: 'badge-cyborg', neural: 'badge-neural', ghost: 'badge-ghost',
}

export default function ProfilePage() {
  const [tab, setTab] = useState<'posts' | 'reposts' | 'media'>('posts')
  const user = CURRENT_USER

  return (
    <div className="min-h-screen" style={{ animation: 'fade-in 0.25s ease both' }}>

      {/* ── Hero Banner ──────────────────────────────────── */}
      <div className="h-48 gradient-primary relative">
        {/* Decorative circles */}
        <div className="absolute right-12 top-8 w-24 h-24 rounded-full bg-white/10" />
        <div className="absolute right-32 bottom-4 w-12 h-12 rounded-full bg-white/10" />
        <div className="absolute left-1/3 top-6 w-8 h-8 rounded-full bg-white/10" />
      </div>

      {/* ── Body ─────────────────────────────────────────── */}
      <div className="flex flex-col xl:flex-row xl:items-start">

        {/* ── LEFT — Profile info ───────────────────────── */}
        <div className="xl:w-[340px] 2xl:w-[400px] shrink-0 xl:sticky xl:top-0 xl:h-screen
                        xl:overflow-y-auto scroll-smooth xl:border-r xl:border-border
                        px-5 pb-6 flex flex-col gap-4 bg-surface">

          {/* Avatar + edit button */}
          <div className="flex items-end justify-between -mt-12 pt-0 mb-1">
            <div className={`w-20 h-20 rounded-full flex items-center justify-center text-2xl
                             font-bold text-white border-4 border-surface shadow-lg
                             ${user.avatarColor}`}>
              {user.avatar}
            </div>
            <button type="button"
              className="px-4 py-1.5 mt-16 rounded-full text-[13px] font-semibold
                         border-2 border-primary text-primary bg-white
                         hover:bg-primary hover:text-white transition-all duration-200 cursor-pointer">
              Edit profile
            </button>
          </div>

          {/* Name */}
          <div>
            <h1 className="font-heading font-bold text-[22px] text-text">{user.displayName}</h1>
            <p className="text-[14px] text-text-muted">@{user.handle}</p>
          </div>

          {/* Badges */}
          <div className="flex flex-wrap gap-1.5">
            {user.badges.map((b) => (
              <span key={b} className={`badge-pill ${badgeClass[b] ?? ''}`}>{b}</span>
            ))}
          </div>

          {/* Bio */}
          <p className="text-[14px] text-text leading-relaxed whitespace-pre-wrap">{user.bio}</p>

          {/* Meta */}
          <div className="flex flex-wrap gap-3 text-[13px] text-text-muted">
            <span>📍 {user.location}</span>
            <span>📅 Joined {user.joinDate}</span>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-3 gap-2">
            {[
              { label: 'Followers', value: user.followers },
              { label: 'Following', value: user.following },
              { label: 'Posts',     value: user.posts },
            ].map(({ label, value }) => (
              <div key={label}
                className="bg-surface-2 rounded-xl p-3 text-center cursor-pointer
                           hover:bg-surface-hover transition-colors duration-200">
                <div className="font-heading font-bold text-[18px] gradient-text">
                  {formatNumber(value)}
                </div>
                <div className="text-[11px] text-text-muted font-medium">{label}</div>
              </div>
            ))}
          </div>
        </div>

        {/* ── RIGHT — Posts ─────────────────────────────── */}
        <div className="flex-1 min-w-0 bg-bg">

          {/* Tabs */}
          <div className="flex border-b border-border bg-surface sticky top-0 z-10">
            {(['posts', 'reposts', 'media'] as const).map((t) => (
              <button key={t} type="button" onClick={() => setTab(t)}
                className={[
                  'flex-1 py-3.5 text-[14px] font-semibold transition-all duration-200 cursor-pointer border-0 bg-transparent capitalize',
                  tab === t
                    ? 'border-b-2 border-primary text-primary'
                    : 'text-text-muted hover:text-text border-b-2 border-transparent',
                ].join(' ')}>
                {t}
              </button>
            ))}
          </div>

          {/* Post list */}
          <div className="flex flex-col gap-[4px] p-3">
            {tab === 'posts' && myPosts.map((post, i) => {
              const author = getUserById(post.authorId)
              if (!author) return null
              return (
                <PostCard key={post.id} post={post} author={author}
                  style={{ animation: `fade-in 0.3s ease ${i * 0.05}s both` }} />
              )
            })}
            {tab !== 'posts' && (
              <div className="flex flex-col items-center justify-center py-24 gap-3">
                <span className="text-5xl">📭</span>
                <p className="text-[14px] text-text-muted font-medium">Nothing here yet</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
