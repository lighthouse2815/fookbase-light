import { View } from 'react-native';
import { router } from 'expo-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { postsApi, type Post } from '../api/posts';
import { useAuth } from '../auth/AuthProvider';
import { Button, Card, ErrorNotice, Label, styles } from './ui';
import { MediaView } from './MediaView';
export function PostCard({ post, detail = false }: { post: Post; detail?: boolean }) {
  const { session } = useAuth(); const cache = useQueryClient();
  const action = useMutation({ mutationFn: (fn: () => Promise<unknown>) => fn(), onSuccess: () => cache.invalidateQueries() });
  return <Card><Label>{post.displayAuthor?.name ?? 'Thành viên Fookbase'}</Label><Label muted>{new Date(post.createdAtUtc).toLocaleString('vi-VN')} · {post.privacy}</Label><Label>{post.content}</Label>
    {post.mediaIds.map(id => <MediaView key={id} path={`/api/posts/${post.id}/media/${id}/access`} />)}
    <Label muted>{Object.values(post.reactionCounts).reduce((a, b) => a + b, 0)} cảm xúc · {post.commentCount} bình luận</Label>
    <View style={styles.row}><Button secondary={!post.viewerReaction} disabled={action.isPending} title={post.viewerReaction ? 'Bỏ thích' : 'Thích'} onPress={() => action.mutate(() => post.viewerReaction ? postsApi.removeReaction(post.id) : postsApi.setReaction(post.id, 'like'))} />
    {!detail && <Button secondary title="Bình luận" onPress={() => router.push(`/posts/${post.id}`)} />}
    <Button secondary title={post.viewerHasSaved ? 'Bỏ lưu' : 'Lưu'} disabled={action.isPending} onPress={() => action.mutate(() => post.viewerHasSaved ? postsApi.removeSaved(post.id) : postsApi.save(post.id))} />
    <Button secondary title="Chia sẻ về trang cá nhân" disabled={action.isPending} onPress={() => { if (session) action.mutate(() => postsApi.share(post.id, { destinationType: 'profile', destinationId: session.user.id })); }} /></View>
    {action.error && <ErrorNotice error={action.error} />}
  </Card>;
}
