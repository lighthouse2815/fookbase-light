import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { feedApi } from '../../api/feed'
import type { FeedItem } from '../../api/feed'
import { mediaApi } from '../../api/media'
import { postsApi } from '../../api/posts'
import type { Post } from '../../api/posts'
import type { UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import { usePreferences } from '../../preferences'
import PaginationControls from '../../shared/components/PaginationControls'
import LivePostCard from './components/LivePostCard'
import NewPostBox from './components/NewPostBox'
import StoryTray from './components/StoryTray'

export default function FeedPage() {
  const { session } = useAuth()
  const { t } = usePreferences()
  const [posts, setPosts] = useState<FeedItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loadMoreError, setLoadMoreError] = useState<string | null>(null)

  const loadFeed = useCallback(async (cursor?: string, append = false) => {
    if (append) {
      setIsLoadingMore(true)
      setLoadMoreError(null)
    } else {
      setIsLoading(true)
      setError(null)
    }

    try {
      const page = await feedApi.getHome(cursor)
      setPosts((currentPosts) => append
        ? [...currentPosts, ...page.items.filter((post) => !currentPosts.some((item) => item.id === post.id))]
        : page.items)
      setNextCursor(page.nextCursor)
    } catch (requestError) {
      const message = requestError instanceof ApiError ? requestError.message : 'Không thể tải bảng tin.'
      if (append) setLoadMoreError(message)
      else setError(message)
    } finally {
      if (append) setIsLoadingMore(false)
      else setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void loadFeed()
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [loadFeed])

  const handleNewPost = async (
    content: string,
    files: readonly File[],
    onUploadProgress: (progress: number) => void,
  ) => {
    const mediaIds = await mediaApi.uploadFiles(files, onUploadProgress)
    await postsApi.create({ content, privacy: 'public', mediaIds })
    await loadFeed()
  }

  const toProfile = (post: FeedItem): UserProfile => ({
    userId: post.author.userId,
    username: post.author.username,
    displayName: post.author.displayName,
    avatarUrl: post.author.avatarUrl,
    bio: null,
    coverUrl: null,
    dateOfBirth: null,
    currentCity: null,
    createdAt: post.createdAtUtc,
    updatedAt: post.updatedAtUtc ?? post.createdAtUtc,
  })

  const mergeUpdatedPost = (updatedPost: Post) => {
    setPosts((currentPosts) => currentPosts.map((post) => post.id === updatedPost.id
      ? {
          ...post,
          ...updatedPost,
          author: post.author,
          media: post.media,
          reactionCount: Object.values(updatedPost.reactionCounts).reduce(
            (total, count) => total + count,
            0,
          ),
        }
      : post))
  }

  return (
    <div className="flex justify-center min-h-screen px-2 sm:px-4 py-4">
      <div className="w-full max-w-[680px] min-w-0 flex flex-col gap-4">
        <StoryTray />
        <NewPostBox onPost={handleNewPost} />
        <div className="flex flex-col gap-4">
          {error && <div className="rounded-lg bg-[#e41e3f]/10 border border-[#e41e3f]/40 p-3 text-sm text-[#ff8a9b]"><p>{error}</p><button type="button" onClick={() => void loadFeed()} className="mt-2 rounded-md border border-[#ff8a9b]/50 bg-transparent px-3 py-1 text-xs font-semibold text-[#ff8a9b] cursor-pointer">{t('refresh')}</button></div>}
          {isLoading && <div className="flex flex-col gap-4" aria-label={t('loadingFeed')}><div className="h-52 rounded-xl bg-surface-2 animate-pulse" /><div className="h-52 rounded-xl bg-surface-2 animate-pulse" /></div>}
          {!isLoading && posts.length === 0 && !error && <p className="text-sm text-text-muted">{t('noPostsYet')}</p>}
          {posts.map((post) => (
            <LivePostCard
              key={post.id}
              post={post}
              author={toProfile(post)}
              currentUserId={session!.user.id}
              onPostUpdated={mergeUpdatedPost}
              onPostDeleted={(postId) => setPosts((currentPosts) => currentPosts.filter((item) => item.id !== postId))}
            />
          ))}
          <PaginationControls hasMore={nextCursor !== null} isLoading={isLoadingMore} error={loadMoreError} label={t('loadMorePosts')} onLoadMore={() => void loadFeed(nextCursor ?? undefined, true)} />
        </div>
      </div>
    </div>
  )
}
