import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { postsApi, type Post } from '../../api/posts'
import { useAuth } from '../../auth/useAuth'
import PaginationControls from '../../shared/components/PaginationControls'
import LivePostCard from '../feed/components/LivePostCard'

export default function SavedPostsPage() {
  const { session } = useAuth()
  const requestRef = useRef<AbortController | null>(null)
  const [posts, setPosts] = useState<Post[]>([])
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)

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
    } catch (requestError) {
      if (controller.signal.aborted || requestRef.current !== controller) return
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải bài viết đã lưu.')
    } finally {
      if (requestRef.current === controller) {
        if (append) setIsLoadingMore(false)
        else setIsLoading(false)
      }
    }
  }, [])

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
      } catch (requestError) {
        if (controller.signal.aborted || requestRef.current !== controller) return
        setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải bài viết đã lưu.')
      } finally {
        if (requestRef.current === controller) setIsLoading(false)
      }
    }

    void loadInitial()
    return () => {
      controller.abort()
      if (requestRef.current === controller) requestRef.current = null
    }
  }, [])

  const updatePost = (updated: Post) => {
    setPosts((current) => current.map((post) => post.id === updated.id ? updated : post))
  }

  return (
    <main className="mx-auto min-h-screen w-full max-w-[680px] px-3 py-5 sm:px-4">
      <h1 className="font-heading text-3xl font-bold text-text">Đã lưu</h1>
      <p className="mt-1 text-sm text-text-muted">Bài viết bạn muốn xem lại.</p>
      <div className="mt-5 flex flex-col gap-4" aria-busy={isLoading || isLoadingMore}>
        {error && <div role="alert" className="rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-3 text-sm text-[#ff8a9b]"><p>{error}</p><button type="button" onClick={() => void load()} className="mt-2 rounded-md border border-[#ff8a9b]/50 bg-transparent px-3 py-1 text-xs font-semibold text-[#ff8a9b] cursor-pointer">Thử lại</button></div>}
        {isLoading && <><div className="h-52 animate-pulse rounded-xl bg-surface-2" /><div className="h-52 animate-pulse rounded-xl bg-surface-2" /></>}
        {!isLoading && !error && posts.length === 0 && <p className="rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">Chưa có bài viết đã lưu.</p>}
        {posts.map((post) => <LivePostCard key={post.id} post={post} currentUserId={session!.user.id} onPostUpdated={updatePost} onPostDeleted={(postId) => setPosts((current) => current.filter((post) => post.id !== postId))} />)}
        {!isLoading && <PaginationControls hasMore={nextCursor !== null} isLoading={isLoadingMore} error={null} label="Tải thêm bài viết đã lưu" onLoadMore={() => void load(nextCursor ?? undefined)} />}
      </div>
    </main>
  )
}
