import { useState } from 'react';
import { FlatList, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { useInfiniteQuery } from '@tanstack/react-query';
import { feedApi } from '../../api/feed';
import { useAuth } from '../../auth/AuthProvider';
import { ActionChip, Avatar, Card, ErrorNotice, Icon, IconButton, Label, Loading, PostAction, styles, useTheme } from '../../components/ui';
import { PostCard } from '../../components/PostCard';

function FeedHeader({ mode, onModeChange, username }: { mode: 'home' | 'following'; onModeChange: (mode: 'home' | 'following') => void; username: string }) {
  const t = useTheme();
  const initial = username?.slice(0, 1).toUpperCase() ?? 'F';
  return <View style={feedStyles.headerWrap}>
    <View style={feedStyles.topBar}><View><Text style={[feedStyles.logo, { color: t.text }]}>fookbase</Text><Text style={{ color: t.muted, fontSize: 10, fontWeight: '800', letterSpacing: 1.6 }}>NHỮNG ĐIỀU NHỎ, KẾT NỐI LỚN</Text></View><View style={styles.row}><IconButton label="Tìm kiếm" icon="search" onPress={() => router.push('/search')} /><IconButton label="Trợ lý AI" icon="sparkle" onPress={() => router.push('/ai-chat')} /></View></View>
    <Card style={feedStyles.composer}>
      <View style={feedStyles.composerTop}><Avatar label={username || initial} size={43} online /><Pressable accessibilityRole="button" accessibilityLabel="Tạo bài viết" onPress={() => router.push('/posts/create')} style={[feedStyles.composerPrompt, { backgroundColor: t.surface2 }]}><Text style={{ color: t.muted, fontSize: 15 }}>Bạn đang nghĩ gì, {username || 'bạn'}?</Text></Pressable><IconButton label="Tạo bài viết" icon="add" onPress={() => router.push('/posts/create')} /></View>
      <View style={[feedStyles.divider, { backgroundColor: t.border }]} />
      <View style={feedStyles.composerActions}><ActionChip title="Ảnh / video" icon="image" onPress={() => router.push('/posts/create')} /><ActionChip title="Tin" icon="sparkle" onPress={() => router.push({ pathname: '/media/create', params: { kind: 'story' } })} /><ActionChip title="Reel" icon="reels" onPress={() => router.push({ pathname: '/media/create', params: { kind: 'reel' } })} /></View>
    </Card>
    <View style={feedStyles.sectionTitle}><View><Label title style={{ fontSize: 19 }}>Tin</Label><Text style={{ color: t.muted, fontSize: 12 }}>Khoảnh khắc mới từ cộng đồng</Text></View><Pressable accessibilityRole="button" accessibilityLabel="Xem tin của bạn bè" onPress={() => router.push('/stories')}><Text style={{ color: t.primary, fontSize: 13, fontWeight: '800' }}>Xem tất cả</Text></Pressable></View>
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={feedStyles.storyRail}>
      <Pressable accessibilityRole="button" accessibilityLabel="Đăng tin mới" onPress={() => router.push({ pathname: '/media/create', params: { kind: 'story' } })} style={[feedStyles.storyCard, { backgroundColor: t.surface2, borderColor: t.border }]}><View style={[feedStyles.storyOrb, { backgroundColor: t.primary }]}><Icon name="add" color="#fff" size={25} /></View><Text style={[feedStyles.storyLabel, { color: t.text }]}>Tin của bạn</Text><Text style={{ color: t.muted, fontSize: 11 }}>Tạo tia sáng</Text></Pressable>
      {([['Khám phá', 'sparkle', '#7b61ff'], ['Bạn bè', 'people', '#3fa877'], ['Reels', 'reels', '#d96845'], ['Đã lưu', 'bookmark', '#c95787']] as const).map(([title, icon, color]) => <Pressable key={title} accessibilityRole="button" accessibilityLabel={title} onPress={() => router.push(title === 'Reels' ? '/reels' : title === 'Đã lưu' ? '/saved' : title === 'Bạn bè' ? '/friends' : '/stories')} style={[feedStyles.storyCard, { backgroundColor: color, borderColor: `${color}aa` }]}><View style={feedStyles.storyPattern}><Icon name={icon} color="#fff" size={30} /></View><Text style={feedStyles.storyLabel}>{title}</Text><Text style={feedStyles.storyHint}>Mở xem</Text></Pressable>)}
    </ScrollView>
    <View style={[feedStyles.modeTabs, { backgroundColor: t.surface2, borderColor: t.border }]}><Pressable accessibilityRole="button" accessibilityLabel="Dành cho bạn" onPress={() => onModeChange('home')} style={[feedStyles.modeTab, mode === 'home' && { backgroundColor: t.card }]}><Icon name="sparkle" color={mode === 'home' ? t.primary : t.muted} size={16} /><Text style={{ color: mode === 'home' ? t.text : t.muted, fontSize: 13, fontWeight: '800' }}>Dành cho bạn</Text></Pressable><Pressable accessibilityRole="button" accessibilityLabel="Đang theo dõi" onPress={() => onModeChange('following')} style={[feedStyles.modeTab, mode === 'following' && { backgroundColor: t.card }]}><Icon name="people" color={mode === 'following' ? t.primary : t.muted} size={16} /><Text style={{ color: mode === 'following' ? t.text : t.muted, fontSize: 13, fontWeight: '800' }}>Đang theo dõi</Text></Pressable></View>
  </View>;
}

export default function FeedScreen() {
  const { session } = useAuth(); const t = useTheme(); const [mode, setMode] = useState<'home' | 'following'>('home');
  const query = useInfiniteQuery({ queryKey: ['feed', session?.user.id, mode], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam, signal }) => mode === 'home' ? feedApi.getHome(pageParam, 20, { signal }) : feedApi.getFollowing(pageParam, 20, { signal }), getNextPageParam: page => page.nextCursor ?? undefined });
  const items = query.data?.pages.flatMap(p => p.items) ?? [];
  return <FlatList style={{ backgroundColor: t.bg }} contentContainerStyle={feedStyles.list} data={items} keyExtractor={p => p.id} renderItem={({ item }) => <View style={feedStyles.postWrap}>{item.share && <View style={feedStyles.shareLine}><Icon name="share" color={t.success} size={15} /><Text style={{ color: t.muted, fontSize: 12 }}>{item.share.actor.displayName} đã chia sẻ</Text></View>}<PostCard post={item.contentType === 'share' && item.share ? item.share.originalPost : { ...item, authorUserId: item.author.userId, displayAuthor: item.displayAuthor, contentType: item.contentType === 'reel' ? 'reel' : 'standardPost' }} /></View>} ListHeaderComponent={<FeedHeader mode={mode} onModeChange={setMode} username={session?.user.username ?? 'bạn'} />} ListEmptyComponent={query.isPending ? <Loading /> : query.error ? null : <View style={feedStyles.empty}><Icon name="sparkle" color={t.accent} size={25} /><Label title style={{ fontSize: 18 }}>Bảng tin đang chờ tia sáng đầu tiên</Label><Text style={{ color: t.muted, textAlign: 'center', lineHeight: 21 }}>Kết nối thêm bạn bè hoặc viết một điều nhỏ để bắt đầu.</Text></View>} ListFooterComponent={query.error ? <ErrorNotice error={query.error} retry={() => void query.refetch()} /> : query.hasNextPage ? <View style={{ paddingHorizontal: 16 }}><PostAction title={query.isFetchingNextPage ? 'Đang tải…' : 'Xem thêm bài viết'} icon="arrow" disabled={query.isFetchingNextPage} onPress={() => void query.fetchNextPage()} /></View> : <Text style={{ color: t.subtle, textAlign: 'center', paddingVertical: 20, fontSize: 12 }}>Bạn đã chạm đáy của dòng thời gian ✦</Text>} refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />;
}

