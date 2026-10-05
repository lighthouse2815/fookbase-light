import { useEffect, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router, type Href } from 'expo-router';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { searchApi, type GlobalSearchResult, type SearchType } from '../../api/search';
import { resolveProfileImageUrl } from '../../api/users';
import { AppHeader, Avatar, Button, Card, ErrorNotice, Field, Label, Loading, Screen, useTheme } from '../../components/ui';

const typeLabels: Record<SearchType, string> = {
  all: 'Tất cả', people: 'Mọi người', groups: 'Nhóm', pages: 'Trang', posts: 'Bài viết', reels: 'Reels', events: 'Sự kiện',
};
type ResultType = Exclude<SearchType, 'all'> | 'hashtags';
type ResultRow = { key: string; type: ResultType; title: string; subtitle: string; action: string; path?: Href; tag?: string; avatar?: string | null };

function resultRows(pages: GlobalSearchResult[], type: SearchType): ResultRow[] {
  const rows: ResultRow[] = [];
  for (const page of pages) {
    if (type === 'all' || type === 'people') for (const person of page.people) rows.push({ key: `people:${person.userId}`, type: 'people', title: person.displayName, subtitle: `@${person.username} · ${person.followerCount} người theo dõi`, avatar: person.avatarUrl, action: `Xem hồ sơ ${person.displayName}`, path: `/profile/${person.userId}` });
    if (type === 'all' || type === 'groups') for (const group of page.groups) rows.push({ key: `groups:${group.groupId}`, type: 'groups', title: group.name, subtitle: `${group.memberCount} thành viên · ${group.privacy === 'private' ? 'Riêng tư' : 'Công khai'}`, action: `Xem nhóm ${group.name}`, path: `/groups/${group.groupId}` });
    if (type === 'all' || type === 'pages') for (const item of page.pages) rows.push({ key: `pages:${item.pageId}`, type: 'pages', title: item.name, subtitle: `${item.category} · ${item.followerCount} người theo dõi`, avatar: item.avatarUrl, action: `Khám phá Trang ${item.name}`, path: `/pages?pageId=${encodeURIComponent(item.pageId)}` });
    if (type === 'all' || type === 'posts') for (const post of page.posts) rows.push({ key: `posts:${post.postId}`, type: 'posts', title: post.snippet || 'Bài viết có nội dung đa phương tiện', subtitle: `${post.displayAuthor?.name ?? 'Bài viết'} · ${post.commentCount} bình luận`, action: `Xem bài viết ${post.snippet || 'có nội dung đa phương tiện'}`, path: `/posts/${post.postId}` });
    if (type === 'all' || type === 'reels') for (const reel of page.reels) rows.push({ key: `reels:${reel.reelId}`, type: 'reels', title: reel.snippet || 'Video Reels', subtitle: `${reel.author.displayName} · ${reel.viewCount} lượt xem`, action: `Xem Reels ${reel.snippet || 'Video Reels'}`, path: `/reels?reelId=${encodeURIComponent(reel.reelId)}` });
    if (type === 'all' || type === 'events') for (const event of page.events ?? []) rows.push({ key: `events:${event.eventId}`, type: 'events', title: event.name, subtitle: `${new Date(event.startsAtUtc).toLocaleString('vi-VN')} · ${event.locationName ?? 'Trực tuyến'}`, action: `Khám phá sự kiện ${event.name}`, path: `/events?eventId=${encodeURIComponent(event.eventId)}` });
    if (type === 'all') for (const hashtag of page.hashtags ?? []) rows.push({ key: `hashtags:${hashtag.tag}`, type: 'hashtags', title: `#${hashtag.displayName}`, subtitle: 'Chủ đề', action: `Xem bài viết về #${hashtag.displayName}`, tag: hashtag.tag });
  }
  return Array.from(new Map(rows.map(row => [row.key, row])).values());
}

