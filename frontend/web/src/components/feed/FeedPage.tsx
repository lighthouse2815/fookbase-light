import { useState } from 'react'
import { POSTS, TRENDING_TOPICS, USERS, getUserById, formatNumber } from '../../data/mockData'
import PostCard from './PostCard'
import NewPostBox from './NewPostBox'
import type { Post } from '../../data/mockData'

let nextId = 100

export default function FeedPage() {
  const [posts, setPosts] = useState<Post[]>(POSTS)

  const handleNewPost = (content: string) => {
    const newPost: Post = {
      id: `new-${nextId++}`,
      authorId: 'u1',
      content,
      timestamp: new Date(),
      likes: 0, reposts: 0, comments: 0,
      tags: [], isLiked: false, isReposted: false,
    }
    setPosts((prev) => [newPost, ...prev])
  }

  return (
    <div className="flex gap-0 min-h-screen">

      {/* ── Main Feed ─────────────────────────────────────── */}
      <main className="flex-1 min-w-0 border-r border-border">

        {/* Sticky header */}
        <div className="sticky top-0 z-10 px-4 py-3 bg-surface/90 backdrop-blur-md
                        border-b border-border flex items-center gap-3">
          <h1 className="font-heading font-bold text-[17px] text-text">Home</h1>
          <div className="ml-auto">
            <button type="button" className="w-8 h-8 rounded-full bg-surface-2 flex items-center
                                             justify-center text-text-muted hover:bg-surface-3
                                             transition-colors cursor-pointer border-none text-sm">
              ✨
            </button>
          </div>
        </div>

        {/* New post area */}
        <div className="px-4 py-4 border-b border-border bg-surface">
          <NewPostBox onPost={handleNewPost} />
        </div>

        {/* Posts */}
        <div className="flex flex-col gap-[4px] p-3">
          {posts.map((post, i) => {
            const author = getUserById(post.authorId)
            if (!author) return null
            return (
              <PostCard
                key={post.id}
                post={post}
                author={author}
                style={{ animation: `fade-in 0.3s ease ${i * 0.04}s both` }}
              />
            )
          })}
        </div>
      </main>

      {/* ── Right Sidebar — Trending ──────────────────────── */}
      <aside className="w-[300px] xl:w-[340px] 2xl:w-[380px] shrink-0 hidden lg:flex flex-col
                        border-r border-border">
        <div className="sticky top-0 p-4 flex flex-col gap-5 h-screen scroll-smooth overflow-y-auto">

          {/* Search bar */}
          <div className="relative mt-1">
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-text-light text-sm">🔍</span>
            <input type="text" placeholder="Search..."
              className="w-full bg-surface-2 border border-border rounded-full
                         text-[13px] text-text pl-9 pr-4 py-2 outline-none
                         focus:input-focus transition-all placeholder:text-text-light" />
          </div>

          {/* Trending section */}
          <section>
            <h2 className="font-heading font-bold text-[15px] text-text mb-3">Trending now</h2>
            <div className="bg-surface rounded-2xl card-shadow border border-border overflow-hidden">
              {TRENDING_TOPICS.slice(0, 8).map((topic, i) => (
                <div key={topic.id}
                  className="flex items-center justify-between px-4 py-3 cursor-pointer
                             border-b border-border last:border-0 transition-colors
                             hover:bg-surface-hover"
                  style={{ animation: `fade-in 0.3s ease ${i * 0.04}s both` }}>
                  <div>
                    <div className="text-[13px] font-semibold text-text">{topic.tag}</div>
                    <div className="text-[11px] text-text-muted">{formatNumber(topic.posts)} posts</div>
                  </div>
                  <span className="text-lg">
                    {topic.trend === 'hot' ? '🔥' : topic.trend === 'up' ? '📈' : '📉'}
                  </span>
                </div>
              ))}
            </div>
          </section>

          {/* Footer */}
          <p className="text-[11px] text-text-light leading-relaxed">
            Terms · Privacy · Cookies · Ads info · More · © 2026 SocialApp
          </p>
        </div>
      </aside>

      {/* ── 3rd Panel — 2K: Active users ─────────────────── */}
      <aside className="w-[300px] 2xl:w-[340px] shrink-0 hidden 2xl:flex flex-col">
        <div className="sticky top-0 p-4 flex flex-col gap-5 h-screen scroll-smooth overflow-y-auto">
          <h2 className="font-heading font-bold text-[15px] text-text mt-1">People you may know</h2>
          <div className="flex flex-col gap-3">
            {USERS.filter(u => u.id !== 'u1').map((user, i) => (
              <div key={user.id}
                className="bg-surface rounded-2xl border border-border card-shadow p-3
                           flex items-center gap-3 transition-all duration-200 hover:card-shadow-hover"
                style={{ animation: `slide-in-left 0.3s ease ${i * 0.06}s both` }}>
                <div className={`w-10 h-10 rounded-full flex items-center justify-center text-[11px]
                                 font-bold text-white shrink-0 ${user.isOnline ? 'avatar-online' : ''}
                                 ${user.avatarColor}`}>
                  {user.avatar}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-[13px] font-semibold text-text truncate">{user.displayName}</div>
                  <div className="text-[11px] text-text-muted truncate">@{user.handle}</div>
                </div>
                <button type="button"
                  className="px-3 py-1 rounded-full text-[12px] font-semibold text-primary
                             border border-[rgba(108,99,255,0.3)] bg-surface-hover
                             hover:gradient-primary hover:text-white hover:border-transparent
                             transition-all duration-200 cursor-pointer shrink-0">
                  Follow
                </button>
              </div>
            ))}
          </div>
        </div>
      </aside>

    </div>
  )
}
