import { router } from 'expo-router';
import { View } from 'react-native';
import { useInfiniteQuery } from '@tanstack/react-query';
import { messengerApi, type Conversation } from '../../api/messages';
import { useAuth } from '../../auth/AuthProvider';
import { Avatar, Button, Card, ErrorNotice, Label, Loading, Screen } from '../../components/ui';
function ConversationName({ userId, title }: { userId: string | null; title: string | null }) {
  return <Label title>{title ?? (userId ? 'Cuộc trò chuyện riêng' : 'Cuộc trò chuyện')}</Label>;
}
export default function Messages() {
  const { session } = useAuth();
  const query = useInfiniteQuery({ queryKey: ['conversations', session?.user.id], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => messengerApi.conversations(pageParam), getNextPageParam: p => p.nextCursor ?? undefined });
  return <Screen><Label title>Zola</Label><Label muted>Những cuộc trò chuyện của bạn</Label><Button title="Trò chuyện mới" onPress={() => router.push('/conversations/create')} /><Button title="Làm mới" secondary onPress={() => void query.refetch()} />{query.isPending && <Loading />}
    {query.data?.pages.flatMap(p => p.items).map(c => <Card key={c.id} style={{ gap: 10 }}><ViewConversation conversation={c} /><Label muted>{c.lastMessage?.content ?? 'Chưa có tin nhắn văn bản'}</Label><Label muted>{c.unreadCount ? `${c.unreadCount} chưa đọc` : 'Đã cập nhật'}</Label><Button secondary title="Mở trò chuyện" onPress={() => router.push(`/conversations/${c.id}`)} /></Card>)}
    {query.error && <ErrorNotice error={query.error} retry={() => void query.refetch()} />}{query.data?.pages[0].items.length === 0 && <Label muted>Bắt đầu trò chuyện với một người bạn.</Label>}{query.hasNextPage && <Button title="Thêm hội thoại" disabled={query.isFetchingNextPage} onPress={() => void query.fetchNextPage()} />}
  </Screen>;
}

function ViewConversation({ conversation }: { conversation: Conversation }) {
  return <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}><Avatar label={conversation.title ?? 'Zola'} size={46} online={conversation.unreadCount > 0} /><ConversationName userId={conversation.participantUserId} title={conversation.title} /></View>;
}
