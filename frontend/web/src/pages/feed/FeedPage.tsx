import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { feedApi } from '../../api/feed'
import type { FeedItem, FeedMode } from '../../api/feed'
import { mediaApi } from '../../api/media'
import { postsApi } from '../../api/posts'
import type { Post } from '../../api/posts'
import { useAuth } from '../../auth/useAuth'
import { usePreferences } from '../../preferences'
import PaginationControls from '../../shared/components/PaginationControls'
import FeedReelCard from './components/FeedReelCard'
import LivePostCard from './components/LivePostCard'
import NewPostBox from './components/NewPostBox'
import StoryTray from './components/StoryTray'

export default function FeedPage() {
  const { session } = useAuth()
  const { t } = usePreferences()
  const [mode, setMode] = useState<FeedMode>('home')
  const [posts, setPosts] = useState<FeedItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loadMoreError, setLoadMoreError] = useState<string | null>(null)
  const requestRef = useRef<AbortController | null>(null)
  const snapshotRef = useRef<{ asOfUtc: string; nextCursor: string | null; mode: FeedMode } | null>(null)
  const currentModeRef = useRef(mode)
  const mountedRef = useRef(false)

  const loadFeed = useCallback(async (cursor?: string) => {
    const append = cursor !== undefined
    const snapshot = snapshotRef.current
    if (append && (requestRef.current || !snapshot || snapshot.mode !== mode || snapshot.nextCursor !== cursor)) return

    requestRef.current?.abort()
    const controller = new AbortController()
    requestRef.current = controller
    if (append) {
      setIsLoadingMore(true)
      setLoadMoreError(null)
    } else {
      snapshotRef.current = null
      setPosts([])
      setNextCursor(null)
      setIsLoading(true)
      setIsLoadingMore(false)
      setError(null)
      setLoadMoreError(null)
    }

    try {
      const request = mode === 'following' ? feedApi.getFollowing : feedApi.getHome
      const page = await request(cursor, 20, { signal: controller.signal })
      if (controller.signal.aborted || requestRef.current !== controller) return
      if (append && snapshot?.asOfUtc !== page.asOfUtc) {
        setNextCursor(null)
        throw new ApiError(t('feedChangedRefresh'), 409)
      }
      snapshotRef.current = { asOfUtc: page.asOfUtc, nextCursor: page.nextCursor, mode }
      setPosts((currentPosts) => {
        if (!append) return page.items
        const existingIds = new Set(currentPosts.map((item) => item.id))
        return [...currentPosts, ...page.items.filter((item) => !existingIds.has(item.id))]
      })
      setNextCursor(page.nextCursor)
    } catch (requestError) {
      if (controller.signal.aborted || requestRef.current !== controller) return
      const message = requestError instanceof ApiError ? requestError.message : t('unableLoadFeed')
      if (append) setLoadMoreError(message)
      else setError(message)
    } finally {
      if (requestRef.current === controller) {
        requestRef.current = null
        if (append) setIsLoadingMore(false)
        else setIsLoading(false)
      }
    }
  }, [mode, t])

  useEffect(() => {
    mountedRef.current = true
    const timeoutId = window.setTimeout(() => {
      void loadFeed()
    }, 0)

    return () => {
      mountedRef.current = false
      window.clearTimeout(timeoutId)
      requestRef.current?.abort()
      requestRef.current = null
    }
  }, [loadFeed])

  const chooseMode = (nextMode: FeedMode) => {
    if (nextMode === mode) return
    requestRef.current?.abort()
    requestRef.current = null
    snapshotRef.current = null
    currentModeRef.current = nextMode
    setPosts([])
    setNextCursor(null)
    setError(null)
    setLoadMoreError(null)
    setIsLoading(true)
    setIsLoadingMore(false)
    setMode(nextMode)
  }

  const handleNewPost = async (
    content: string,
    files: readonly File[],
    onUploadProgress: (progress: number) => void,
  ) => {
    const mediaIds = await mediaApi.uploadFiles(files, onUploadProgress)
    await postsApi.create({ content, privacy: 'public', mediaIds })
    if (mountedRef.current && currentModeRef.current === mode) await loadFeed()
  }

  const mergeUpdatedPost = (updatedPost: Post) => {
    setPosts((currentPosts) => currentPosts.map((post) => post.id === updatedPost.id
      ? {
          ...post,
          content: updatedPost.content,
          privacy: updatedPost.privacy,
          updatedAtUtc: updatedPost.updatedAtUtc,
          mediaIds: updatedPost.mediaIds,
          commentCount: updatedPost.commentCount,
          reactionCounts: updatedPost.reactionCounts,
          viewerReaction: updatedPost.viewerReaction,
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
        <section className="rounded-xl border border-border bg-surface p-3">
          <div className="flex items-center justify-between gap-2">
            <nav aria-label={t('feedModes')} className="flex gap-2">
              {(['home', 'following'] as const).map((value) => <button key={value} type="button" aria-pressed={mode === value} onClick={() => chooseMode(value)} className={`rounded-full border-none px-4 py-2 text-sm font-semibold cursor-pointer ${mode === value ? 'bg-primary text-white' : 'bg-surface-2 text-text-muted hover:text-text'}`}>{t(value)}</button>)}
            </nav>
            <button type="button" onClick={() => void loadFeed()} disabled={isLoading} className="rounded-lg border border-border bg-transparent px-3 py-2 text-xs font-semibold text-text cursor-pointer disabled:opacity-50">{t('refresh')}</button>
          </div>
          <p className="mt-2 px-1 text-xs leading-relaxed text-text-muted">{t(mode === 'home' ? 'homeFeedDescription' : 'followingFeedDescription')}</p>
        </section>
        <div className="flex flex-col gap-4" aria-busy={isLoading || isLoadingMore}>
          {error && <div role="alert" className="rounded-lg bg-[#e41e3f]/10 border border-[#e41e3f]/40 p-3 text-sm text-[#ff8a9b]"><p>{error}</p><button type="button" onClick={() => void loadFeed()} className="mt-2 rounded-md border border-[#ff8a9b]/50 bg-transparent px-3 py-1 text-xs font-semibold text-[#ff8a9b] cursor-pointer">{t('refresh')}</button></div>}
          {isLoading && <div className="flex flex-col gap-4" aria-label={t('loadingFeed')}><div className="h-52 rounded-xl bg-surface-2 animate-pulse" /><div className="h-52 rounded-xl bg-surface-2 animate-pulse" /></div>}
          {!isLoading && posts.length === 0 && !error && <p className="text-sm text-text-muted">{t('noPostsYet')}</p>}
          {posts.map((post) => post.contentType === 'reel' ? <FeedReelCard key={post.id} item={post} /> : (
            <LivePostCard
              key={post.id}
              post={{ ...post, authorUserId: post.author.userId }}
              group={post.containerType === 'group' ? post.container : undefined}
              currentUserId={session!.user.id}
              onPostUpdated={mergeUpdatedPost}
              onPostDeleted={(postId) => setPosts((currentPosts) => currentPosts.filter((item) => item.id !== postId))}
            />
          ))}
          {!isLoading && <PaginationControls hasMore={nextCursor !== null} isLoading={isLoadingMore} error={loadMoreError} label={t('loadMorePosts')} onLoadMore={() => void loadFeed(nextCursor ?? undefined)} />}
        </div>
      </div>
    </div>
  )
}
