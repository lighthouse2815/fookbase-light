import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { feedApi } from '../../api/feed'
import type { FeedItem, FeedMode } from '../../api/feed'
import { mediaApi } from '../../api/media'
import { postsApi } from '../../api/posts'
import type { Post } from '../../api/posts'
import { birthdaysApi } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import { usePreferences } from '../../preferences'
import { Mascot } from 'page-mascot'
import PaginationControls from '../../shared/components/PaginationControls'
import FeedReelCard from './components/FeedReelCard'
import FeedShareCard from './components/FeedShareCard'
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
  const [todayBirthdayCount, setTodayBirthdayCount] = useState(0)
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

  useEffect(() => { void birthdaysApi.getToday().then((items) => setTodayBirthdayCount(items.length)).catch(() => undefined) }, [])

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
    privacy: 'public' | 'friends' | 'onlyMe',
    textBackground: string | null,
  ) => {
    const mediaIds = await mediaApi.uploadFiles(files, onUploadProgress)
    await postsApi.create({ content, privacy, mediaIds, textBackground })
    if (mountedRef.current && currentModeRef.current === mode) await loadFeed()
  }

  const mergeUpdatedPost = (updatedPost: Post) => {
    const merge = (post: FeedItem): FeedItem => ({
      ...post,
      content: updatedPost.content,
      privacy: updatedPost.privacy,
      updatedAtUtc: updatedPost.updatedAtUtc,
      mediaIds: updatedPost.mediaIds,
      commentCount: updatedPost.commentCount,
      reactionCounts: updatedPost.reactionCounts,
      viewerReaction: updatedPost.viewerReaction,
      textBackground: updatedPost.textBackground,
      mentions: updatedPost.mentions,
      reactionCount: Object.values(updatedPost.reactionCounts).reduce((total, count) => total + count, 0),
    })
    setPosts((currentPosts) => currentPosts.map((post) => {
      if (post.id === updatedPost.id) return merge(post)
      if (post.share?.originalPost.id === updatedPost.id) {
        return { ...post, share: { ...post.share, originalPost: updatedPost } }
      }
      return post
    }))
  }

  return (
    <div className="flex justify-center min-h-screen px-2 sm:px-4 py-4">
      <div className="w-full max-w-[680px] min-w-0 flex flex-col gap-4">
        <div className="flex items-center gap-3 rounded-2xl border border-border bg-surface px-4 py-2.5 shadow-sm">
          <Mascot
            directions="/mascots/fox-directions.webp"
            reactions="/mascots/fox-reactions.webp"
            size={56}
            label="Feed Fooky Mascot"
          />
          <div className="min-w-0 flex-1">
            <p className="text-sm font-bold text-text">Bảng tin Fookbase</p>
            <p className="text-xs text-text-muted">Fooky chào bạn! Hôm nay bạn có tin gì mới không?</p>
          </div>
        </div>
        <NewPostBox onPost={handleNewPost} />
        <StoryTray />
        {todayBirthdayCount > 0 && <Link to="/birthdays" className="rounded-xl border border-border bg-surface px-4 py-3 text-sm font-medium text-text no-underline">🎂 {todayBirthdayCount} bạn có sinh nhật hôm nay</Link>}
        <section className="rounded-xl border border-border bg-surface p-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
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
          {!isLoading && posts.length === 0 && !error && (
            <div className="flex flex-col items-center gap-3 rounded-xl border border-border bg-surface py-10 px-4 text-center">
              <Mascot
                directions="/mascots/fox-directions.webp"
                reactions="/mascots/fox-reactions.webp"
                size={96}
                label="Feed mascot"
              />
              <p className="text-base font-semibold text-text">{t('noPostsYet')}</p>
              <p className="text-sm text-text-muted">Hãy theo dõi thêm bạn bè để xem bài viết của họ ở đây!</p>
            </div>
          )}

          {posts.map((post) => post.contentType === 'share' ? (
            <FeedShareCard
              key={post.id}
              item={post}
              currentUserId={session!.user.id}
              onOriginalUpdated={mergeUpdatedPost}
              onOriginalDeleted={(postId) => setPosts((currentPosts) => currentPosts.filter((item) => item.share?.originalPost.id !== postId))}
            />
          ) : post.contentType === 'reel' ? <FeedReelCard key={post.id} item={post} /> : (
            <div key={post.id} className="flex flex-col gap-1">
              {post.isSuggested && <span className="px-2 text-xs font-semibold text-primary">{post.recommendationReason ?? t('suggestedReel')}</span>}
              <LivePostCard
                post={{ ...post, authorUserId: post.author.userId, contentType: 'standardPost' }}
                group={post.containerType === 'group' ? post.container : undefined}
                currentUserId={session!.user.id}
                onPostUpdated={mergeUpdatedPost}
                onPostDeleted={(postId) => setPosts((currentPosts) => currentPosts.filter((item) => item.id !== postId))}
              />
            </div>
          ))}
          {!isLoading && <PaginationControls hasMore={nextCursor !== null} isLoading={isLoadingMore} error={loadMoreError} label={t('loadMorePosts')} onLoadMore={() => void loadFeed(nextCursor ?? undefined)} />}
        </div>
      </div>
    </div>
  )
}
