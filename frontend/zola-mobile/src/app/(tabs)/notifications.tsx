import { router } from 'expo-router';
import { Pressable } from 'react-native';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { notificationsApi, type AppNotification } from '../../api/notifications';
import { messengerApi } from '../../api/messages';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Label, Loading, Screen } from '../../components/ui';
const labels: Record<string, string> = { FriendRequestReceived: 'đã gửi lời mời kết bạn', FriendRequestAccepted: 'đã chấp nhận kết bạn', UserFollowed: 'đã theo dõi bạn', PostReaction: 'đã bày tỏ cảm xúc với bài viết', PostComment: 'đã bình luận', CommentReaction: 'đã bày tỏ cảm xúc với bình luận', PostShared: 'đã chia sẻ bài viết', PostMention: 'đã nhắc đến bạn', CommentMention: 'đã nhắc đến bạn trong bình luận', GroupInvite: 'đã mời bạn vào nhóm', GroupJoinApproved: 'đã duyệt yêu cầu vào nhóm', StoryReaction: 'đã phản hồi tin của bạn' };
export default function Notifications() {
  const { session } = useAuth(); const cache = useQueryClient();
  const query = useInfiniteQuery({ queryKey: ['notifications', session?.user.id], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => notificationsApi.getPage(pageParam), getNextPageParam: p => p.nextCursor ?? undefined });
  const messages = useQuery({ queryKey: ['message-notifications', session?.user.id], queryFn: () => messengerApi.notifications(), staleTime: 15_000 });
  const read = useMutation({ mutationFn: (id?: string) => id ? notificationsApi.markRead(id) : notificationsApi.markAllRead(), onSuccess: () => cache.invalidateQueries() });
  return <Screen><Label title>Thông báo</Label><Button secondary title="Đánh dấu tất cả đã đọc" disabled={read.isPending} onPress={() => read.mutate(undefined)} />{query.isPending && <Loading />}
    {messages.data?.items.map(item => <Pressable key={`message-${item.message.id}`} accessibilityRole="button" accessibilityLabel={`Mở cuộc trò chuyện ${item.conversation.title ?? 'Zola'}`} onPress={() => router.push(`/conversations/${item.conversation.id}`)}><Card><Label>{item.conversation.title ?? 'Cuộc trò chuyện mới'}</Label><Label>{item.message.content ?? 'Đã gửi một tệp đính kèm.'}</Label><Label muted>{new Date(item.message.createdAtUtc).toLocaleString('vi-VN')}</Label></Card></Pressable>)}
    {query.data?.pages.flatMap(p => p.items).map(n => <NotificationCard key={n.id} notification={n} onRead={() => read.mutate(n.id)} disabled={read.isPending} />)}
    {!query.isPending && !messages.isPending && !query.error && !messages.error && !query.data?.pages.some(page => page.items.length > 0) && !messages.data?.items.length && <Label muted>Bạn chưa có thông báo mới.</Label>}
    {query.error && <ErrorNotice error={query.error} retry={() => void query.refetch()} />}{messages.error && <ErrorNotice error={messages.error} retry={() => void messages.refetch()} />}{read.error && <ErrorNotice error={read.error} />}{query.hasNextPage && <Button title="Xem thêm" onPress={() => void query.fetchNextPage()} disabled={query.isFetchingNextPage} />}
  </Screen>;
}

function NotificationCard({ notification, onRead, disabled }: { notification: AppNotification; onRead: () => void; disabled: boolean }) {
  return <Card><Label>{notification.isRead ? '' : '● '}{notification.actorDisplayName ?? 'Zola'} {labels[notification.type] ?? 'có cập nhật mới cho bạn'}</Label><Label muted>{new Date(notification.createdAtUtc).toLocaleString('vi-VN')}</Label>{!notification.isRead && <Button secondary title="Đánh dấu đã đọc" onPress={onRead} disabled={disabled} />}</Card>;
}
