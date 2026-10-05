import { useRef, useState } from 'react'
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native'
import { Image } from 'expo-image'
import { useLocalSearchParams } from 'expo-router'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { pagesApi, type Page, type PageInvitation } from '../../api/pages'
import { useAuth } from '../../auth/AuthProvider'
import { Avatar, Button, Card, ErrorNotice, Field, Icon, Label, Loading, Screen, styles, useTheme } from '../../components/ui'

type PageMode = 'discover' | 'mine' | 'following'

const modeLabels: Record<PageMode, string> = {
  discover: 'Khám phá',
  mine: 'Trang của bạn',
  following: 'Đang theo dõi',
}

function PageCard({ page, pending, onToggleFollow }: { page: Page; pending: boolean; onToggleFollow: () => void }) {
  const theme = useTheme()
  return <Card style={pageStyles.pageCard}>
    {page.coverUrl
      ? <Image source={{ uri: page.coverUrl }} contentFit="cover" style={pageStyles.cover} />
      : <View style={[pageStyles.cover, { backgroundColor: `${theme.primary}20` }]}><Icon name="people" color={theme.primary} size={34} /></View>}
    <View style={pageStyles.pageBody}>
      <Avatar label={page.name} uri={page.avatarUrl} size={54} />
      <View style={pageStyles.pageCopy}>
        <Label style={pageStyles.pageName} numberOfLines={1}>{page.name}</Label>
        <Text style={{ color: theme.muted, fontSize: 12 }} numberOfLines={1}>{page.category} · {page.followerCount.toLocaleString('vi-VN')} người theo dõi</Text>
      </View>
    </View>
    {page.bio && <Text style={[pageStyles.bio, { color: theme.muted }]} numberOfLines={2}>{page.bio}</Text>}
    <View style={styles.row}>
      <Button title={page.isFollowing ? 'Đang theo dõi' : 'Theo dõi'} secondary={page.isFollowing} disabled={pending} onPress={onToggleFollow} />
      {page.viewerRole && <View style={[pageStyles.role, { backgroundColor: `${theme.success}1c` }]}><Text style={{ color: theme.success, fontSize: 12, fontWeight: '800' }}>Bạn là {page.viewerRole}</Text></View>}
    </View>
  </Card>
}

function InvitationCard({ invitation, pending, onAccept, onDecline }: { invitation: PageInvitation; pending: boolean; onAccept: () => void; onDecline: () => void }) {
  const theme = useTheme()
  const page = invitation.page
  if (!page) return null
  return <Card tone="soft" style={pageStyles.invitation}>
    <Avatar label={page.name} uri={page.avatarUrl} size={44} />
    <View style={{ flex: 1, gap: 2 }}><Label style={{ fontWeight: '800' }} numberOfLines={1}>{page.name}</Label><Text style={{ color: theme.muted, fontSize: 12 }}>Bạn được mời làm {invitation.role}</Text></View>
    <Pressable accessibilityRole="button" accessibilityLabel={`Bỏ qua lời mời từ ${page.name}`} disabled={pending} onPress={onDecline}><Icon name="close" color={theme.muted} size={20} /></Pressable>
    <Button compact title="Chấp nhận" disabled={pending} onPress={onAccept} />
  </Card>
}

export default function PagesScreen() {
  const { session } = useAuth()
  const { pageId } = useLocalSearchParams<{ pageId?: string }>()
  return <PagesScreenContent key={`${session?.user.id}:${pageId}`} />
}

