import { useCallback, useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { postsApi, type Post } from '../../api/posts'
import { useAuth } from '../../auth/useAuth'
import PaginationControls from '../../shared/components/PaginationControls'
import LivePostCard from '../feed/components/LivePostCard'

export default function HashtagPage() {
  const { tag: rawTag = '' } = useParams()
  const tag = rawTag.replace(/^#/, '').toLowerCase()
  const { session } = useAuth()
  const [posts, setPosts] = useState<Post[]>([])
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (cursor?: string) => {
    const append = cursor !== undefined
    if (append) setIsLoadingMore(true)
    else setIsLoading(true)
    setError(null)
    try {
      const page = await postsApi.getHashtagPosts(tag, cursor)
      setPosts((current) => append
        ? [...current, ...page.items.filter((item) => !current.some((post) => post.id === item.id))]
        : page.items)
      setNextCursor(page.nextCursor)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải hashtag này.')
    } finally {
      if (append) setIsLoadingMore(false)
      else setIsLoading(false)
    }
  }, [tag])

  useEffect(() => { void load() }, [load])

  if (!tag) return <main className="mx-auto min-h-screen w-full max-w-[680px] px-3 py-5 text-sm text-text-muted">Hashtag không hợp lệ.</main>

  return (
    <main className="mx-auto min-h-screen w-full max-w-[680px] px-3 py-5 sm:px-4">
      <h1 className="font-heading text-3xl font-bold text-text">#{tag}</h1>
      <p className="mt-1 text-sm text-text-muted">Bài viết công khai bạn có quyền xem.</p>
      <div className="mt-5 flex flex-col gap-4" aria-busy={isLoading || isLoadingMore}>
        {error && <div role="alert" className="rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-3 text-sm text-[#ff8a9b]"><p>{error}</p><button type="button" onClick={() => void load()} className="mt-2 rounded-md border border-[#ff8a9b]/50 bg-transparent px-3 py-1 text-xs font-semibold text-[#ff8a9b] cursor-pointer">Thử lại</button></div>}
        {isLoading && <><div className="h-52 animate-pulse rounded-xl bg-surface-2" /><div className="h-52 animate-pulse rounded-xl bg-surface-2" /></>}
        {!isLoading && !error && posts.length === 0 && <p className="rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">Chưa có bài viết phù hợp.</p>}
        {posts.map((post) => <LivePostCard key={post.id} post={post} currentUserId={session!.user.id} onPostUpdated={(updated) => setPosts((current) => current.map((post) => post.id === updated.id ? updated : post))} onPostDeleted={(postId) => setPosts((current) => current.filter((post) => post.id !== postId))} />)}
        {!isLoading && <PaginationControls hasMore={nextCursor !== null} isLoading={isLoadingMore} error={null} label="Tải thêm bài viết" onLoadMore={() => void load(nextCursor ?? undefined)} />}
      </div>
    </main>
  )
}
