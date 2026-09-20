import { useState } from 'react';
import { router, useLocalSearchParams } from 'expo-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { View } from 'react-native';
import { postsApi } from '../../api/posts';
import { groupsApi } from '../../api/groups';
import { pickMedia, uploadMedia, type PickedMedia } from '../../api/media';
import { Button, Card, ErrorNotice, Field, Label, Screen, styles } from '../../components/ui';
export default function CreatePost() {
  const { groupId } = useLocalSearchParams<{ groupId?: string }>();
  const [content, setContent] = useState(''); const [privacy, setPrivacy] = useState('friends'); const [files, setFiles] = useState<PickedMedia[]>([]); const [error, setError] = useState<unknown>(null);
  const cache = useQueryClient();
  const send = useMutation({ mutationFn: async () => { const ids: string[] = []; for (const file of files) ids.push(await uploadMedia(file)); return groupId ? groupsApi.createPost(groupId, content, ids) : postsApi.create({ content, privacy, mediaIds: ids }); }, onSuccess: async post => { await cache.invalidateQueries(); router.replace(`/posts/${post.id}`); } });
  return <Screen><Label title>Tạo bài viết</Label><Card><Field label="Bạn đang nghĩ gì?" multiline style={{ minHeight: 150, textAlignVertical: 'top' }} value={content} onChangeText={setContent} editable={!send.isPending} />
    {!groupId && <View style={styles.row}>{[['public', 'Công khai'], ['friends', 'Bạn bè'], ['onlyMe', 'Chỉ mình tôi']].map(([v, label]) => <Button key={v} secondary={privacy !== v} title={label} disabled={send.isPending} onPress={() => setPrivacy(v)} />)}</View>}
    {files.map((file, index) => <Button key={`${file.uri}-${index}`} secondary title={`Bỏ ${file.name}`} disabled={send.isPending} onPress={() => setFiles(files.filter((_, i) => i !== index))} />)}
    <Button secondary title="Thêm ảnh / video" disabled={send.isPending} onPress={() => { void pickMedia().then(file => { if (file) setFiles([...files, file]); }).catch(setError); }} />
    {(error || send.error) ? <ErrorNotice error={error || send.error} /> : null}<Button title={send.isPending ? 'Đang tải và đăng…' : 'Đăng bài'} disabled={send.isPending || (!content.trim() && !files.length)} onPress={() => send.mutate()} />
  </Card></Screen>;
}
