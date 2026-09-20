import { router } from 'expo-router';
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { notificationsApi, type AppNotification } from '../../api/notifications';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Label, Loading, Screen } from '../../components/ui';
const labels: Record<string, string> = { FriendRequestReceived: 'đã gửi lời mời kết bạn', FriendRequestAccepted: 'đã chấp nhận kết bạn', UserFollowed: 'đã theo dõi bạn', PostReaction: 'đã bày tỏ cảm xúc với bài viết', PostComment: 'đã bình luận', CommentReaction: 'đã bày tỏ cảm xúc với bình luận', PostShared: 'đã chia sẻ bài viết', PostMention: 'đã nhắc đến bạn', CommentMention: 'đã nhắc đến bạn trong bình luận', GroupInvite: 'đã mời bạn vào nhóm', GroupJoinApproved: 'đã duyệt yêu cầu vào nhóm', StoryReaction: 'đã phản hồi tin của bạn' };
export function notificationPath(n: AppNotification): string | null {
  if (!n.entityId) return null;
  switch (n.entityType) {
    case 'Post': return `/posts/${n.entityId}`;
    case 'Comment': return n.parentEntityId ? `/posts/${n.parentEntityId}` : null;
    case 'Group': return `/groups/${n.entityId}`;
    case 'GroupInvite': case 'GroupJoinRequest': return n.parentEntityId ? `/groups/${n.parentEntityId}` : '/groups';
    case 'FriendRequest': return '/friends';
    case 'UserFollow': return n.actorUserId ? `/profile/${n.actorUserId}` : null;
    case 'Story': return `/stories/${n.entityId}`;
    default: return null;
  }
}
export default function Notifications() {
  const { session } = useAuth(); const cache = useQueryClient();
  const query = useInfiniteQuery({ queryKey: ['notifications', session?.user.id], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => notificationsApi.getPage(pageParam), getNextPageParam: p => p.nextCursor ?? undefined });
  const read = useMutation({ mutationFn: (id?: string) => id ? notificationsApi.markRead(id) : notificationsApi.markAllRead(), onSuccess: () => cache.invalidateQueries() });
  return <Screen><Label title>Thông báo</Label><Button secondary title="Đánh dấu tất cả đã đọc" disabled={read.isPending} onPress={() => read.mutate(undefined)} />{query.isPending && <Loading />}
    {query.data?.pages.flatMap(p => p.items).map(n => <Card key={n.id}><Label>{n.isRead ? '' : '● '}{n.actorDisplayName ?? 'Fookbase'} {labels[n.type] ?? 'có cập nhật mới cho bạn'}</Label><Label muted>{new Date(n.createdAtUtc).toLocaleString('vi-VN')}</Label>{notificationPath(n) ? <Button secondary title="Xem nội dung" onPress={() => { read.mutate(n.id); router.push(notificationPath(n)!); }} /> : <Label muted>Nội dung này hiện có trên website Fookbase Light.</Label>}{!n.isRead && <Button secondary title="Đánh dấu đã đọc" onPress={() => read.mutate(n.id)} disabled={read.isPending} />}</Card>)}
    {query.error && <ErrorNotice error={query.error} retry={() => void query.refetch()} />}{read.error && <ErrorNotice error={read.error} />}{query.hasNextPage && <Button title="Xem thêm" onPress={() => void query.fetchNextPage()} disabled={query.isFetchingNextPage} />}
  </Screen>;
}
