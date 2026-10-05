import { useEffect, useRef, useState } from 'react';
import { useLocalSearchParams, router } from 'expo-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { View } from 'react-native';
import { pickMedia, uploadMedia, type PickedMedia } from '../../api/media';
import { storiesApi, type StoryPrivacy } from '../../api/stories';
import { reelsApi } from '../../api/reels';
import { useAuth } from '../../auth/AuthProvider';
import { useSessionDraft } from '../../features/drafts/sessionDraft';
import { confirmDiscard, useDraftLeave } from '../../features/drafts/useDraftLeave';
import { PickedMediaPreview } from '../../components/PickedMediaPreview';
import { Button, Card, ErrorNotice, Field, Label, Screen, styles } from '../../components/ui';
const emptyDraft = { file: null as PickedMedia | null, caption: '', privacy: 'friends' as StoryPrivacy, uploadedId: null as string | null };
export default function CreateMedia() {
  const { kind } = useLocalSearchParams<{ kind?: string }>();
  const { session } = useAuth();
  return <CreateMediaContent key={`${session?.user.id}:${kind === "reel" ? "reel" : "story"}`} />;
}
function CreateMediaContent() {
  const { kind } = useLocalSearchParams<{ kind: string }>(); const reel = kind === 'reel'; const cache = useQueryClient();
  const { session } = useAuth(); const accountId = session?.user.id;
  const [draft, setDraft, discard, current] = useSessionDraft(accountId, `media:${reel ? 'reel' : 'story'}`, emptyDraft);
  const { file, caption, privacy } = draft;
  const [error, setError] = useState<unknown>(null); const [progress, setProgress] = useState('');
  const sending = useRef(false); const picking = useRef(false);
  const create = useMutation({ mutationFn: async () => {
    if (!file) throw new Error('Hãy chọn media.');
    if (reel && !file.mimeType.startsWith('video/')) throw new Error('Reel cần một video.');
    if (!current()) throw new Error('Phiên đăng nhập đã thay đổi.');
    setProgress('Đang tải tệp 1/1 · Đã xong 0/1');
    const id = draft.uploadedId || await uploadMedia(file);
    if (!current()) throw new Error('Phiên đăng nhập đã thay đổi.');
    setDraft(previous => ({ ...previous, uploadedId: id })); setProgress('Đã tải 1/1 tệp · Đang đăng…');
    const value = await (reel ? reelsApi.create({ caption, privacy, videoMediaId: id }) : storiesApi.create(id, caption, privacy));
    discard(); return value;
  }, onSuccess: async () => { await cache.invalidateQueries(); }, onSettled: () => { sending.current = false; setProgress(''); } });
  useDraftLeave(Boolean(caption || file || privacy !== 'friends'), create.isPending);
  useEffect(() => { if (create.isSuccess && current()) router.replace(reel ? '/reels' : `/stories/${create.data.id}`); }, [create.isSuccess, create.data, reel]);
  return <Screen><Label title>{reel ? 'Tạo Reel' : 'Đăng tin'}</Label><Card><Field label="Chú thích" value={caption} onChangeText={value => setDraft(previous => ({ ...previous, caption: value }))} multiline editable={!create.isPending} />
    <View style={styles.row}>{(['public','friends','onlyMe'] as const).map((value,index) => <Button key={value} title={['Công khai','Bạn bè','Chỉ mình tôi'][index]} secondary={privacy !== value} disabled={create.isPending} onPress={() => setDraft(previous => ({ ...previous, privacy: value }))} />)}</View>
    {file && <PickedMediaPreview key={file.uri} file={file} />}
    <Button title={file ? `Đổi ${file.name}` : 'Chọn ảnh / video'} secondary disabled={create.isPending} onPress={() => {
      if (picking.current || sending.current) return; picking.current = true; setError(null);
      void pickMedia().then(value => { if (value && !sending.current) setDraft(previous => ({ ...previous, file: value, uploadedId: null })); }).catch(setError).finally(() => { picking.current = false; });
    }} />
    {progress ? <Label>{progress}</Label> : null}{(create.error || error) ? <ErrorNotice error={create.error || error} /> : null}
    <Button title={create.isPending ? 'Đang đăng…' : 'Đăng'} disabled={!file || create.isPending} onPress={() => { if (!sending.current && !picking.current) { sending.current = true; setError(null); create.mutate(); } }} />
    <Button secondary title="Bỏ bản nháp" disabled={create.isPending} onPress={() => confirmDiscard(() => { discard(); create.reset(); setError(null); })} />
  </Card></Screen>;
}
