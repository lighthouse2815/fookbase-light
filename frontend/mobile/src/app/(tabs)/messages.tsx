import { router } from 'expo-router';
import { View } from 'react-native';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { messengerApi } from '../../api/messages';
import { resolveProfileImageUrl, usersApi } from '../../api/users';
import { useAuth } from '../../auth/AuthProvider';
import { AppHeader, Avatar, Button, Card, ErrorNotice, IconButton, Label, Loading, Screen } from '../../components/ui';
function ConversationName({ userId, title }: { userId: string | null; title: string | null }) {
  const { session } = useAuth();
  const user = useQuery({ queryKey: ['user', session?.user.id, userId], queryFn: () => usersApi.getById(userId!), enabled: !!userId });
  const name = title ?? user.data?.displayName ?? 'Cuộc trò chuyện';
  return <View style={messageStyles.identity}><Avatar label={name} uri={user.data?.avatarUrl ? resolveProfileImageUrl(user.data.avatarUrl) : null} size={46} online /><View style={{ flex: 1, gap: 2 }}><Label style={{ fontWeight: '800' }} numberOfLines={1}>{name}</Label><Label muted style={{ fontSize: 12 }}>Cuộc trò chuyện</Label></View></View>;
}
export default function Messages() {
  const { session } = useAuth();
  const query = useInfiniteQuery({ queryKey: ['conversations', session?.user.id], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => messengerApi.conversations(pageParam), getNextPageParam: p => p.nextCursor ?? undefined });
  return <Screen><AppHeader title="Tin nhắn" action={<IconButton label="Trò chuyện mới" icon="add" onPress={() => router.push('/conversations/create')} />} /><Button title="Làm mới" secondary onPress={() => void query.refetch()} />{query.isPending && <Loading />}
    {query.data?.pages.flatMap(p => p.items).map(c => <Card key={c.id} style={messageStyles.card}><ConversationName userId={c.participantUserId} title={c.title} /><Label muted numberOfLines={1}>{c.lastMessage?.content ?? 'Chưa có tin nhắn văn bản'}</Label><View style={messageStyles.footer}><Label muted style={{ fontSize: 12 }}>{c.unreadCount ? `${c.unreadCount} chưa đọc` : 'Đã cập nhật'}</Label><Button compact secondary title="Mở trò chuyện" onPress={() => router.push(`/conversations/${c.id}`)} /></View></Card>)}
    {query.error && <ErrorNotice error={query.error} retry={() => void query.refetch()} />}{query.data?.pages[0].items.length === 0 && <Label muted>Bắt đầu trò chuyện với một người bạn.</Label>}{query.hasNextPage && <Button title="Thêm hội thoại" disabled={query.isFetchingNextPage} onPress={() => void query.fetchNextPage()} />}
  </Screen>;
}

const messageStyles = {
  card: { gap: 11 },
  identity: { flexDirection: 'row' as const, alignItems: 'center' as const, gap: 10 },
  footer: { flexDirection: 'row' as const, alignItems: 'center' as const, justifyContent: 'space-between' as const, gap: 8 },
};
