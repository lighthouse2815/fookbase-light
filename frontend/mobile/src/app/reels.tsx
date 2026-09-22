import { useState } from 'react';
import { Text, View } from 'react-native';
import { Image } from 'expo-image';
import { router } from 'expo-router';
import { useInfiniteQuery } from '@tanstack/react-query';
import { reelsApi, type ReelFeedMode } from '../api/reels';
import { resolveProfileImageUrl } from '../api/users';
import { useAuth } from '../auth/AuthProvider';
import { Button, Card, ErrorNotice, Label, Loading, Screen, styles, useTheme } from '../components/ui';
import { MediaView } from '../components/MediaView';
export default function Reels() {
  const [mode, setMode] = useState<ReelFeedMode>('forYou'); const [index, setIndex] = useState(0); const { session } = useAuth();
  const t = useTheme();
  const query = useInfiniteQuery({ queryKey: ['reels', session?.user.id, mode], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => reelsApi.getFeed(mode, pageParam), getNextPageParam: p => p.nextCursor ?? undefined });
  const items = query.data?.pages.flatMap(p => p.items) ?? []; const current = items[index];
  return <Screen><Label title>Reels</Label><View style={styles.row}>{(['forYou', 'following'] as const).map((m,i) => <Button key={m} title={['Dành cho bạn', 'Đang theo dõi'][i]} secondary={mode !== m} onPress={() => { setIndex(0); setMode(m); }} />)}<Button title="Tạo Reel" onPress={() => router.push({ pathname: '/media/create', params: { kind: 'reel' } })} /></View>
    {query.isPending && <Loading />}{query.error && <ErrorNotice error={query.error} retry={() => void query.refetch()} />}
    {current ? <Card key={current.id}><View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>{current.author.avatarUrl ? <Image source={resolveProfileImageUrl(current.author.avatarUrl)} accessibilityLabel={`Ảnh đại diện của ${current.author.displayName}`} cachePolicy="memory-disk" contentFit="cover" style={{ width: 40, height: 40, borderRadius: 20 }} /> : <View accessibilityLabel={`Ảnh đại diện của ${current.author.displayName}`} style={{ width: 40, height: 40, alignItems: 'center', justifyContent: 'center', borderRadius: 20, backgroundColor: t.primary }}><Text style={{ color: '#fff', fontSize: 12, fontWeight: '700' }}>{current.author.displayName.slice(0, 2).toUpperCase()}</Text></View>}<Label>{current.author.displayName}</Label></View><MediaView path={`/api/reels/${current.id}/video/access`} /><Label>{current.caption}</Label><Button secondary title="Bình luận và cảm xúc" onPress={() => router.push(`/posts/${current.id}`)} /></Card> : !query.isPending && <Label muted>Chưa có video.</Label>}
    <View style={styles.row}><Button secondary title="Trước" disabled={index === 0} onPress={() => setIndex(index - 1)} /><Button title="Tiếp theo" disabled={query.isFetchingNextPage || (index >= items.length - 1 && !query.hasNextPage)} onPress={() => { if (index < items.length - 1) setIndex(index + 1); else void query.fetchNextPage().then(result => { if ((result.data?.pages.flatMap(p => p.items).length ?? 0) > index + 1) setIndex(index + 1); }); }} /></View>
  </Screen>;
}