const feedStyles = StyleSheet.create({
  list: { paddingBottom: 18 },
  headerWrap: { paddingTop: 12, paddingBottom: 12, gap: 14 },
  topBar: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: 16 },
  logo: { fontSize: 26, fontWeight: '900', letterSpacing: -1.1 },
  composer: { padding: 14, gap: 12, borderRadius: 0 },
  composerTop: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  composerPrompt: { flex: 1, minHeight: 43, justifyContent: 'center', paddingHorizontal: 14, borderRadius: 22 },
  divider: { height: 1 },
  composerActions: { flexDirection: 'row', justifyContent: 'space-between', gap: 4 },
  sectionTitle: { flexDirection: 'row', alignItems: 'flex-end', justifyContent: 'space-between', paddingHorizontal: 16 },
  storyRail: { gap: 10, paddingHorizontal: 16 },
  storyCard: { width: 108, height: 148, borderRadius: 16, borderWidth: 1, padding: 10, justifyContent: 'flex-end', overflow: 'hidden' },
  storyOrb: { position: 'absolute', width: 60, height: 60, borderRadius: 40, top: 14, left: 24, alignItems: 'center', justifyContent: 'center' },
  storyPattern: { position: 'absolute', top: 16, left: 19, width: 68, height: 68, borderRadius: 35, backgroundColor: '#ffffff2b', alignItems: 'center', justifyContent: 'center' },
  storyLabel: { fontSize: 13, fontWeight: '800', color: '#fff' },
  storyHint: { fontSize: 11, color: '#ffffffb8', marginTop: 2 },
  modeTabs: { flexDirection: 'row', borderRadius: 14, borderWidth: 1, padding: 4, gap: 4, marginHorizontal: 16 },
  modeTab: { flex: 1, minHeight: 40, borderRadius: 11, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6 },
  postWrap: { marginBottom: 10, gap: 6 },
  shareLine: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 16 },
  empty: { alignItems: 'center', gap: 8, paddingHorizontal: 28, paddingVertical: 48 },
});
