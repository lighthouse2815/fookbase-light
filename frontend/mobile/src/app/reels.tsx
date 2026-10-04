import { useState } from 'react';
import { Pressable, StyleSheet, Text, useWindowDimensions, View } from 'react-native';
import { Image } from 'expo-image';
import { router } from 'expo-router';
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { reelsApi, type ReelFeedMode } from '../api/reels';
import { postsApi } from '../api/posts';
import { resolveProfileImageUrl, usersApi } from '../api/users';
import { useAuth } from '../auth/AuthProvider';
import { ErrorNotice, Icon, IconButton, Label, Loading, useTheme } from '../components/ui';
import { MediaView } from '../components/MediaView';

export default function Reels() {
  const [mode, setMode] = useState<ReelFeedMode>('forYou'); const [index, setIndex] = useState(0); const { session } = useAuth(); const t = useTheme(); const cache = useQueryClient();
  const { height: viewportHeight } = useWindowDimensions();
  const query = useInfiniteQuery({ queryKey: ['reels', session?.user.id, mode], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => reelsApi.getFeed(mode, pageParam), getNextPageParam: p => p.nextCursor ?? undefined });
  const items = query.data?.pages.flatMap(p => p.items) ?? []; const current = items[index];
  const action = useMutation({ mutationFn: (operation: () => Promise<unknown>) => operation(), onSuccess: () => cache.invalidateQueries({ queryKey: ['reels', session?.user.id] }) });
  const next = () => { if (index < items.length - 1) setIndex(index + 1); else void query.fetchNextPage().then(result => { if ((result.data?.pages.flatMap(p => p.items).length ?? 0) > index + 1) setIndex(index + 1); }); };
  return <View style={reelStyles.screen}>
    <View style={reelStyles.topbar}><Pressable accessibilityRole="button" accessibilityLabel="Quay lại" onPress={() => router.back()} style={reelStyles.back}><Text style={reelStyles.backText}>‹</Text></Pressable><Text style={reelStyles.title}>Reels</Text><View style={reelStyles.topActions}><IconButton label="Tìm Reels" icon="search" onPress={() => router.push('/search')} /><IconButton label="Tạo Reel" icon="add" onPress={() => router.push({ pathname: '/media/create', params: { kind: 'reel' } })} /></View></View>
    <View style={reelStyles.modeTabs}>{(['forYou', 'following'] as const).map((value, position) => <Pressable key={value} accessibilityRole="tab" accessibilityLabel={position === 0 ? 'Dành cho bạn' : 'Đang theo dõi'} accessibilityState={{ selected: mode === value }} onPress={() => { setIndex(0); setMode(value); }} style={[reelStyles.modeTab, mode === value && reelStyles.modeTabActive]}><Text style={reelStyles.modeText}>{position === 0 ? 'Dành cho bạn' : 'Đang theo dõi'}</Text></Pressable>)}</View>
    {query.isPending && <View style={reelStyles.center}><Loading /></View>}
    {query.error && <View style={reelStyles.center}><ErrorNotice error={query.error} retry={() => void query.refetch()} /></View>}
    {current && <View style={reelStyles.stage}>
      <MediaView path={`/api/reels/${current.id}/video/access`} height={Math.max(440, viewportHeight - 150)} nativeControls={false} borderRadius={0} contentFit="cover" autoPlay loop />
      <View pointerEvents="none" style={reelStyles.gradient} />
      <View style={reelStyles.creator}><View style={reelStyles.creatorRow}>{current.author.avatarUrl ? <Image source={resolveProfileImageUrl(current.author.avatarUrl)} contentFit="cover" style={reelStyles.avatar} /> : <View style={[reelStyles.avatar, { backgroundColor: t.primary }]}><Text style={reelStyles.avatarInitial}>{current.author.displayName.slice(0, 1).toUpperCase()}</Text></View>}<Text numberOfLines={1} style={reelStyles.creatorName}>{current.author.displayName}</Text><Pressable accessibilityRole="button" accessibilityLabel={current.viewerFollowsAuthor ? 'Bỏ theo dõi tác giả' : 'Theo dõi tác giả'} disabled={action.isPending || current.author.userId === session?.user.id} onPress={() => action.mutate(() => current.viewerFollowsAuthor ? usersApi.unfollow(current.author.userId) : usersApi.follow(current.author.userId))} style={[reelStyles.follow, (action.isPending || current.author.userId === session?.user.id) && reelStyles.disabled]}><Text style={reelStyles.followText}>{current.viewerFollowsAuthor ? 'Đang theo dõi' : 'Theo dõi'}</Text></Pressable></View><Text numberOfLines={2} style={reelStyles.caption}>{current.caption || 'Chia sẻ một khoảnh khắc cùng Fookbase'}</Text></View>
      <View style={reelStyles.actions}><Pressable accessibilityRole="button" accessibilityLabel="Thích Reel" disabled={action.isPending} onPress={() => action.mutate(() => current.viewerReaction ? postsApi.removeReaction(current.id) : postsApi.setReaction(current.id, 'like'))} style={[reelStyles.action, action.isPending && reelStyles.disabled]}><Icon name="heart" color={current.viewerReaction ? '#ff5c73' : '#fff'} size={31} /><Text style={reelStyles.actionText}>{current.reactionCount || 'Thích'}</Text></Pressable><Pressable accessibilityRole="button" accessibilityLabel="Bình luận Reel" onPress={() => router.push(`/posts/${current.id}`)} style={reelStyles.action}><Icon name="comment" color="#fff" size={31} /><Text style={reelStyles.actionText}>Bình luận</Text></Pressable><Pressable accessibilityRole="button" accessibilityLabel="Chia sẻ Reel" disabled={action.isPending || !session} onPress={() => { if (session) action.mutate(() => postsApi.share(current.id, { destinationType: 'profile', destinationId: session.user.id })); }} style={[reelStyles.action, (action.isPending || !session) && reelStyles.disabled]}><Icon name="share" color="#fff" size={31} /><Text style={reelStyles.actionText}>Chia sẻ</Text></Pressable><Pressable accessibilityRole="button" accessibilityLabel={current.viewerHasSaved ? 'Bỏ lưu Reel' : 'Lưu Reel'} disabled={action.isPending} onPress={() => action.mutate(() => current.viewerHasSaved ? postsApi.removeSaved(current.id) : postsApi.save(current.id))} style={[reelStyles.action, action.isPending && reelStyles.disabled]}><Icon name="bookmark" color={current.viewerHasSaved ? '#2d88ff' : '#fff'} size={30} /><Text style={reelStyles.actionText}>{current.viewerHasSaved ? 'Đã lưu' : 'Lưu'}</Text></Pressable></View>
      {action.error && <View style={reelStyles.actionError}><ErrorNotice error={action.error} /></View>}
      <Pressable accessibilityRole="button" accessibilityLabel="Reel tiếp theo" onPress={next} disabled={query.isFetchingNextPage || (index >= items.length - 1 && !query.hasNextPage)} style={reelStyles.nextZone} />
    </View>}
    {!current && !query.isPending && !query.error && <View style={reelStyles.center}><Label muted>Chưa có Reel để xem.</Label></View>}
  </View>;
}

