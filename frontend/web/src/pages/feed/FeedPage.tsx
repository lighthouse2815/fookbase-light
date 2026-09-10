import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { mediaApi } from '../../api/media'
import { postsApi } from '../../api/posts'
import type { Post } from '../../api/posts'
import { usersApi } from '../../api/users'
import type { UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import LivePostCard from './components/LivePostCard'
import NewPostBox from './components/NewPostBox'

export default function FeedPage() {
  const { session } = useAuth()
  const [posts, setPosts] = useState<Post[]>([])
  const [authors, setAuthors] = useState<Record<string, UserProfile>>({})
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [totalPosts, setTotalPosts] = useState(0)
  const [suggestedUsers, setSuggestedUsers] = useState<UserProfile[]>([])
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

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void usersApi.search('', 0, 5)
        .then((page) => setSuggestedUsers(page.items.filter((user) => user.userId !== session!.user.id)))
        .catch(() => setSuggestedUsers([]))
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [session])

  const handleNewPost = async (
    content: string,
    files: readonly File[],
    onUploadProgress: (progress: number) => void,
  ) => {
    const mediaIds = await mediaApi.uploadFiles(files, onUploadProgress)
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

      <aside className="w-[300px] xl:w-[340px] shrink-0 hidden lg:flex flex-col">
        <div className="sticky top-14 p-2 flex flex-col gap-5 h-[calc(100vh-56px)] scroll-smooth overflow-y-auto">
          <section>
            <h2 className="font-heading font-bold text-[15px] text-text mb-3">People you may know</h2>
            <div className="flex flex-col gap-3">
              {suggestedUsers.map((user) => (
                <div
                  key={user.userId}
                  className="bg-surface rounded-xl border border-border p-3 flex items-center gap-3"
                >
                  <div className="w-10 h-10 rounded-full overflow-hidden flex items-center justify-center text-[11px] font-bold text-white bg-primary shrink-0">
                    {user.avatarUrl ? <img src={user.avatarUrl} alt="" className="w-full h-full object-cover" /> : user.displayName.slice(0, 2).toUpperCase()}
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="text-[13px] font-semibold text-text truncate">{user.displayName}</p>
                    <p className="text-[11px] text-text-muted truncate">@{user.username}</p>
                  </div>
                  <Link to={`/profile/${user.userId}`} className="px-3 py-1 rounded-full text-[12px] font-semibold text-primary border border-primary/30 bg-surface-2 hover:bg-primary hover:text-white hover:border-transparent transition-all no-underline">View</Link>
                </div>
              ))}
              {suggestedUsers.length === 0 && <p className="text-sm text-text-muted">No suggestions yet.</p>}
            </div>
          </section>

          {/* Footer */}
          <p className="text-[11px] text-text-light leading-relaxed">
            Terms · Privacy · Cookies · Ads info · More · © 2026 Fookbase
          </p>
        </div>
      </aside>
    </div>
  )
}
