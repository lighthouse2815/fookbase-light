import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { postsApi, type Post } from '../../api/posts'
import { usersApi, type UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import Mascot from '../../shared/components/Mascot'
import PaginationControls from '../../shared/components/PaginationControls'
import LivePostCard from '../feed/components/LivePostCard'

export default function SavedPostsPage() {
  const { session } = useAuth()
  const requestRef = useRef<AbortController | null>(null)
  const [posts, setPosts] = useState<Post[]>([])
  const [authors, setAuthors] = useState<Record<string, UserProfile>>({})
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const loadAuthors = useCallback(async (items: readonly Post[], controller: AbortController) => {
    const authorIds = [...new Set(items.flatMap((post) => post.authorUserId ? [post.authorUserId] : []))]
    if (authorIds.length === 0) return

    const results = await Promise.allSettled(authorIds.map((authorId) => usersApi.getById(authorId)))
    if (controller.signal.aborted || requestRef.current !== controller) return
    setAuthors((current) => {
      const next = { ...current }
      results.forEach((result, index) => {
        if (result.status === 'fulfilled') next[authorIds[index]] = result.value
      })
      return next
    })
  }, [])

  const load = useCallback(async (cursor?: string) => {
    const append = cursor !== undefined
    requestRef.current?.abort()
    const controller = new AbortController()
    requestRef.current = controller
    if (append) setIsLoadingMore(true)
    else setIsLoading(true)
    setError(null)
    try {
      const page = await postsApi.getSaved(cursor, 20, controller.signal)
      if (controller.signal.aborted || requestRef.current !== controller) return
      setPosts((current) => append
        ? [...current, ...page.items.filter((item) => !current.some((post) => post.id === item.id))]
        : page.items)
      setNextCursor(page.nextCursor)
      void loadAuthors(page.items, controller)
    } catch (requestError) {
      if (controller.signal.aborted || requestRef.current !== controller) return
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải bài viết đã lưu.')
    } finally {
      if (requestRef.current === controller) {
        if (append) setIsLoadingMore(false)
        else setIsLoading(false)
      }
    }
  }, [loadAuthors])

  useEffect(() => {
    const controller = new AbortController()
    requestRef.current?.abort()
    requestRef.current = controller

    const loadInitial = async () => {
      try {
        const page = await postsApi.getSaved(undefined, 20, controller.signal)
        if (controller.signal.aborted || requestRef.current !== controller) return
        setPosts(page.items)
        setNextCursor(page.nextCursor)
        void loadAuthors(page.items, controller)
      } catch (requestError) {
        if (controller.signal.aborted || requestRef.current !== controller) return
        setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải bài viết đã lưu.')
      } finally {
        if (requestRef.current === controller) setIsLoading(false)
      }
    }

    void loadInitial()
    return () => {
      const activeController = requestRef.current
      requestRef.current = null
      activeController?.abort()
    }
  }, [loadAuthors])

  const updatePost = (updated: Post) => {
    setPosts((current) => updated.viewerHasSaved
      ? current.map((post) => post.id === updated.id ? updated : post)
      : current.filter((post) => post.id !== updated.id))
  }

  return (
    <main className="mx-auto min-h-screen w-full max-w-[680px] px-3 py-5 sm:px-4">
      <h1 className="font-heading text-3xl font-bold text-text">Đã lưu</h1>
      <p className="mt-1 text-sm text-text-muted">Bài viết bạn muốn xem lại.</p>
      <div className="mt-5 flex flex-col gap-4" aria-busy={isLoading || isLoadingMore}>
        {error && <div role="alert" className="rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-3 text-sm text-[#ff8a9b]"><p>{error}</p><button type="button" onClick={() => void load()} className="mt-2 rounded-md border border-[#ff8a9b]/50 bg-transparent px-3 py-1 text-xs font-semibold text-[#ff8a9b] cursor-pointer">Thử lại</button></div>}
        {isLoading && <><div className="h-52 animate-pulse rounded-xl bg-surface-2" /><div className="h-52 animate-pulse rounded-xl bg-surface-2" /></>}
        {!isLoading && !error && posts.length === 0 && (
          <div className="flex flex-col items-center gap-3 rounded-xl border border-border bg-surface py-10 px-4 text-center">
            <Mascot size="lg" animated />
            <p className="text-base font-semibold text-text">Chưa có bài viết đã lưu</p>
            <p className="text-sm text-text-muted">Nhấn biểu tượng bookmark trên bất kỳ bài viết nào để lưu lại!</p>
          </div>
        )}

        {posts.map((post) => <LivePostCard key={post.id} post={{ ...post, viewerHasSaved: true }} author={post.authorUserId ? authors[post.authorUserId] : undefined} currentUserId={session!.user.id} onPostUpdated={updatePost} onPostDeleted={(postId) => setPosts((current) => current.filter((post) => post.id !== postId))} />)}
        {!isLoading && <PaginationControls hasMore={nextCursor !== null} isLoading={isLoadingMore} error={null} label="Tải thêm bài viết đã lưu" onLoadMore={() => void load(nextCursor ?? undefined)} />}
      </div>
    </main>
  )
}
