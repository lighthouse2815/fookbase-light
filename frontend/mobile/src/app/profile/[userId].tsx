import { useState } from 'react';
import { Alert, View } from 'react-native';
import { useLocalSearchParams, router } from 'expo-router';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { usersApi } from '../../api/users';
import { friendsApi } from '../../api/friends';
import { postsApi } from '../../api/posts';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Field, Label, Loading, Screen, styles } from '../../components/ui';
import { PostCard } from '../../components/PostCard';
import { FriendActions } from '../../components/FriendActions';
export default function Profile() {
  const { userId } = useLocalSearchParams<{ userId: string }>(); const { session } = useAuth(); const own = session?.user.id === userId; const cache = useQueryClient();
  const [editing, setEditing] = useState(false); const [name, setName] = useState(''); const [bio, setBio] = useState('');
  const user = useQuery({ queryKey: ['user', session?.user.id, userId], queryFn: () => usersApi.getById(userId) });
  const status = useQuery({ queryKey: ['relationship', session?.user.id, userId], queryFn: () => friendsApi.getStatus(userId), enabled: !own });
  const posts = useInfiniteQuery({ queryKey: ['user-posts', session?.user.id, userId], initialPageParam: 0, queryFn: ({ pageParam }) => postsApi.getByUser(userId, pageParam), getNextPageParam: p => p.offset + p.items.length < p.total ? p.offset + p.items.length : undefined });
  const action = useMutation({ mutationFn: (fn: () => Promise<unknown>) => fn(), onSuccess: () => cache.invalidateQueries() });
  if (!user.data) return <Screen>{user.error ? <ErrorNotice error={user.error} retry={() => void user.refetch()} /> : <Loading />}</Screen>;
  return <Screen><Card><Label title>{user.data.displayName}</Label><Label muted>@{user.data.username}</Label><Label>{user.data.bio}</Label><Label muted>{user.data.followerCount} người theo dõi · {user.data.followingCount} đang theo dõi</Label>
    {own ? <><Button secondary title="Chỉnh sửa hồ sơ" onPress={() => { setEditing(!editing); setName(user.data.displayName); setBio(user.data.bio ?? ''); }} />{editing && <><Field label="Tên hiển thị" value={name} onChangeText={setName} /><Field label="Giới thiệu" multiline value={bio} onChangeText={setBio} /><Button title="Lưu hồ sơ" disabled={action.isPending || !name.trim()} onPress={() => action.mutate(() => usersApi.updateCurrent({ displayName: name, bio }))} /></>}</> : <View style={styles.row}>
    {status.data?.status !== 'blocked' && <Button title={user.data.isFollowing ? 'Bỏ theo dõi' : 'Theo dõi'} disabled={action.isPending || status.isPending} onPress={() => action.mutate(() => user.data.isFollowing ? usersApi.unfollow(userId) : usersApi.follow(userId))} />}
    {status.data && <FriendActions userId={userId} relationship={status.data} pending={action.isPending} run={fn => action.mutate(fn)} />}
    {status.data?.status !== 'blocked' && <Button secondary title="Chặn người dùng" disabled={action.isPending} onPress={() => Alert.alert('Chặn tài khoản?', 'Bạn sẽ không còn tương tác với tài khoản này.', [{ text: 'Hủy', style: 'cancel' }, { text: 'Chặn', style: 'destructive', onPress: () => action.mutate(() => friendsApi.block(userId)) }])} />}
    </View>}
    {status.error && <ErrorNotice error={status.error} retry={() => void status.refetch()} />}{action.error && <ErrorNotice error={action.error} />}
  </Card>{own && <Button title="Viết bài" onPress={() => router.push('/posts/create')} />}
  {posts.data?.pages.flatMap(p => p.items).map(p => <PostCard key={p.id} post={p} />)}{posts.error && <ErrorNotice error={posts.error} retry={() => void posts.refetch()} />}{posts.hasNextPage && <Button secondary title="Thêm bài viết" disabled={posts.isFetchingNextPage} onPress={() => void posts.fetchNextPage()} />}</Screen>;
}
