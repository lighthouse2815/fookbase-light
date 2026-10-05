import { useEffect, useRef, useState } from 'react';
import { router, useLocalSearchParams } from 'expo-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { View } from 'react-native';
import { postsApi } from '../../api/posts';
import { groupsApi } from '../../api/groups';
import { pickMedia, uploadMedia, type PickedMedia } from '../../api/media';
import { useAuth } from '../../auth/AuthProvider';
import { useSessionDraft } from '../../features/drafts/sessionDraft';
import { confirmDiscard, useDraftLeave } from '../../features/drafts/useDraftLeave';
import { PickedMediaPreview } from '../../components/PickedMediaPreview';
import { Button, Card, ErrorNotice, Field, Label, Screen, styles } from '../../components/ui';
const emptyDraft = { content: '', privacy: 'friends', files: [] as PickedMedia[], uploaded: {} as Record<string, string> };
export default function CreatePost() {
  const { groupId } = useLocalSearchParams<{ groupId?: string }>();
  const { session } = useAuth();
  return <CreatePostContent key={`${session?.user.id}:${groupId || "feed"}`} />;
}
function CreatePostContent() {
  const { groupId } = useLocalSearchParams<{ groupId?: string }>();
  const { session } = useAuth(); const accountId = session?.user.id;
  const [draft, setDraft, discard, current] = useSessionDraft(accountId, `post:${groupId || 'feed'}`, emptyDraft);
  const { content, privacy, files } = draft;
  const [error, setError] = useState<unknown>(null); const [progress, setProgress] = useState('');
  const sending = useRef(false); const picking = useRef(false);
  const cache = useQueryClient();
  const send = useMutation({ mutationFn: async () => {
    const ids: string[] = [];
    for (const [index, file] of files.entries()) {
      if (!current()) throw new Error('Phiên đăng nhập đã thay đổi.');
      setProgress(`Đang tải tệp ${index + 1}/${files.length} · Đã xong ${ids.length}/${files.length}`);
      const id = draft.uploaded[file.uri] || await uploadMedia(file);
      if (!current()) throw new Error('Phiên đăng nhập đã thay đổi.');
      setDraft(previous => ({ ...previous, uploaded: { ...previous.uploaded, [file.uri]: id } })); ids.push(id);
    }
    if (!current()) throw new Error('Phiên đăng nhập đã thay đổi.');
    setProgress(`Đã tải ${ids.length}/${files.length} tệp · Đang đăng bài…`);
    const post = await (groupId ? groupsApi.createPost(groupId, content, ids) : postsApi.create({ content, privacy, mediaIds: ids }));
    discard(); return post;
  }, onSuccess: async () => { await cache.invalidateQueries(); }, onSettled: () => { sending.current = false; setProgress(''); } });
  useDraftLeave(Boolean(content || files.length || privacy !== 'friends'), send.isPending);
  useEffect(() => { if (send.isSuccess && current()) router.replace(`/posts/${send.data.id}`); }, [send.isSuccess, send.data]);
  return <Screen><Label title>Tạo bài viết</Label><Card><Field label="Bạn đang nghĩ gì?" multiline style={{ minHeight: 150, textAlignVertical: 'top' }} value={content} onChangeText={value => setDraft(previous => ({ ...previous, content: value }))} editable={!send.isPending} />
    {!groupId && <View style={styles.row}>{[['public', 'Công khai'], ['friends', 'Bạn bè'], ['onlyMe', 'Chỉ mình tôi']].map(([value, label]) => <Button key={value} secondary={privacy !== value} title={label} disabled={send.isPending} onPress={() => setDraft(previous => ({ ...previous, privacy: value }))} />)}</View>}
    {files.map((file, index) => <View key={`${file.uri}-${index}`}><PickedMediaPreview file={file} /><Button secondary title={`Bỏ ${file.name}`} disabled={send.isPending} onPress={() => setDraft(previous => ({ ...previous, files: previous.files.filter((_, i) => i !== index) }))} /></View>)}
    <Button secondary title="Thêm ảnh / video" disabled={send.isPending} onPress={() => {
      if (picking.current || sending.current) return; picking.current = true; setError(null);
      void pickMedia().then(file => { if (file && !sending.current) setDraft(previous => ({ ...previous, files: [...previous.files, file] })); }).catch(setError).finally(() => { picking.current = false; });
    }} />
    {progress ? <Label>{progress}</Label> : null}{(error || send.error) ? <ErrorNotice error={error || send.error} /> : null}
    <Button title={send.isPending ? 'Đang tải và đăng…' : 'Đăng bài'} disabled={send.isPending || (!content.trim() && !files.length)} onPress={() => { if (!sending.current && !picking.current) { sending.current = true; setError(null); send.mutate(); } }} />
    <Button secondary title="Bỏ bản nháp" disabled={send.isPending} onPress={() => confirmDiscard(() => { discard(); send.reset(); setError(null); })} />
  </Card></Screen>;
}
