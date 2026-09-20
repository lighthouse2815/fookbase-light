import { useState } from 'react';
import { FlatList, View } from 'react-native';
import { router } from 'expo-router';
import { useInfiniteQuery } from '@tanstack/react-query';
import { feedApi } from '../../api/feed';
import { useAuth } from '../../auth/AuthProvider';
import { Button, ErrorNotice, Label, Loading, styles, useTheme } from '../../components/ui';
import { PostCard } from '../../components/PostCard';
export default function FeedScreen() {
  const { session } = useAuth(); const t = useTheme(); const [mode, setMode] = useState<'home' | 'following'>('home');
  const query = useInfiniteQuery({ queryKey: ['feed', session?.user.id, mode], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam, signal }) => mode === 'home' ? feedApi.getHome(pageParam, 20, { signal }) : feedApi.getFollowing(pageParam, 20, { signal }), getNextPageParam: page => page.nextCursor ?? undefined });
  return <FlatList style={{ backgroundColor: t.bg }} contentContainerStyle={styles.screen} data={query.data?.pages.flatMap(p => p.items) ?? []} keyExtractor={p => p.id} renderItem={({ item }) => <PostCard post={item.contentType === 'share' && item.share ? item.share.originalPost : { ...item, authorUserId: item.author.userId, contentType: item.contentType === 'reel' ? 'reel' : 'standardPost' }} />}
    ListHeaderComponent={<View style={{ gap: 12 }}><Label title>Bảng tin</Label><View style={styles.row}><Button title="Tạo bài viết" onPress={() => router.push('/posts/create')} /><Button secondary title="Menu" onPress={() => router.push('/menu')} /></View><View style={styles.row}><Button secondary={mode !== 'home'} title="Dành cho bạn" onPress={() => setMode('home')} /><Button secondary={mode !== 'following'} title="Đang theo dõi" onPress={() => setMode('following')} /></View></View>}
    ListEmptyComponent={query.isPending ? <Loading /> : query.error ? null : <Label muted>Chưa có bài viết. Hãy kết nối thêm bạn bè.</Label>}
    ListFooterComponent={query.error ? <ErrorNotice error={query.error} retry={() => void query.refetch()} /> : query.hasNextPage ? <Button secondary disabled={query.isFetchingNextPage} title="Xem thêm" onPress={() => void query.fetchNextPage()} /> : null}
    refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />;
}
