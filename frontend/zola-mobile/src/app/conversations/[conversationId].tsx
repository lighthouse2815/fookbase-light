import { AppState, FlatList, KeyboardAvoidingView, Platform, View, type ViewToken } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useCallback, useEffect, useRef, useState } from 'react';
import { router, useLocalSearchParams } from 'expo-router';
import { useIsFocused } from 'expo-router/react-navigation';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { messengerApi, type Message } from '../../api/messages';
import { pickMedia, uploadMedia, type PickedMedia } from '../../api/media';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Field, Label, Loading, Screen, styles, useTheme } from '../../components/ui';
import { MediaView } from '../../components/MediaView';
import { PickedMediaPreview } from '../../components/PickedMediaPreview';
import { useSessionDraft } from '../../features/drafts/sessionDraft';
import { confirmDiscard, useDraftLeave } from '../../features/drafts/useDraftLeave';
const emptyDraft = { text: '', file: null as PickedMedia | null, uploadedId: null as string | null };
export default function ConversationScreen() {
  const { conversationId } = useLocalSearchParams<{ conversationId: string }>();
  const { session } = useAuth();
  return <ConversationContent key={`${session?.user.id}:${conversationId}`} />;
}
function ConversationContent() {
  const theme = useTheme();
  const { conversationId } = useLocalSearchParams<{ conversationId: string }>(); const { session } = useAuth(); const cache = useQueryClient();
  const [draft, setDraft, discard, current] = useSessionDraft(session?.user.id, `conversation:${conversationId}`, emptyDraft);
  const { text, file } = draft; const [error, setError] = useState<unknown>(null); const [progress, setProgress] = useState('');
  const sending = useRef(false); const picking = useRef(false);
  const conversation = useQuery({ queryKey: ['conversation', session?.user.id, conversationId], queryFn: () => messengerApi.conversation(conversationId) });
  const query = useInfiniteQuery({ queryKey: ['messages', session?.user.id, conversationId], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => messengerApi.messages(conversationId, pageParam), getNextPageParam: p => p.hasMore ? p.nextCursor ?? undefined : undefined });
  const messages = Array.from(new Map(query.data?.pages.flatMap(p => p.items).map(m => [m.id, m])).values()).sort((a,b) => a.createdAtUtc.localeCompare(b.createdAtUtc) || a.id.localeCompare(b.id));
  const focused = useIsFocused();
  const [appState, setAppState] = useState(AppState.currentState);
  const [visibleIds, setVisibleIds] = useState<string[]>([]);
  const [readError, setReadError] = useState<unknown>(null);
  const [reading, setReading] = useState(false);
  const [confirmed, setConfirmed] = useState<Message | null>(null);
  const readLock = useRef(false);
  const mounted = useRef(true);
  const viewabilityConfig = useRef({ itemVisiblePercentThreshold: 60, minimumViewTime: 500 }).current;
  const onViewableItemsChanged = useCallback(({ viewableItems }: { viewableItems: ViewToken<Message>[] }) => {
    setVisibleIds(viewableItems.filter(token => token.isViewable).map(token => token.item.id));
  }, []);
  useEffect(() => {
    mounted.current = true;
    const listener = AppState.addEventListener('change', setAppState);
    return () => { mounted.current = false; listener.remove(); };
  }, []);
  const candidate = messages.filter(message => visibleIds.includes(message.id) && message.senderUserId !== session?.user.id).at(-1);
  useEffect(() => {
    if (!focused || appState !== 'active' || !session || !candidate || (confirmed && (candidate.createdAtUtc < confirmed.createdAtUtc || (candidate.createdAtUtc === confirmed.createdAtUtc && candidate.id <= confirmed.id))) || readLock.current || readError) return;
    readLock.current = true;
    setReading(true);
    void messengerApi.read(conversationId, candidate.id).then(() => {
      if (!mounted.current) return;
      setConfirmed(candidate);
      void cache.invalidateQueries({ queryKey: ['conversations'] });
    }).catch(value => { if (mounted.current) setReadError(value); }).finally(() => {
      readLock.current = false;
      if (mounted.current) setReading(false);
    });
  }, [focused, appState, session, candidate?.id, candidate?.createdAtUtc, confirmed, reading, readError, conversationId, cache]);
  const send = useMutation({ mutationFn: async () => {
    if (!current()) throw new Error('Phiên đăng nhập đã thay đổi.');
    let id = draft.uploadedId;
    if (file && !id) {
      setProgress('Đang tải tệp 1/1 · Đã xong 0/1'); id = await uploadMedia(file);
      if (!current()) throw new Error('Phiên đăng nhập đã thay đổi.');
      setDraft(previous => ({ ...previous, uploadedId: id }));
    }
    if (!current()) throw new Error('Phiên đăng nhập đã thay đổi.');
    setProgress(file ? 'Đã tải 1/1 tệp · Đang gửi tin…' : 'Đang gửi tin…');
    const message = await messengerApi.send(conversationId, text, id ? [id] : []);
    discard(); return message;
  }, onSuccess: async () => { await cache.invalidateQueries(); }, onError: () => { if (current()) void query.refetch(); }, onSettled: () => { sending.current = false; if (mounted.current) setProgress(''); } });
  useDraftLeave(Boolean(text || file), send.isPending);
  if ((conversation.error && !conversation.data) || (query.error && !query.data)) return <Screen><ErrorNotice error={conversation.error || query.error} retry={() => { void conversation.refetch(); void query.refetch(); }} /></Screen>;
  return <SafeAreaView edges={['bottom', 'left', 'right']} style={{ flex: 1, backgroundColor: theme.bg }}><KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined} keyboardVerticalOffset={90}>
    <View style={{ padding: 12, gap: 8 }}><Label title>{conversation.data?.title ?? 'Trò chuyện'}</Label><Button secondary title="Thành viên và cài đặt nhóm" onPress={() => router.push({ pathname: '/conversations/members', params: { id: conversationId } })} /></View>
    {(conversation.error || query.error) ? <ErrorNotice error={conversation.error || query.error} retry={() => { if (conversation.error) void conversation.refetch(); if (query.isFetchNextPageError) void query.fetchNextPage(); else if (query.error) void query.refetch(); }} /> : null}
    {readError ? <ErrorNotice error={readError} retry={() => setReadError(null)} /> : null}
    <FlatList inverted maintainVisibleContentPosition={{ minIndexForVisible: 0 }} onViewableItemsChanged={onViewableItemsChanged} viewabilityConfig={viewabilityConfig} style={{ flex: 1 }} contentContainerStyle={styles.screen} keyboardShouldPersistTaps="handled" data={[...messages].reverse()} keyExtractor={m => m.id}
      ListEmptyComponent={query.isPending ? <Loading /> : <Label muted>Chưa có tin nhắn.</Label>}
      ListFooterComponent={query.hasNextPage ? <Button secondary title="Tin nhắn trước" disabled={query.isFetchingNextPage} onPress={() => void query.fetchNextPage()} /> : null}
      refreshing={query.isRefetching} onRefresh={() => void query.refetch()}
      renderItem={({ item: m }) => <Card><Label muted>{m.senderUserId === session?.user.id ? 'Bạn' : 'Thành viên'} · {new Date(m.createdAtUtc).toLocaleString('vi-VN')}</Label><Label>{m.deletedAtUtc ? 'Tin nhắn đã thu hồi' : m.content}</Label>{!m.deletedAtUtc && m.attachments.map(a => <MediaView key={a.mediaId} path={`/api/messages/media/${a.mediaId}/read-url`} />)}{m.readAtUtc && <Label muted>Đã đọc</Label>}</Card>} />
    <View style={{ padding: 12, gap: 8 }}><Field label="Tin nhắn" multiline value={text} onChangeText={value => setDraft(previous => ({ ...previous, text: value }))} editable={!send.isPending} style={{ maxHeight: 100 }} />
      {file && <><PickedMediaPreview key={file.uri} file={file} /><Button title={`Bỏ ${file.name}`} secondary onPress={() => setDraft(previous => ({ ...previous, file: null, uploadedId: null }))} disabled={send.isPending} /></>}
      <View style={styles.row}><Button title="Đính kèm" secondary disabled={send.isPending} onPress={() => {
        if (picking.current || sending.current) return; picking.current = true; setError(null);
        void pickMedia().then(value => { if (value && !sending.current) setDraft(previous => ({ ...previous, file: value, uploadedId: null })); }).catch(setError).finally(() => { picking.current = false; });
      }} /><Button title={send.isPending ? 'Đang gửi…' : 'Gửi'} disabled={send.isPending || (!text.trim() && !file)} onPress={() => { if (!sending.current && !picking.current) { sending.current = true; setError(null); send.mutate(); } }} /></View>
      {progress ? <Label>{progress}</Label> : null}
      {(text || file) && <Button title="Bỏ bản nháp" secondary disabled={send.isPending} onPress={() => confirmDiscard(() => { discard(); send.reset(); setError(null); })} />}
      {(send.error || error) ? <ErrorNotice error={send.error || error} /> : null}{send.error && <Label muted>Kiểm tra tin nhắn mới trước khi gửi lại để tránh gửi trùng.</Label>}
    </View>
  </KeyboardAvoidingView></SafeAreaView>;
}
