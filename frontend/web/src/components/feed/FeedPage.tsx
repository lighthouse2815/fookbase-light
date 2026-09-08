import { useState } from 'react'
import { POSTS, TRENDING_TOPICS, getUserById, formatNumber } from '../../data/mockData'
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
      likes: 0,
      reposts: 0,
      comments: 0,
      tags: [],
      isLiked: false,
      isReposted: false,
    }
    setPosts((prev) => [newPost, ...prev])
  }

  return (
    <div className="flex gap-0 min-h-screen">

      {/* ── Main Feed ─────────────────────────────────────── */}
      <main className="flex-1 border-r border-[rgba(0,255,255,0.1)] max-w-[680px]">

        {/* Header */}
        <div className="sticky top-0 z-10 px-4 py-3 border-b border-[rgba(0,255,255,0.12)]
                        bg-[rgba(10,14,20,0.92)] backdrop-blur-md flex items-center gap-3">
          <span className="font-mono text-[10px] text-cyber-green tracking-widest">◈</span>
          <h1 className="font-mono text-[13px] text-text-bright tracking-widest">LIVE_FEED</h1>
          <div className="ml-auto flex items-center gap-1.5">
            <span className="w-1.5 h-1.5 rounded-full bg-cyber-green shadow-[0_0_6px_rgba(0,255,65,0.5)]" />
            <span className="font-mono text-[9px] text-cyber-green tracking-widest">STREAMING</span>
          </div>
        </div>

        {/* New post */}
        <div className="p-4 border-b border-[rgba(0,255,255,0.1)]">
          <NewPostBox onPost={handleNewPost} />
        </div>

        {/* Posts */}
        <div className="divide-y divide-[rgba(0,255,255,0.07)]">
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

      {/* ── Right Sidebar — Trending ───────────────────────── */}
      <aside className="w-[300px] shrink-0 hidden lg:block">
        <div className="sticky top-0 p-4 flex flex-col gap-4 h-screen scroll-cyber overflow-y-auto">

          {/* Trending header */}
          <div className="flex items-center gap-2 pt-1">
            <span className="font-mono text-[10px] text-cyber-cyan tracking-widest">◉</span>
            <h2 className="font-mono text-[12px] text-text-bright tracking-widest">TRENDING_NOW</h2>
          </div>

          {/* Trending list */}
          <div className="border border-[rgba(0,255,255,0.12)] rounded-[4px] bg-bg-card overflow-hidden">
            {TRENDING_TOPICS.slice(0, 8).map((topic, i) => (
              <div
                key={topic.id}
                className="flex items-center justify-between px-3 py-2.5 cursor-pointer
                           border-b border-[rgba(0,255,255,0.07)] last:border-0
                           transition-all duration-200 group
                           hover:bg-[rgba(0,255,255,0.04)]"
                style={{ animation: `fade-in 0.3s ease ${i * 0.05}s both` }}
              >
                <div className="flex items-center gap-2">
                  <span className="font-mono text-[11px] text-text-dim w-4 text-right">{i + 1}</span>
                  <div>
                    <div className="font-mono text-[12px] text-cyber-cyan group-hover:text-cyber-cyan
                                    transition-colors tracking-wide">
                      {topic.tag}
                    </div>
                    <div className="font-mono text-[9px] text-text-dim">
                      {formatNumber(topic.posts)} posts
                    </div>
                  </div>
                </div>
                <span className={`font-mono text-[10px] ${
                  topic.trend === 'hot'  ? 'text-cyber-danger' :
                  topic.trend === 'up'   ? 'text-cyber-green'  : 'text-text-dim'
                }`}>
                  {topic.trend === 'hot' ? '🔥' : topic.trend === 'up' ? '↑' : '↓'}
                </span>
              </div>
            ))}
          </div>

          {/* Footer */}
          <div className="font-mono text-[9px] text-text-dim leading-relaxed tracking-wide">
            <p>// HackerNet v2.0.1</p>
            <p>// Encrypted · Anonymous · Free</p>
          </div>
        </div>
      </aside>

    </div>
  )
}
