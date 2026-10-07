import { useInfiniteQuery, useQueries } from '@tanstack/react-query'
import { ApiError } from '../../api/client'
import { postsApi } from '../../api/posts'
import { usersApi } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import { Mascot } from 'page-mascot'
import PaginationControls from '../../shared/components/PaginationControls'
import LivePostCard from '../feed/components/LivePostCard'

export default function SavedPostsPage() {
  const { session } = useAuth()
  const queryKey = ['saved-posts', session?.user.id]
  const query = useInfiniteQuery({
    queryKey,
    enabled: Boolean(session),
    initialPageParam: undefined as string | undefined,
    queryFn: async ({ pageParam, signal }) => {
      await new Promise((resolve) => setTimeout(resolve, 0))
      signal.throwIfAborted()
      return postsApi.getSaved(pageParam, 20, signal)
    },
    getNextPageParam: (page) => page.nextCursor ?? undefined,
  })
  const posts = [...new Map(query.data?.pages.flatMap((page) => page.items).map((post) => [post.id, post])).values()]
  const authorIds = [...new Set(posts.flatMap((post) => post.authorUserId ? [post.authorUserId] : []))]
  const profiles = useQueries({ queries: authorIds.map((authorId) => ({
    queryKey: ['profile', session?.user.id, authorId],
    queryFn: ({ signal }: { signal: AbortSignal }) => usersApi.getById(authorId, { signal }),
  })) })
  const authors = Object.fromEntries(authorIds.map((id, index) => [id, profiles[index].data]))
  const isLoading = query.isPending
  const isLoadingMore = query.isFetchingNextPage
  const error = query.error && !query.isFetchNextPageError
    ? query.error instanceof ApiError ? query.error.message : 'Không thể tải bài viết đã lưu.'
    : null

  return (
    <main className="mx-auto min-h-screen w-full max-w-[680px] px-3 py-5 sm:px-4">
      <h1 className="font-heading text-3xl font-bold text-text">Đã lưu</h1>
      <p className="mt-1 text-sm text-text-muted">Bài viết bạn muốn xem lại.</p>
      <div className="mt-5 flex flex-col gap-4" aria-busy={isLoading || isLoadingMore}>
        {error && <div role="alert" className="rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 p-3 text-sm text-[#ff8a9b]"><p>{error}</p><button type="button" onClick={() => void query.refetch()} className="mt-2 rounded-md border border-[#ff8a9b]/50 bg-transparent px-3 py-1 text-xs font-semibold text-[#ff8a9b] cursor-pointer">Thử lại</button></div>}
        {isLoading && <><div className="h-52 animate-pulse rounded-xl bg-surface-2" /><div className="h-52 animate-pulse rounded-xl bg-surface-2" /></>}
        {!isLoading && !error && posts.length === 0 && (
          <div className="flex flex-col items-center gap-3 rounded-xl border border-border bg-surface py-10 px-4 text-center">
            <Mascot
              directions="/mascots/postbot-directions.webp"
              reactions="/mascots/postbot-reactions.webp"
              size={96}
              label="Saved posts bot"
            />
            <p className="text-base font-semibold text-text">Chưa có bài viết đã lưu</p>
            <p className="text-sm text-text-muted">Nhấn biểu tượng bookmark trên bất kỳ bài viết nào để lưu lại!</p>
          </div>
        )}

        {posts.map((post) => <LivePostCard key={post.id} post={{ ...post, viewerHasSaved: true }} author={post.authorUserId ? authors[post.authorUserId] : undefined} currentUserId={session!.user.id} />)}
        {!isLoading && <PaginationControls hasMore={query.hasNextPage} isLoading={isLoadingMore} error={query.isFetchNextPageError ? query.error?.message ?? null : null} label="Tải thêm bài viết đã lưu" onLoadMore={() => { if (!query.isFetching) void query.fetchNextPage() }} />}
      </div>
    </main>
  )
}
