import { useState } from 'react'
import { Alert, StyleSheet, Text, View } from 'react-native'
import { Image } from 'expo-image'
import { useLocalSearchParams, router } from 'expo-router'
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { resolveProfileImageUrl, usersApi } from '../../api/users'
import { friendsApi } from '../../api/friends'
import { postsApi } from '../../api/posts'
import { useAuth } from '../../auth/AuthProvider'
import { Avatar, Button, Card, ErrorNotice, Field, Icon, Label, Loading, Screen, useTheme } from '../../components/ui'
import { PostCard } from '../../components/PostCard'
import { FriendActions } from '../../components/FriendActions'

function ProfileDetails({ details }: { details: { currentCity: string | null; hometown?: string | null; workplace?: string | null; education?: string | null; website?: string | null } }) {
  const theme = useTheme()
  const items = [
    details.currentCity && ['Sống tại', details.currentCity, 'people'],
    details.hometown && ['Đến từ', details.hometown, 'home'],
    details.workplace && ['Làm việc tại', details.workplace, 'people'],
    details.education && ['Đã học tại', details.education, 'people'],
    details.website && ['Trang web', details.website, 'globe'],
  ].filter(Boolean) as [string, string, 'home' | 'people' | 'globe'][]
  if (items.length === 0) return null
  return <Card tone="soft" style={profileStyles.details}><Label style={{ fontWeight: '900' }}>Giới thiệu</Label>{items.map(([prefix, value, icon]) => <View key={prefix} style={profileStyles.detailRow}><Icon name={icon} color={theme.muted} size={17} /><Text style={{ color: theme.muted, flex: 1, fontSize: 13 }}>{prefix} <Text style={{ color: theme.text, fontWeight: '700' }}>{value}</Text></Text></View>)}</Card>
}

export default function Profile() {
  const { userId } = useLocalSearchParams<{ userId: string }>()
  const { session } = useAuth()
  const own = session?.user.id === userId
  const cache = useQueryClient()
  const theme = useTheme()
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState('')
  const [bio, setBio] = useState('')
  const user = useQuery({ queryKey: ['user', session?.user.id, userId], queryFn: () => usersApi.getById(userId) })
  const status = useQuery({ queryKey: ['relationship', session?.user.id, userId], queryFn: () => friendsApi.getStatus(userId), enabled: !own })
  const posts = useInfiniteQuery({ queryKey: ['user-posts', session?.user.id, userId], initialPageParam: 0, queryFn: ({ pageParam }) => postsApi.getByUser(userId, pageParam), getNextPageParam: page => page.offset + page.items.length < page.total ? page.offset + page.items.length : undefined })
  const action = useMutation({ mutationFn: (work: () => Promise<unknown>) => work(), onSuccess: () => cache.invalidateQueries() })

  if (!user.data) return <Screen>{user.error ? <ErrorNotice error={user.error} retry={() => void user.refetch()} /> : <Loading />}</Screen>
  const profile = user.data
  const avatarUrl = profile.avatarUrl ? resolveProfileImageUrl(profile.avatarUrl) : null
  const coverUrl = profile.coverUrl ? resolveProfileImageUrl(profile.coverUrl) : null
  const postItems = posts.data?.pages.flatMap(page => page.items) ?? []

  return <Screen style={profileStyles.screen}>
    <View style={[profileStyles.coverWrap, { backgroundColor: `${theme.primary}22` }]}>{coverUrl ? <Image source={{ uri: coverUrl }} contentFit="cover" style={profileStyles.cover} /> : <><View style={[profileStyles.coverGlow, { backgroundColor: `${theme.primary}30` }]} /><Icon name="sparkle" color={`${theme.primary}99`} size={44} /></>}</View>
    <View style={profileStyles.identity}><View style={[profileStyles.avatarFrame, { borderColor: theme.card, backgroundColor: theme.card }]}><Avatar label={profile.displayName} uri={avatarUrl} size={108} /></View><View style={profileStyles.identityCopy}><Label title style={profileStyles.name} numberOfLines={2}>{profile.displayName}</Label>{profile.username && <Text style={{ color: theme.muted, fontSize: 13 }}>@{profile.username}</Text>}</View></View>
    {profile.bio && <Text style={[profileStyles.bio, { color: theme.text }]}>{profile.bio}</Text>}
    <View style={[profileStyles.stats, { borderColor: theme.border, backgroundColor: theme.card }]}><View style={profileStyles.stat}><Text style={{ color: theme.text, fontSize: 16, fontWeight: '900' }}>{profile.followerCount.toLocaleString('vi-VN')}</Text><Text style={{ color: theme.muted, fontSize: 12 }}>người theo dõi</Text></View><View style={[profileStyles.statDivider, { backgroundColor: theme.border }]} /><View style={profileStyles.stat}><Text style={{ color: theme.text, fontSize: 16, fontWeight: '900' }}>{profile.followingCount.toLocaleString('vi-VN')}</Text><Text style={{ color: theme.muted, fontSize: 12 }}>đang theo dõi</Text></View><View style={[profileStyles.statDivider, { backgroundColor: theme.border }]} /><View style={profileStyles.stat}><Text style={{ color: theme.text, fontSize: 16, fontWeight: '900' }}>{postItems.length}</Text><Text style={{ color: theme.muted, fontSize: 12 }}>bài hiển thị</Text></View></View>
    {own ? <><Button title="Chỉnh sửa hồ sơ" secondary onPress={() => { setEditing(!editing); setName(profile.displayName); setBio(profile.bio ?? '') }} />{editing && <Card style={profileStyles.editCard}><Label style={{ fontWeight: '900' }}>Chỉnh sửa thông tin cơ bản</Label><Field label="Tên hiển thị" value={name} onChangeText={setName} /><Field label="Giới thiệu" multiline value={bio} onChangeText={setBio} style={{ minHeight: 94, textAlignVertical: 'top' }} /><Button title={action.isPending ? 'Đang lưu…' : 'Lưu thay đổi'} disabled={action.isPending || !name.trim()} onPress={() => action.mutate(() => usersApi.updateCurrent({ displayName: name.trim(), bio }))} /></Card>}</> : <View style={profileStyles.actions}>{status.data?.status !== 'blocked' && <Button title={profile.isFollowing ? 'Đang theo dõi' : 'Theo dõi'} secondary={profile.isFollowing === true} disabled={action.isPending || status.isPending} onPress={() => action.mutate(() => profile.isFollowing ? usersApi.unfollow(userId) : usersApi.follow(userId))} />}{status.data && <FriendActions userId={userId} relationship={status.data} pending={action.isPending} run={work => action.mutate(work)} />}{status.data?.status !== 'blocked' && <Button secondary danger title="Chặn" disabled={action.isPending} onPress={() => Alert.alert('Chặn tài khoản?', 'Bạn sẽ không còn tương tác với tài khoản này.', [{ text: 'Hủy', style: 'cancel' }, { text: 'Chặn', style: 'destructive', onPress: () => action.mutate(() => friendsApi.block(userId)) }])} />}</View>}
    {status.error && <ErrorNotice error={status.error} retry={() => void status.refetch()} />}{action.error && <ErrorNotice error={action.error} />}
    <ProfileDetails details={profile} />
    <View style={[profileStyles.profileTabs, { borderBottomColor: theme.border }]}><Text style={[profileStyles.profileTab, { color: theme.primary, borderBottomColor: theme.primary }]}>Bài viết</Text><Text style={[profileStyles.profileTab, { color: theme.muted, borderBottomColor: 'transparent' }]}>Giới thiệu</Text><Text style={[profileStyles.profileTab, { color: theme.muted, borderBottomColor: 'transparent' }]}>Ảnh</Text></View>
    <View style={profileStyles.postHeading}><View><Label style={{ fontWeight: '900', fontSize: 19 }}>Bài viết</Label><Text style={{ color: theme.muted, fontSize: 12 }}>Những điều {own ? 'bạn' : profile.displayName} đã chia sẻ</Text></View>{own && <Button compact title="Viết bài" onPress={() => router.push('/posts/create')} />}</View>
    {posts.isPending && <Loading />}{postItems.map(post => <PostCard key={post.id} post={post} />)}{!posts.isPending && !posts.error && postItems.length === 0 && <Card tone="soft" style={profileStyles.empty}><Icon name="sparkle" color={theme.accent} size={23} /><Label style={{ fontWeight: '800' }}>Chưa có bài viết để hiển thị</Label><Text style={{ color: theme.muted, fontSize: 13, textAlign: 'center' }}>{own ? 'Hãy chia sẻ một điều nhỏ để bắt đầu dòng thời gian của bạn.' : 'Khi có bài viết công khai, chúng sẽ xuất hiện tại đây.'}</Text></Card>}{posts.error && <ErrorNotice error={posts.error} retry={() => void posts.refetch()} />}{posts.hasNextPage && <Button secondary title={posts.isFetchingNextPage ? 'Đang tải…' : 'Xem thêm bài viết'} disabled={posts.isFetchingNextPage} onPress={() => void posts.fetchNextPage()} />}
  </Screen>
}

