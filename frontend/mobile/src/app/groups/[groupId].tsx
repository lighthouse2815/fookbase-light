import { router, useLocalSearchParams } from 'expo-router';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { groupsApi } from '../../api/groups';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Label, Loading, Screen } from '../../components/ui';
import { PostCard } from '../../components/PostCard';
import { UserCard } from '../../components/UserCard';
export default function GroupDetail() {
  const { groupId } = useLocalSearchParams<{ groupId: string }>(); const { session } = useAuth(); const cache = useQueryClient();
  const group = useQuery({ queryKey: ['group', session?.user.id, groupId], queryFn: () => groupsApi.get(groupId) });
  const canManage = ['owner', 'admin', 'moderator'].includes(group.data?.viewerRole ?? '');
  const requests = useInfiniteQuery({ queryKey: ['group-requests', session?.user.id, groupId], enabled: canManage, initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => groupsApi.getJoinRequests(groupId, pageParam), getNextPageParam: p => p.nextCursor ?? undefined });
  const posts = useInfiniteQuery({ queryKey: ['group-posts', session?.user.id, groupId], enabled: !!group.data && (group.data.privacy === 'public' || !!group.data.viewerRole), initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => groupsApi.getPosts(groupId, pageParam), getNextPageParam: p => p.nextCursor ?? undefined });
  const action = useMutation({ mutationFn: (fn: () => Promise<unknown>) => fn(), onSuccess: () => cache.invalidateQueries() });
  if (!group.data) return <Screen>{group.error ? <ErrorNotice error={group.error} retry={() => void group.refetch()} /> : <Loading />}</Screen>;
  return <Screen><Card><Label title>{group.data.name}</Label><Label>{group.data.description}</Label><Label muted>{group.data.memberCount} thành viên</Label>
    {group.data.viewerRole ? <><Button title="Viết bài trong nhóm" onPress={() => router.push({ pathname: '/posts/create', params: { groupId } })} />{group.data.viewerRole !== 'owner' && <Button secondary title="Rời nhóm" disabled={action.isPending} onPress={() => action.mutate(() => groupsApi.leave(groupId))} />}</> : <Button title="Yêu cầu tham gia" disabled={action.isPending} onPress={() => action.mutate(() => groupsApi.join(groupId))} />}
    {action.isSuccess && <Label>Đã cập nhật yêu cầu.</Label>}{action.error && <ErrorNotice error={action.error} />}</Card>
    {canManage && <><Label title>Yêu cầu tham gia</Label>{requests.data?.pages.flatMap(p => p.items).map(r => <UserCard key={r.id} userId={r.requesterUserId}><Button title="Duyệt" disabled={action.isPending} onPress={() => action.mutate(() => groupsApi.approveJoinRequest(groupId, r.id))} /><Button secondary title="Từ chối" disabled={action.isPending} onPress={() => action.mutate(() => groupsApi.declineJoinRequest(groupId, r.id))} /></UserCard>)}{requests.error && <ErrorNotice error={requests.error} />}{requests.hasNextPage && <Button title="Thêm yêu cầu" onPress={() => void requests.fetchNextPage()} />}</>}
    {posts.data?.pages.flatMap(p => p.items).map(p => <PostCard key={p.id} post={p} />)}{posts.error && <ErrorNotice error={posts.error} retry={() => void posts.refetch()} />}{posts.hasNextPage && <Button secondary title="Thêm bài viết" disabled={posts.isFetchingNextPage} onPress={() => void posts.fetchNextPage()} />}
  </Screen>;
}
