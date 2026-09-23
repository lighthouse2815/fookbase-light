import { useState } from 'react';
import { View } from 'react-native';
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { friendsApi } from '../api/friends';
import { useAuth } from '../auth/AuthProvider';
import { UserCard } from '../components/UserCard';
import { AppHeader, Button, ErrorNotice, Label, Loading, Screen, styles } from '../components/ui';
export default function Friends() {
  const [mode, setMode] = useState<'friends' | 'incoming' | 'outgoing' | 'blocked'>('friends'); const { session } = useAuth(); const cache = useQueryClient();
  const query = useInfiniteQuery({ queryKey: ['friends', session?.user.id, mode], initialPageParam: 0, queryFn: async ({ pageParam }) => {
    if (mode === 'incoming' || mode === 'outgoing') { const p = await (mode === 'incoming' ? friendsApi.getIncomingRequests : friendsApi.getOutgoingRequests)(pageParam, 30); return { ...p, items: p.items.map(x => ({ userId: mode === 'incoming' ? x.senderUserId : x.receiverUserId, requestId: x.id })) }; }
    const p = await (mode === 'friends' ? friendsApi.getFriends : friendsApi.getBlockedUsers)(pageParam, 30); return { ...p, items: p.items.map(x => ({ userId: x.userId, requestId: '' })) };
  }, getNextPageParam: p => p.offset + p.items.length < p.total ? p.offset + p.items.length : undefined });
  const action = useMutation({ mutationFn: (fn: () => Promise<unknown>) => fn(), onSuccess: () => cache.invalidateQueries() });
  return <Screen><AppHeader title="Bạn bè" /><View style={styles.row}>{(['friends', 'incoming', 'outgoing', 'blocked'] as const).map((v, i) => <Button key={v} title={['Bạn bè', 'Lời mời đến', 'Đã gửi', 'Đã chặn'][i]} secondary={v !== mode} onPress={() => setMode(v)} />)}</View>{query.isPending && <Loading />}
    {query.data?.pages.flatMap(p => p.items).map(x => <UserCard key={x.userId} userId={x.userId}>{mode === 'incoming' && <Button title="Chấp nhận" disabled={action.isPending} onPress={() => action.mutate(() => friendsApi.acceptRequest(x.requestId))} />}<Button secondary disabled={action.isPending} title={{ friends: 'Hủy kết bạn', incoming: 'Từ chối', outgoing: 'Hủy lời mời', blocked: 'Bỏ chặn' }[mode]} onPress={() => action.mutate(() => mode === 'friends' ? friendsApi.unfriend(x.userId) : mode === 'incoming' ? friendsApi.declineRequest(x.requestId) : mode === 'outgoing' ? friendsApi.cancelRequest(x.requestId) : friendsApi.unblock(x.userId))} /></UserCard>)}
    {query.error && <ErrorNotice error={query.error} retry={() => void query.refetch()} />}{action.error && <ErrorNotice error={action.error} />}{query.data?.pages[0].total === 0 && <Label muted>Danh sách đang trống.</Label>}{query.hasNextPage && <Button title="Xem thêm" disabled={query.isFetchingNextPage} onPress={() => void query.fetchNextPage()} />}
  </Screen>;
}
