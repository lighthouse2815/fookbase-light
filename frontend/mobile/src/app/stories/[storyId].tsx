import { useEffect, useState } from 'react';
import { Alert } from 'react-native';
import { useLocalSearchParams, router } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { storiesApi } from '../../api/stories';
import { useAuth } from '../../auth/AuthProvider';
import { MediaView } from '../../components/MediaView';
import { Button, Card, ErrorNotice, Field, Label, Loading, Screen } from '../../components/ui';
export default function Story() {
  const { storyId } = useLocalSearchParams<{ storyId: string }>(); const { session } = useAuth(); const [reply, setReply] = useState(''); const cache = useQueryClient();
  const query = useQuery({ queryKey: ['story', session?.user.id, storyId], queryFn: () => storiesApi.get(storyId) });
  const action = useMutation({ mutationFn: (fn: () => Promise<unknown>) => fn(), onSuccess: () => cache.invalidateQueries() });
  useEffect(() => { if (query.data && !query.data.isViewed && !query.data.canManage) void storiesApi.markViewed(storyId).catch(() => {}); }, [storyId, query.data?.isViewed, query.data?.canManage]);
  if (!query.data) return <Screen>{query.error ? <ErrorNotice error={query.error} retry={() => void query.refetch()} /> : <Loading />}</Screen>;
  const s = query.data;
  return <Screen><Label title>{s.author.displayName}</Label><MediaView path={s.media.accessPath} /><Label>{s.caption}</Label><Label muted>Hết hạn: {new Date(s.expiresAtUtc).toLocaleString('vi-VN')}</Label>
    <Button title={s.viewerReaction ? 'Bỏ cảm xúc' : 'Yêu thích'} disabled={action.isPending} onPress={() => action.mutate(() => s.viewerReaction ? storiesApi.removeReaction(storyId) : storiesApi.setReaction(storyId, 'love'))} />
    {!s.canManage && <Card><Field label="Trả lời tin" value={reply} onChangeText={setReply} /><Button title="Gửi trả lời" disabled={!reply.trim() || action.isPending} onPress={() => action.mutate(async () => { const c = await storiesApi.reply(storyId, reply); router.push(`/conversations/${c.conversationId}`); })} /></Card>}
    {s.canManage && <><Label muted>{s.viewerCount ?? 0} lượt xem</Label><Button secondary title="Xóa tin" onPress={() => Alert.alert('Xóa tin?', 'Tin sẽ không còn hiển thị.', [{ text:'Hủy', style:'cancel' }, { text:'Xóa', style:'destructive', onPress: () => action.mutate(async () => { await storiesApi.remove(storyId); router.replace('/stories'); }) }])} /></>}{action.error && <ErrorNotice error={action.error} />}
  </Screen>;
}
