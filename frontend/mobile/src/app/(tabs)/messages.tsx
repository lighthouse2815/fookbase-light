import { router } from 'expo-router';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { messengerApi } from '../../api/messages';
import { usersApi } from '../../api/users';
import { useAuth } from '../../auth/AuthProvider';
import { AppHeader, Button, Card, ErrorNotice, IconButton, Label, Loading, Screen } from '../../components/ui';
function ConversationName({ userId, title }: { userId: string | null; title: string | null }) {
  const { session } = useAuth();
  const user = useQuery({ queryKey: ['user', session?.user.id, userId], queryFn: () => usersApi.getById(userId!), enabled: !!userId });
  return <Label title>{title ?? user.data?.displayName ?? 'Cuộc trò chuyện'}</Label>;
}
export default function Messages() {
  const { session } = useAuth();
  const query = useInfiniteQuery({ queryKey: ['conversations', session?.user.id], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => messengerApi.conversations(pageParam), getNextPageParam: p => p.nextCursor ?? undefined });
  return <Screen><AppHeader title="Tin nhắn" action={<IconButton label="Trò chuyện mới" icon="add" onPress={() => router.push('/conversations/create')} />} /><Button title="Làm mới" secondary onPress={() => void query.refetch()} />{query.isPending && <Loading />}
    {query.data?.pages.flatMap(p => p.items).map(c => <Card key={c.id}><ConversationName userId={c.participantUserId} title={c.title} /><Label>{c.lastMessage?.content ?? 'Chưa có tin nhắn văn bản'}</Label><Label muted>{c.unreadCount} chưa đọc</Label><Button secondary title="Mở trò chuyện" onPress={() => router.push(`/conversations/${c.id}`)} /></Card>)}
    {query.error && <ErrorNotice error={query.error} retry={() => void query.refetch()} />}{query.data?.pages[0].items.length === 0 && <Label muted>Bắt đầu trò chuyện với một người bạn.</Label>}{query.hasNextPage && <Button title="Thêm hội thoại" disabled={query.isFetchingNextPage} onPress={() => void query.fetchNextPage()} />}
  </Screen>;
}
