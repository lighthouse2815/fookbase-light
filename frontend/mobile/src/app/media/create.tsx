import { useState } from 'react';
import { useLocalSearchParams, router } from 'expo-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { View } from 'react-native';
import { pickMedia, uploadMedia, type PickedMedia } from '../../api/media';
import { storiesApi, type StoryPrivacy } from '../../api/stories';
import { reelsApi } from '../../api/reels';
import { Button, Card, ErrorNotice, Field, Label, Screen, styles } from '../../components/ui';
export default function CreateMedia() {
  const { kind } = useLocalSearchParams<{ kind: string }>(); const reel = kind === 'reel'; const cache = useQueryClient();
  const [file, setFile] = useState<PickedMedia | null>(null); const [caption, setCaption] = useState(''); const [privacy, setPrivacy] = useState<StoryPrivacy>('friends'); const [error, setError] = useState<unknown>(null);
  const create = useMutation({ mutationFn: async () => {
    if (!file) throw new Error('Hãy chọn media.');
    if (reel && !file.mimeType.startsWith('video/')) throw new Error('Reel cần một video.');
    const id = await uploadMedia(file);
    return reel ? reelsApi.create({ caption, privacy, videoMediaId: id }) : storiesApi.create(id, caption, privacy);
  }, onSuccess: async value => { await cache.invalidateQueries(); router.replace(reel ? '/reels' : `/stories/${value.id}`); } });
  return <Screen><Label title>{reel ? 'Tạo Reel' : 'Đăng tin'}</Label><Card><Field label="Chú thích" value={caption} onChangeText={setCaption} multiline /><View style={styles.row}>{(['public','friends','onlyMe'] as const).map((p,i) => <Button key={p} title={['Công khai','Bạn bè','Chỉ mình tôi'][i]} secondary={privacy !== p} onPress={() => setPrivacy(p)} />)}</View>
    <Button title={file ? `Đổi ${file.name}` : 'Chọn ảnh / video'} secondary disabled={create.isPending} onPress={() => { void pickMedia().then(setFile).catch(setError); }} />{(create.error || error) ? <ErrorNotice error={create.error || error} /> : null}<Button title={create.isPending ? 'Đang đăng…' : 'Đăng'} disabled={!file || create.isPending} onPress={() => create.mutate()} /></Card></Screen>;
}