export function SearchScreen({ accountId }: { accountId?: string }) {
  const theme = useTheme();
  const [text, setText] = useState('');
  const [term, setTerm] = useState('');
  const [type, setType] = useState<SearchType>('all');
  const [debouncedText, setDebouncedText] = useState('');
  const draft = text.trim();
  const showResults = !!accountId && term.length >= 2 && draft === term;
  const showSuggestions = !!accountId && draft.length >= 2 && draft !== term;
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedText(draft), 300);
    return () => clearTimeout(timer);
  }, [draft]);
  const query = useInfiniteQuery({
    queryKey: ['global-search', accountId, term, type],
    enabled: showResults,
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam, signal }) => searchApi.search(term, type, pageParam, 20, { signal }),
    getNextPageParam: (lastPage, _pages, _lastParam, pageParams) => type !== 'all' && lastPage.nextCursor && !pageParams.includes(lastPage.nextCursor) ? lastPage.nextCursor : undefined,
  });
  const suggestions = useQuery({
    queryKey: ['search-suggestions', accountId, debouncedText],
    enabled: showSuggestions && debouncedText === draft,
    queryFn: ({ signal }) => searchApi.suggestions(debouncedText, 5, { signal }),
  });
  const suggestionReady = showSuggestions && debouncedText === draft;
  const rows = showResults ? resultRows(query.data?.pages ?? [], type) : suggestionReady && suggestions.data ? resultRows([{ ...suggestions.data, posts: [], reels: [], nextCursor: null }], type) : [];
  const submit = () => {
    if (!accountId || draft.length < 2) return;
    if (draft === term) void query.refetch();
    else setTerm(draft);
  };
  const open = (row: ResultRow) => {
    if (row.tag) { const hashtag = `#${row.tag}`; setText(hashtag); setTerm(hashtag); setType('posts'); }
    else if (row.path) router.push(row.path);
  };
  const loading = showResults ? query.isPending || query.isFetching : showSuggestions && (!suggestionReady || suggestions.isPending || suggestions.isFetching);
  const error = showResults ? query.error : suggestionReady ? suggestions.error : null;
  const retry = () => {
    if (showResults) {
      if (query.isFetchNextPageError) void query.fetchNextPage();
      else void query.refetch();
    } else void suggestions.refetch();
  };

  return <Screen>
    <AppHeader title="Tìm kiếm" subtitle="Tìm người, nhóm, Trang và nội dung trên Fookbase" />
    <Field label="Từ khóa tìm kiếm" placeholder="Nhập tên, nội dung hoặc #chủ đề" value={text} onChangeText={value => { setText(value); if (!value.trim()) setTerm(''); }} returnKeyType="search" onSubmitEditing={submit} />
    <Button title="Tìm kiếm" disabled={!accountId || draft.length < 2} onPress={submit} />
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={searchStyles.filters}>
      {(Object.keys(typeLabels) as SearchType[]).map(value => <Pressable key={value} accessibilityRole="button" accessibilityLabel={typeLabels[value]} accessibilityState={{ selected: value === type }} onPress={() => setType(value)} style={[searchStyles.filter, { backgroundColor: value === type ? theme.primarySoft : theme.surface2, borderColor: value === type ? theme.primary : theme.border }]}><Text style={{ color: value === type ? theme.primary : theme.muted, fontWeight: '700' }}>{typeLabels[value]}</Text></Pressable>)}
    </ScrollView>
    {!accountId ? <Label>Đăng nhập để tìm kiếm.</Label> : draft.length < 2 ? <Label muted>Nhập ít nhất 2 ký tự để tìm kiếm.</Label> : showSuggestions && <Label muted>Gợi ý nhanh · Nhấn Tìm kiếm để xem đầy đủ.</Label>}
    {showResults && <Label muted>Kết quả cho “{term}”{type === 'all' ? ' · Chọn một loại để xem đầy đủ.' : ''}</Label>}
    {loading && <Loading />}
    {error && <ErrorNotice error={error} retry={retry} />}
    {rows.map(row => <Card key={row.key}>
      <Label muted style={searchStyles.kind}>{row.type === 'hashtags' ? 'Hashtag' : typeLabels[row.type]}</Label>
      <View style={searchStyles.resultHeader}>
        {(row.type === 'people' || row.type === 'pages') && <Avatar label={row.title} uri={row.avatar ? resolveProfileImageUrl(row.avatar) : null} />}
        <View style={searchStyles.copy}><Label style={searchStyles.title} numberOfLines={3}>{row.title}</Label><Label muted numberOfLines={2}>{row.subtitle}</Label></View>
      </View>
      <Button secondary title={row.action} onPress={() => open(row)} />
    </Card>)}
    {showResults && query.isSuccess && !query.isFetching && !error && rows.length === 0 && <Card><Label>Không tìm thấy kết quả.</Label><Label muted>Thử từ khóa hoặc loại kết quả khác.</Label></Card>}
    {showResults && query.hasNextPage && <Button title={query.isFetchingNextPage ? 'Đang tải thêm…' : 'Xem thêm'} disabled={query.isFetching} onPress={() => void query.fetchNextPage()} />}
  </Screen>;
}

const searchStyles = StyleSheet.create({
  filters: { gap: 8, paddingVertical: 4 },
  filter: { minHeight: 42, paddingHorizontal: 13, justifyContent: 'center', borderRadius: 20, borderWidth: 1 },
  kind: { fontSize: 12, fontWeight: '700' },
  resultHeader: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  copy: { flex: 1, gap: 3 },
  title: { fontWeight: '800' },
});
