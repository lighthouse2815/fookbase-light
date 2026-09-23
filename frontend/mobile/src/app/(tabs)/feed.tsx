import { useState } from 'react';
import { FlatList, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { Image } from 'expo-image';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { apiRequest } from '../../api/client';
import { feedApi } from '../../api/feed';
import type { MediaReadUrl } from '../../api/media';
import { storiesApi, type Story } from '../../api/stories';
import { resolveProfileImageUrl } from '../../api/users';
import { useAuth } from '../../auth/AuthProvider';
import { Avatar, Card, ErrorNotice, Icon, IconButton, Label, Loading, PostAction, styles, useTheme } from '../../components/ui';
import { PostCard } from '../../components/PostCard';

function StoryTile({ story }: { story: Story }) {
  const { session } = useAuth(); const t = useTheme();
  const previewPath = story.media.mediaType === 'video' ? story.media.posterAccessPath ?? story.media.accessPath : story.media.accessPath;
  const preview = useQuery({ queryKey: ['story-preview', session?.user.id, story.id], queryFn: () => apiRequest<MediaReadUrl>(previewPath), staleTime: 60_000 });
  const avatar = story.author.avatarUrl ? resolveProfileImageUrl(story.author.avatarUrl) : null;
  return <Pressable accessibilityRole="button" accessibilityLabel={`Xem tin của ${story.author.displayName}`} onPress={() => router.push(`/stories/${story.id}`)} style={[feedStyles.storyCard, { backgroundColor: t.surface2 }]}>
    {preview.data?.url ? <Image source={{ uri: preview.data.url }} contentFit="cover" style={StyleSheet.absoluteFill} /> : <View style={[StyleSheet.absoluteFill, { backgroundColor: t.surface3 }]} />}
    <View style={feedStyles.storyShade} />
    <View style={[feedStyles.storyAvatar, { borderColor: story.isViewed ? t.card : t.primary }]}><Avatar label={story.author.displayName} uri={avatar} size={34} /></View>
    <Text numberOfLines={2} style={feedStyles.storyName}>{story.author.displayName}</Text>
  </Pressable>;
}

function FeedHeader({ mode, onModeChange, username, userId }: { mode: 'home' | 'following'; onModeChange: (mode: 'home' | 'following') => void; username: string; userId: string }) {
  const t = useTheme();
  const initial = username?.slice(0, 1).toUpperCase() ?? 'F';
  const stories = useQuery({ queryKey: ['story-tray', userId], queryFn: storiesApi.tray });
  const tray = stories.data?.items.flatMap(author => author.stories.slice(0, 1)) ?? [];
  return <View style={feedStyles.headerWrap}>
    <View style={[feedStyles.topBar, { borderBottomColor: t.border }]}><Text style={[feedStyles.logo, { color: t.primary }]}>fookbase</Text><View style={styles.row}><IconButton label="Tạo bài viết" icon="add" onPress={() => router.push('/posts/create')} /><IconButton label="Tìm kiếm" icon="search" onPress={() => router.push('/search')} /><IconButton label="Tin nhắn" icon="messages" onPress={() => router.push('/messages')} /></View></View>
    <Card style={feedStyles.composer}>
      <View style={feedStyles.composerTop}><Avatar label={username || initial} size={43} online /><Pressable accessibilityRole="button" accessibilityLabel="Tạo bài viết" onPress={() => router.push('/posts/create')} style={[feedStyles.composerPrompt, { backgroundColor: t.surface2, borderColor: t.border }]}><Text style={{ color: t.muted, fontSize: 15 }}>Bạn đang nghĩ gì?</Text></Pressable><Icon name="image" color={t.success} size={28} /></View>
      <View style={[feedStyles.divider, { backgroundColor: t.border }]} />
      <View style={feedStyles.composerActions}><Pressable accessibilityRole="button" accessibilityLabel="Ảnh hoặc video" onPress={() => router.push('/posts/create')} style={feedStyles.composerAction}><Icon name="image" color={t.success} size={18} /><Text style={[feedStyles.composerActionLabel, { color: t.muted }]}>Ảnh/video</Text></Pressable><Pressable accessibilityRole="button" accessibilityLabel="Đăng tin" onPress={() => router.push({ pathname: '/media/create', params: { kind: 'story' } })} style={feedStyles.composerAction}><Icon name="sparkle" color="#8b5cf6" size={18} /><Text style={[feedStyles.composerActionLabel, { color: t.muted }]}>Tin</Text></Pressable><Pressable accessibilityRole="button" accessibilityLabel="Tạo reel" onPress={() => router.push({ pathname: '/media/create', params: { kind: 'reel' } })} style={feedStyles.composerAction}><Icon name="reels" color="#e95f7a" size={18} /><Text style={[feedStyles.composerActionLabel, { color: t.muted }]}>Reel</Text></Pressable></View>
    </Card>
    <View style={feedStyles.sectionTitle}><Label title style={{ fontSize: 19 }}>Tin</Label><Pressable accessibilityRole="button" accessibilityLabel="Xem tin của bạn bè" onPress={() => router.push('/stories')}><Text style={{ color: t.primary, fontSize: 13, fontWeight: '800' }}>Xem tất cả</Text></Pressable></View>
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={feedStyles.storyRail}>
      <Pressable accessibilityRole="button" accessibilityLabel="Đăng tin mới" onPress={() => router.push({ pathname: '/media/create', params: { kind: 'story' } })} style={[feedStyles.storyCard, { backgroundColor: t.surface2 }]}><View style={feedStyles.createStoryVisual}><Avatar label={username || initial} size={62} /></View><View style={[feedStyles.createStoryPlus, { backgroundColor: t.primary, borderColor: t.card }]}><Icon name="add" color="#fff" size={20} /></View><View style={[feedStyles.createStoryFooter, { backgroundColor: t.card }]}><Text style={{ color: t.text, fontSize: 12, fontWeight: '800' }}>Tạo tin</Text></View></Pressable>
      {tray.map(story => <StoryTile key={story.id} story={story} />)}
    </ScrollView>
    <View style={[feedStyles.modeTabs, { borderBottomColor: t.border }]}><Pressable accessibilityRole="button" accessibilityLabel="Dành cho bạn" onPress={() => onModeChange('home')} style={[feedStyles.modeTab, mode === 'home' && { borderBottomColor: t.primary }]}><Text style={{ color: mode === 'home' ? t.primary : t.muted, fontSize: 14, fontWeight: '800' }}>Dành cho bạn</Text></Pressable><Pressable accessibilityRole="button" accessibilityLabel="Đang theo dõi" onPress={() => onModeChange('following')} style={[feedStyles.modeTab, mode === 'following' && { borderBottomColor: t.primary }]}><Text style={{ color: mode === 'following' ? t.primary : t.muted, fontSize: 14, fontWeight: '800' }}>Đang theo dõi</Text></Pressable></View>
  </View>;
}

export default function FeedScreen() {
  const { session } = useAuth(); const t = useTheme(); const [mode, setMode] = useState<'home' | 'following'>('home');
  const query = useInfiniteQuery({ queryKey: ['feed', session?.user.id, mode], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam, signal }) => mode === 'home' ? feedApi.getHome(pageParam, 20, { signal }) : feedApi.getFollowing(pageParam, 20, { signal }), getNextPageParam: page => page.nextCursor ?? undefined });
  const items = query.data?.pages.flatMap(p => p.items) ?? [];
  return <FlatList style={{ backgroundColor: t.bg }} contentContainerStyle={feedStyles.list} data={items} keyExtractor={p => p.id} renderItem={({ item }) => <View style={feedStyles.postWrap}>{item.share && <View style={feedStyles.shareLine}><Icon name="share" color={t.success} size={15} /><Text style={{ color: t.muted, fontSize: 12 }}>{item.share.actor.displayName} đã chia sẻ</Text></View>}<PostCard post={item.contentType === 'share' && item.share ? item.share.originalPost : { ...item, authorUserId: item.author.userId, displayAuthor: item.displayAuthor, contentType: item.contentType === 'reel' ? 'reel' : 'standardPost' }} /></View>} ListHeaderComponent={<FeedHeader mode={mode} onModeChange={setMode} username={session?.user.username ?? 'bạn'} userId={session?.user.id ?? ''} />} ListEmptyComponent={query.isPending ? <Loading /> : query.error ? null : <View style={feedStyles.empty}><Icon name="sparkle" color={t.accent} size={25} /><Label title style={{ fontSize: 18 }}>Bảng tin đang chờ tia sáng đầu tiên</Label><Text style={{ color: t.muted, textAlign: 'center', lineHeight: 21 }}>Kết nối thêm bạn bè hoặc viết một điều nhỏ để bắt đầu.</Text></View>} ListFooterComponent={query.error ? <ErrorNotice error={query.error} retry={() => void query.refetch()} /> : query.hasNextPage ? <View style={{ paddingHorizontal: 16 }}><PostAction title={query.isFetchingNextPage ? 'Đang tải…' : 'Xem thêm bài viết'} icon="arrow" disabled={query.isFetchingNextPage} onPress={() => void query.fetchNextPage()} /></View> : <Text style={{ color: t.subtle, textAlign: 'center', paddingVertical: 20, fontSize: 12 }}>Bạn đã chạm đáy của dòng thời gian ✦</Text>} refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />;
}