function PagesScreenContent() {
  const { session } = useAuth()
  const cache = useQueryClient()
  const { pageId } = useLocalSearchParams<{ pageId?: string }>()
  const scrollRef = useRef<ScrollView>(null)
  const scrolled = useRef(false)
  const selected = useQuery({
    queryKey: ['pages', session?.user.id, 'selected', pageId],
    enabled: !!session && !!pageId,
    queryFn: () => pagesApi.get(pageId!),
  })
  const [mode, setMode] = useState<PageMode>('discover')
  const [input, setInput] = useState('')
  const [search, setSearch] = useState('')
  const pages = useInfiniteQuery({
    queryKey: ['pages', session?.user.id, mode, search],
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam, signal }) => mode === 'discover'
      ? pagesApi.discover(search, pageParam, { signal })
      : mode === 'mine' ? pagesApi.mine(pageParam, { signal }) : pagesApi.following(pageParam, { signal }),
    getNextPageParam: page => page.nextCursor ?? undefined,
  })
  const invitations = useInfiniteQuery({
    queryKey: ['page-invitations', session?.user.id], initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam, signal }) => pagesApi.invitationsMine(pageParam, { signal }), getNextPageParam: page => page.nextCursor ?? undefined,
  })
  const action = useMutation({ mutationFn: (work: () => Promise<unknown>) => work(), onSuccess: () => cache.invalidateQueries({ queryKey: ['pages'] }) })
  const inviteAction = useMutation({ mutationFn: (work: () => Promise<unknown>) => work(), onSuccess: () => cache.invalidateQueries({ queryKey: ['page-invitations'] }) })
  const items = pages.data?.pages.flatMap(page => page.items) ?? []
  const pendingInvitations = invitations.data?.pages.flatMap(page => page.items) ?? []
  const theme = useTheme()

  return <Screen scrollRef={scrollRef}>
    <View style={pageStyles.hero}><View style={[pageStyles.heroIcon, { backgroundColor: `${theme.primary}1c` }]}><Icon name="people" color={theme.primary} size={28} /></View><View style={{ flex: 1 }}><Label title style={{ fontSize: 26 }}>Trang</Label><Text style={{ color: theme.muted, fontSize: 13 }}>Theo dõi những thương hiệu và cộng đồng bạn yêu thích.</Text></View></View>
    {pendingInvitations.length > 0 && <View style={{ gap: 8 }}><Label style={{ fontWeight: '800' }}>Lời mời quản lý</Label>{pendingInvitations.map(invitation => <InvitationCard key={invitation.id} invitation={invitation} pending={inviteAction.isPending} onAccept={() => inviteAction.mutate(() => pagesApi.acceptInvitation(invitation.id))} onDecline={() => inviteAction.mutate(() => pagesApi.declineInvitation(invitation.id))} />)}</View>}
    <View style={[pageStyles.modeTabs, { backgroundColor: theme.surface2, borderColor: theme.border }]}>{(Object.keys(modeLabels) as PageMode[]).map(value => <Pressable key={value} accessibilityRole="button" accessibilityState={{ selected: mode === value }} onPress={() => setMode(value)} style={[pageStyles.modeTab, mode === value && { backgroundColor: theme.card }]}><Text style={{ color: mode === value ? theme.primary : theme.muted, fontWeight: '800', fontSize: 12 }}>{modeLabels[value]}</Text></Pressable>)}</View>
    {mode === 'discover' && <View style={pageStyles.searchRow}><View style={{ flex: 1 }}><Field label="Tìm trang" value={input} onChangeText={setInput} placeholder="Tên trang hoặc chủ đề" returnKeyType="search" onSubmitEditing={() => setSearch(input)} /></View><Button compact title="Tìm" onPress={() => setSearch(input)} /></View>}
    {pageId && selected.isPending && <Loading />}
    {selected.error && <ErrorNotice error={selected.error} retry={() => void selected.refetch()} />}
    {selected.data && <View accessibilityLabel="Trang đã chọn từ tìm kiếm" onLayout={event => { if (!scrolled.current) { scrolled.current = true; scrollRef.current?.scrollTo({ y: event.nativeEvent.layout.y, animated: true }); } }} style={{ borderWidth: 2, borderColor: theme.primary, borderRadius: 16, padding: 4, gap: 8 }}>
      <Label style={{ color: theme.primary, fontWeight: '800' }}>Trang đã chọn từ tìm kiếm</Label>
      <PageCard page={selected.data} pending={action.isPending} onToggleFollow={() => action.mutate(() => selected.data!.isFollowing ? pagesApi.unfollow(selected.data!.id) : pagesApi.follow(selected.data!.id))} />
    </View>}
    {pages.isPending && <Loading />}
    {items.filter(item => item.id !== selected.data?.id).map(page => <PageCard key={page.id} page={page} pending={action.isPending} onToggleFollow={() => action.mutate(() => page.isFollowing ? pagesApi.unfollow(page.id) : pagesApi.follow(page.id))} />)}
    {!pages.isPending && !pages.error && items.length === 0 && !selected.data && !pageId && <Card tone="soft" style={pageStyles.empty}><Icon name="sparkle" color={theme.accent} size={24} /><Label style={{ fontWeight: '800' }}>{mode === 'mine' ? 'Bạn chưa quản lý Trang nào' : mode === 'following' ? 'Chưa có Trang được theo dõi' : 'Chưa tìm thấy Trang phù hợp'}</Label><Text style={{ color: theme.muted, textAlign: 'center', fontSize: 13, lineHeight: 19 }}>{mode === 'discover' ? 'Thử một từ khóa khác để tìm những cộng đồng mới.' : 'Khám phá Trang để làm đầy không gian này.'}</Text></Card>}
    {pages.error && <ErrorNotice error={pages.error} retry={() => void pages.refetch()} />}{action.error && <ErrorNotice error={action.error} />}{inviteAction.error && <ErrorNotice error={inviteAction.error} />}
    {pages.hasNextPage && <Button secondary title={pages.isFetchingNextPage ? 'Đang tải…' : 'Xem thêm Trang'} disabled={pages.isFetchingNextPage} onPress={() => void pages.fetchNextPage()} />}
  </Screen>
}

const pageStyles = StyleSheet.create({
  hero: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  heroIcon: { width: 56, height: 56, borderRadius: 19, alignItems: 'center', justifyContent: 'center' },
  modeTabs: { flexDirection: 'row', padding: 4, gap: 4, borderRadius: 15, borderWidth: 1 },
  modeTab: { flex: 1, minHeight: 38, borderRadius: 11, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 5 },
  searchRow: { flexDirection: 'row', alignItems: 'flex-end', gap: 8 },
  pageCard: { padding: 0, overflow: 'hidden', gap: 0 },
  cover: { width: '100%', height: 92, alignItems: 'center', justifyContent: 'center' },
  pageBody: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 14, marginTop: -22 },
  pageCopy: { flex: 1, paddingTop: 22, gap: 2 },
  pageName: { fontSize: 17, fontWeight: '900', lineHeight: 21 },
  bio: { paddingHorizontal: 14, paddingTop: 11, fontSize: 13, lineHeight: 19 },
  role: { paddingHorizontal: 9, paddingVertical: 7, borderRadius: 10 },
  invitation: { padding: 11, flexDirection: 'row', alignItems: 'center', gap: 9 },
  empty: { alignItems: 'center', gap: 8, paddingVertical: 28, paddingHorizontal: 22 },
})
