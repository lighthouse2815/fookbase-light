import { useInfiniteQuery } from '@tanstack/react-query';
import { postsApi } from '../api/posts';
import { useAuth } from '../auth/AuthProvider';
import { Button, ErrorNotice, Label, Loading, Screen } from '../components/ui';
import { PostCard } from '../components/PostCard';
export default function Saved() {
  const { session } = useAuth();
  const query = useInfiniteQuery({ queryKey: ['saved', session?.user.id], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam, signal }) => postsApi.getSaved(pageParam, 20, signal), getNextPageParam: p => p.nextCursor ?? undefined });
  return <Screen><Label title>Bài viết đã lưu</Label>{query.isPending && <Loading />}{query.data?.pages.flatMap(p => p.items).map(p => <PostCard key={p.id} post={p} />)}{query.error && <ErrorNotice error={query.error} retry={() => void query.refetch()} />}{query.data?.pages[0].items.length === 0 && <Label muted>Bạn chưa lưu bài viết nào.</Label>}{query.hasNextPage && <Button title="Xem thêm" disabled={query.isFetchingNextPage} onPress={() => void query.fetchNextPage()} />}</Screen>;
}