const feedStyles = StyleSheet.create({
  list: { paddingBottom: 18 },
  headerWrap: { paddingTop: 8, paddingBottom: 10, gap: 12 },
  topBar: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: 16, paddingBottom: 10, borderBottomWidth: StyleSheet.hairlineWidth },
  logo: { fontSize: 29, fontWeight: '900', letterSpacing: -1.6 },
  composer: { padding: 14, gap: 12, borderRadius: 0 },
  composerTop: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  composerPrompt: { flex: 1, minHeight: 43, justifyContent: 'center', paddingHorizontal: 14, borderRadius: 22, borderWidth: 1 },
  divider: { height: 1 },
  composerActions: { flexDirection: 'row', justifyContent: 'space-between' },
  composerAction: { minHeight: 36, flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6 },
  composerActionLabel: { fontSize: 13, fontWeight: '700' },
  sectionTitle: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: 16, paddingTop: 2 },
  storyRail: { gap: 10, paddingHorizontal: 16 },
  storyCard: { width: 112, height: 178, borderRadius: 13, overflow: 'hidden', justifyContent: 'flex-end' },
  storyShade: { position: 'absolute', top: 0, right: 0, bottom: 0, left: 0, backgroundColor: '#00000025' },
  storyAvatar: { position: 'absolute', top: 9, left: 9, borderRadius: 21, borderWidth: 3 },
  storyName: { color: '#fff', fontSize: 12, fontWeight: '800', paddingHorizontal: 9, paddingBottom: 10, textShadowColor: '#000', textShadowRadius: 4 },
  createStoryVisual: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#3a3b3c' },
  createStoryPlus: { position: 'absolute', width: 30, height: 30, borderRadius: 16, borderWidth: 3, bottom: 30, left: 41, alignItems: 'center', justifyContent: 'center' },
  createStoryFooter: { height: 40, alignItems: 'center', justifyContent: 'flex-end', paddingBottom: 8 },
  modeTabs: { flexDirection: 'row', marginHorizontal: 16, borderBottomWidth: 1 },
  modeTab: { flex: 1, minHeight: 42, alignItems: 'center', justifyContent: 'center', borderBottomWidth: 3, borderBottomColor: 'transparent' },
  postWrap: { marginBottom: 10, gap: 6 },
  shareLine: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 16 },
  empty: { alignItems: 'center', gap: 8, paddingHorizontal: 28, paddingVertical: 48 },
});