const reelStyles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#000' },
  topbar: { minHeight: 64, paddingTop: 13, paddingHorizontal: 15, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  back: { width: 42, height: 42, alignItems: 'center', justifyContent: 'center' },
  backText: { color: '#fff', fontSize: 44, fontWeight: '300', lineHeight: 44, marginTop: -7 },
  title: { color: '#fff', fontSize: 29, fontWeight: '900', letterSpacing: -0.8 },
  topActions: { flexDirection: 'row', gap: 8 },
  modeTabs: { flexDirection: 'row', justifyContent: 'center', gap: 25, paddingBottom: 9 },
  modeTab: { paddingHorizontal: 2, paddingBottom: 7, borderBottomWidth: 3, borderBottomColor: 'transparent' },
  modeTabActive: { borderBottomColor: '#fff' },
  modeText: { color: '#fff', fontSize: 14, fontWeight: '800' },
  stage: { flex: 1, position: 'relative', overflow: 'hidden' },
  gradient: { position: 'absolute', left: 0, right: 0, bottom: 0, height: 290, backgroundColor: '#00000072' },
  creator: { position: 'absolute', bottom: 30, left: 16, right: 104, gap: 9 },
  creatorRow: { flexDirection: 'row', alignItems: 'center', gap: 9 },
  avatar: { width: 42, height: 42, borderRadius: 21, borderWidth: 2, borderColor: '#fff', alignItems: 'center', justifyContent: 'center' },
  avatarInitial: { color: '#fff', fontWeight: '900', fontSize: 16 },
  creatorName: { color: '#fff', fontSize: 16, fontWeight: '900', flexShrink: 1 },
  follow: { borderWidth: 1, borderColor: '#ffffffaa', borderRadius: 8, paddingHorizontal: 9, paddingVertical: 6 },
  followText: { color: '#fff', fontSize: 12, fontWeight: '800' },
  caption: { color: '#fff', fontSize: 15, lineHeight: 21 },
  actions: { position: 'absolute', right: 13, bottom: 25, alignItems: 'center', gap: 20 },
  action: { alignItems: 'center', gap: 3 },
  actionText: { color: '#fff', fontSize: 11, fontWeight: '800', maxWidth: 74, textAlign: 'center' },
  disabled: { opacity: 0.48 },
  actionError: { position: 'absolute', top: 12, left: 12, right: 12 },
  nextZone: { position: 'absolute', top: 0, right: 0, width: 70, height: '50%' },
  center: { flex: 1, paddingHorizontal: 16, justifyContent: 'center' },
});
