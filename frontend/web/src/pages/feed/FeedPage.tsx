import { useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { mediaApi } from '../../api/media'
import { postsApi } from '../../api/posts'
import type { Post } from '../../api/posts'
import { usersApi } from '../../api/users'
import type { UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import { TRENDING_TOPICS, USERS, formatNumber } from '../../data/mockData'
import LivePostCard from './components/LivePostCard'
import NewPostBox from './components/NewPostBox'

export default function FeedPage() {
  const { session } = useAuth()
  const [posts, setPosts] = useState<Post[]>([])
  const [authors, setAuthors] = useState<Record<string, UserProfile>>({})
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [totalPosts, setTotalPosts] = useState(0)
  const [error, setError] = useState<string | null>(null)

  const loadFeed = async (offset = 0, append = false) => {
    if (append) {
      setIsLoadingMore(true)
    } else {
      setIsLoading(true)
    }
    setError(null)

    try {
      const page = await postsApi.getFeed(offset)
      const userIds = [...new Set(page.items.map((post) => post.authorUserId))]
      const profileResults = await Promise.allSettled(userIds.map((userId) => usersApi.getById(userId)))
      const profiles: Record<string, UserProfile> = {}
      profileResults.forEach((result, index) => {
        if (result.status === 'fulfilled') profiles[userIds[index]] = result.value
      })

      setPosts((currentPosts) => append
        ? [...currentPosts, ...page.items.filter((post) => !currentPosts.some((item) => item.id === post.id))]
        : page.items)
      setAuthors((currentAuthors) => append ? { ...currentAuthors, ...profiles } : profiles)
      setTotalPosts(page.total)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải bảng tin.')
    } finally {
      if (append) {
        setIsLoadingMore(false)
      } else {
        setIsLoading(false)
      }
    }
  }

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void loadFeed()
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [])

  const handleNewPost = async (content: string, file: File | null) => {
    const mediaIds = file ? [await mediaApi.uploadFile(file)] : []
    const post = await postsApi.create({ content, privacy: 'public', mediaIds })
    setPosts((currentPosts) => [post, ...currentPosts])
    setTotalPosts((currentTotal) => currentTotal + 1)
    if (!authors[post.authorUserId]) {
      const profile = await usersApi.getById(post.authorUserId)
      setAuthors((currentAuthors) => ({ ...currentAuthors, [post.authorUserId]: profile }))
    }
  }

  return (
    <div className="flex justify-center gap-6 min-h-screen px-2 sm:px-4 py-4">
      {/* ── Center Feed Column (max-w-[680px], centered) ── */}
      <div className="w-full max-w-[680px] min-w-0 flex flex-col gap-4">
        {/* New post area */}
        <NewPostBox onPost={handleNewPost} />

        {/* Stories row */}
        <div className="bg-surface rounded-xl border border-border p-3 sm:p-4">
          <div className="flex items-center gap-4 overflow-x-auto scroll-smooth pb-1">
            {USERS.map((user) => (
              <div
                key={user.id}
                className="flex flex-col items-center gap-1.5 shrink-0 cursor-pointer group"
              >
                <div className="p-[2px] rounded-full bg-gradient-to-tr from-primary to-[#4a93e8] group-hover:scale-105 transition-transform duration-200">
                  <div className="p-[2px] bg-surface rounded-full">
                    <div
                      className={`w-12 h-12 rounded-full flex items-center justify-center text-[13px] font-bold text-white ${user.avatarColor}`}
                    >
                      {user.avatar}
                    </div>
                  </div>
                </div>
                <span className="text-[12px] font-medium text-text max-w-[68px] truncate text-center">
                  {user.displayName.split(' ')[0]}
                </span>
              </div>
            ))}
          </div>
        </div>

        {/* Posts */}
        <div className="flex flex-col gap-4">
          {error && <p className="rounded-lg bg-[#e41e3f]/10 border border-[#e41e3f]/40 p-3 text-sm text-[#ff8a9b]">{error}</p>}
          {isLoading && <p className="text-sm text-text-muted">Loading feed...</p>}
          {!isLoading && posts.length === 0 && !error && <p className="text-sm text-text-muted">No posts yet.</p>}
          {posts.map((post) => (
            <LivePostCard
              key={post.id}
              post={post}
              author={authors[post.authorUserId]}
              currentUserId={session!.user.id}
              onPostUpdated={(updatedPost) => setPosts((currentPosts) => currentPosts.map((item) => item.id === updatedPost.id ? updatedPost : item))}
              onPostDeleted={(postId) => setPosts((currentPosts) => currentPosts.filter((item) => item.id !== postId))}
            />
          ))}
          {posts.length < totalPosts && (
            <button
              type="button"
              onClick={() => void loadFeed(posts.length, true)}
              disabled={isLoadingMore}
              className="rounded-lg bg-surface-2 hover:bg-surface-hover disabled:opacity-60 border border-border py-2.5 text-sm font-semibold text-text cursor-pointer"
            >
              {isLoadingMore ? 'Loading...' : 'Load more posts'}
            </button>
          )}
        </div>
      </div>

      {/* ── Right Sidebar — Trending ──────────────────────── */}
      <aside className="w-[300px] xl:w-[340px] shrink-0 hidden lg:flex flex-col">
        <div className="sticky top-14 p-2 flex flex-col gap-5 h-[calc(100vh-56px)] scroll-smooth overflow-y-auto">
          {/* Search bar */}
          <div className="relative mt-1">
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-text-light text-sm">🔍</span>
            <input
              type="text"
              placeholder="Search..."
              className="w-full bg-surface-2 border border-border rounded-full
                         text-[13px] text-text pl-9 pr-4 py-2 outline-none
                         focus:input-focus transition-all placeholder:text-text-light"
            />
          </div>

          {/* Trending section */}
          <section>
            <h2 className="font-heading font-bold text-[15px] text-text mb-3">Trending now</h2>
            <div className="bg-surface rounded-xl border border-border overflow-hidden">
              {TRENDING_TOPICS.slice(0, 8).map((topic, i) => (
                <div
                  key={topic.id}
                  className="flex items-center justify-between px-4 py-3 cursor-pointer
                             border-b border-border last:border-0 transition-colors
                             hover:bg-surface-2"
                  style={{ animation: `fade-in 0.3s ease ${i * 0.04}s both` }}
                >
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
            Terms · Privacy · Cookies · Ads info · More · © 2026 Fookbase
          </p>
        </div>
      </aside>

      {/* ── 3rd Panel — People you may know ─────────────────── */}
      <aside className="w-[300px] 2xl:w-[320px] shrink-0 hidden 2xl:flex flex-col">
        <div className="sticky top-14 p-2 flex flex-col gap-5 h-[calc(100vh-56px)] scroll-smooth overflow-y-auto">
          <h2 className="font-heading font-bold text-[15px] text-text mt-1">People you may know</h2>
          <div className="flex flex-col gap-3">
            {USERS.filter((u) => u.id !== 'u1').map((user, i) => (
              <div
                key={user.id}
                className="bg-surface rounded-xl border border-border p-3
                           flex items-center gap-3 transition-all duration-200 hover:bg-surface-2"
                style={{ animation: `slide-in-left 0.3s ease ${i * 0.06}s both` }}
              >
                <div
                  className={`w-10 h-10 rounded-full flex items-center justify-center text-[11px]
                                 font-bold text-white shrink-0 ${user.isOnline ? 'avatar-online' : ''}
                                 ${user.avatarColor}`}
                >
                  {user.avatar}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-[13px] font-semibold text-text truncate">{user.displayName}</div>
                  <div className="text-[11px] text-text-muted truncate">@{user.handle}</div>
                </div>
                <button
                  type="button"
                  className="px-3 py-1 rounded-full text-[12px] font-semibold text-primary
                             border border-primary/30 bg-surface-2
                             hover:bg-primary hover:text-white hover:border-transparent
                             transition-all duration-200 cursor-pointer shrink-0"
                >
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