const profileStyles = StyleSheet.create({
  screen: { gap: 14 },
  coverWrap: { height: 194, marginHorizontal: -16, marginTop: -12, overflow: 'hidden', alignItems: 'center', justifyContent: 'center' },
  cover: { width: '100%', height: '100%' },
  coverGlow: { position: 'absolute', width: 230, height: 230, borderRadius: 115, right: -62, top: -93 },
  identity: { alignItems: 'center', gap: 7, marginTop: -58, paddingHorizontal: 4 },
  avatarFrame: { padding: 3, borderWidth: 3, borderRadius: 62 },
  identityCopy: { alignItems: 'center', gap: 2 },
  name: { fontSize: 25, lineHeight: 30, textAlign: 'center' },
  bio: { paddingHorizontal: 12, fontSize: 14, lineHeight: 21, textAlign: 'center' },
  stats: { minHeight: 62, flexDirection: 'row', alignItems: 'center', borderWidth: 1, borderRadius: 10, paddingHorizontal: 7 },
  stat: { flex: 1, alignItems: 'center', gap: 2 },
  statDivider: { height: 30, width: 1 },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  editCard: { gap: 12 },
  details: { gap: 10 },
  detailRow: { flexDirection: 'row', alignItems: 'center', gap: 9 },
  profileTabs: { flexDirection: 'row', borderBottomWidth: 1, marginHorizontal: -4 },
  profileTab: { flex: 1, minHeight: 42, paddingTop: 12, textAlign: 'center', fontSize: 14, fontWeight: '800', borderBottomWidth: 3 },
  postHeading: { flexDirection: 'row', alignItems: 'flex-end', justifyContent: 'space-between', marginTop: 4 },
  empty: { alignItems: 'center', gap: 7, paddingVertical: 26, paddingHorizontal: 20 },
})
