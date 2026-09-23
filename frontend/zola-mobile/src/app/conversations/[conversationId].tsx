import { FlatList, KeyboardAvoidingView, Platform, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useEffect, useState } from 'react';
import { router, useLocalSearchParams } from 'expo-router';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { messengerApi } from '../../api/messages';
import { pickMedia, uploadMedia, type PickedMedia } from '../../api/media';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Field, Label, Loading, Screen, styles, useTheme } from '../../components/ui';
import { MediaView } from '../../components/MediaView';
export default function ConversationScreen() {
  const theme = useTheme();
  const { conversationId } = useLocalSearchParams<{ conversationId: string }>(); const { session } = useAuth(); const cache = useQueryClient();
  const [text, setText] = useState(''); const [file, setFile] = useState<PickedMedia | null>(null); const [error, setError] = useState<unknown>(null);
  const conversation = useQuery({ queryKey: ['conversation', session?.user.id, conversationId], queryFn: () => messengerApi.conversation(conversationId) });
  const query = useInfiniteQuery({ queryKey: ['messages', session?.user.id, conversationId], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => messengerApi.messages(conversationId, pageParam), getNextPageParam: p => p.hasMore ? p.nextCursor ?? undefined : undefined });
  const messages = Array.from(new Map(query.data?.pages.flatMap(p => p.items).map(m => [m.id, m])).values()).sort((a,b) => a.createdAtUtc.localeCompare(b.createdAtUtc));
  const latest = messages.at(-1);
  useEffect(() => { if (latest && latest.senderUserId !== session?.user.id) void messengerApi.read(conversationId, latest.id).then(() => cache.invalidateQueries({ queryKey: ['conversations'] })).catch(setError); }, [latest?.id, conversationId, session?.user.id, cache]);
  const send = useMutation({ mutationFn: async () => messengerApi.send(conversationId, text, file ? [await uploadMedia(file)] : []), onSuccess: async () => { setText(''); setFile(null); await cache.invalidateQueries(); }, onError: () => { void query.refetch(); } });
  if (conversation.error || query.error) return <Screen><ErrorNotice error={conversation.error || query.error} retry={() => { void conversation.refetch(); void query.refetch(); }} /></Screen>;
  return <SafeAreaView edges={['bottom', 'left', 'right']} style={{ flex: 1, backgroundColor: theme.bg }}><KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined} keyboardVerticalOffset={90}>
    <View style={{ padding: 12, gap: 8 }}><Label title>{conversation.data?.title ?? 'Trò chuyện'}</Label><Button secondary title="Thành viên và cài đặt nhóm" onPress={() => router.push({ pathname: '/conversations/members', params: { id: conversationId } })} /></View>
    <FlatList inverted style={{ flex: 1 }} contentContainerStyle={styles.screen} keyboardShouldPersistTaps="handled" data={[...messages].reverse()} keyExtractor={m => m.id}
      ListEmptyComponent={query.isPending ? <Loading /> : <Label muted>Chưa có tin nhắn.</Label>}
      ListFooterComponent={query.hasNextPage ? <Button secondary title="Tin nhắn trước" disabled={query.isFetchingNextPage} onPress={() => void query.fetchNextPage()} /> : null}
      refreshing={query.isRefetching} onRefresh={() => void query.refetch()}
      renderItem={({ item: m }) => <Card><Label muted>{m.senderUserId === session?.user.id ? 'Bạn' : 'Thành viên'} · {new Date(m.createdAtUtc).toLocaleString('vi-VN')}</Label><Label>{m.deletedAtUtc ? 'Tin nhắn đã thu hồi' : m.content}</Label>{!m.deletedAtUtc && m.attachments.map(a => <MediaView key={a.mediaId} path={`/api/messages/media/${a.mediaId}/read-url`} />)}{m.readAtUtc && <Label muted>Đã đọc</Label>}</Card>} />
    <View style={{ padding: 12, gap: 8 }}><Field label="Tin nhắn" multiline value={text} onChangeText={setText} editable={!send.isPending} style={{ maxHeight: 100 }} />
      {file && <Button title={`Bỏ ${file.name}`} secondary onPress={() => setFile(null)} disabled={send.isPending} />}
      <View style={styles.row}><Button title="Đính kèm" secondary disabled={send.isPending} onPress={() => { void pickMedia().then(setFile).catch(setError); }} /><Button title={send.isPending ? 'Đang gửi…' : 'Gửi'} disabled={send.isPending || (!text.trim() && !file)} onPress={() => send.mutate()} /></View>
      {(send.error || error) ? <ErrorNotice error={send.error || error} /> : null}{send.error && <Label muted>Kiểm tra tin nhắn mới trước khi gửi lại để tránh gửi trùng.</Label>}
    </View>
  </KeyboardAvoidingView></SafeAreaView>;
}
