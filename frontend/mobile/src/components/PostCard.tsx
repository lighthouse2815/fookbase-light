import { StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { postsApi, type Post } from '../api/posts';
import { resolveProfileImageUrl } from '../api/users';
import { useAuth } from '../auth/AuthProvider';
import { Avatar, Card, ErrorNotice, Icon, Label, Loading, PostAction, useTheme } from './ui';
import { MediaView } from './MediaView';

export function PostCard({ post, detail = false }: { post: Post; detail?: boolean }) {
  const { session } = useAuth(); const cache = useQueryClient(); const t = useTheme();
  const saved = useQuery({ queryKey: ['post-saved', session?.user.id, post.id], queryFn: () => postsApi.getById(post.id), enabled: post.viewerHasSaved === undefined });
  const hasSaved = post.viewerHasSaved ?? saved.data?.viewerHasSaved;
  const action = useMutation({ mutationFn: (fn: () => Promise<unknown>) => fn(), onSuccess: () => cache.invalidateQueries() });
  const authorName = post.displayAuthor?.name ?? 'Thành viên Fookbase';
  const handle = post.displayAuthor?.username ? `@${post.displayAuthor.username}` : 'fookbase';
  const avatarUrl = post.displayAuthor?.avatarUrl ? resolveProfileImageUrl(post.displayAuthor.avatarUrl) : null;
  const reactionTotal = Object.values(post.reactionCounts).reduce((a, b) => a + b, 0);
  const privacyLabel = post.privacy === 'friends' ? 'Bạn bè' : post.privacy === 'onlyMe' ? 'Chỉ mình tôi' : 'Công khai';
  return <Card tone="raised" style={postStyles.card}>
    <View style={postStyles.header}>
      <Avatar label={authorName} uri={avatarUrl} size={46} online />
      <View style={{ flex: 1, gap: 1 }}><Label style={{ fontSize: 15, fontWeight: '800' }}>{authorName}</Label><Text style={{ color: t.muted, fontSize: 12 }}>{handle} · {new Date(post.createdAtUtc).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })} · {privacyLabel}</Text></View>
      <Icon name="more" color={t.muted} size={18} />
    </View>
    {!!post.content && <Text style={[postStyles.content, { color: t.text }]}>{post.content}</Text>}
    {post.mediaIds.map(id => <View key={id} style={postStyles.media}><MediaView path={`/api/posts/${post.id}/media/${id}/access`} /></View>)}
    <View style={postStyles.meta}><View style={postStyles.metaLeft}><View style={[postStyles.reactionDot, { backgroundColor: t.like }]}><Icon name="heart" color="#fff" size={11} /></View><Text style={{ color: t.muted, fontSize: 12 }}>{reactionTotal || 'Chưa có'} {reactionTotal === 1 ? 'cảm xúc' : 'cảm xúc'}</Text></View><Text style={{ color: t.muted, fontSize: 12 }}>{post.commentCount} bình luận · {post.shareCount ?? 0} chia sẻ</Text></View>
    <View style={[postStyles.actions, { borderTopColor: t.border }]}>
      <PostAction title={post.viewerReaction ? 'Đã thích' : 'Thích'} icon="heart" active={!!post.viewerReaction} disabled={action.isPending} onPress={() => action.mutate(() => post.viewerReaction ? postsApi.removeReaction(post.id) : postsApi.setReaction(post.id, 'like'))} />
      {!detail && <PostAction title="Bình luận" icon="comment" onPress={() => router.push(`/posts/${post.id}`)} />}
      <PostAction title={hasSaved ? 'Bỏ lưu' : 'Lưu'} icon="bookmark" active={!!hasSaved} disabled={action.isPending || hasSaved === undefined} onPress={() => action.mutate(() => hasSaved ? postsApi.removeSaved(post.id) : postsApi.save(post.id))} />
      <PostAction title="Chia sẻ" icon="share" disabled={action.isPending} onPress={() => { if (session) action.mutate(() => postsApi.share(post.id, { destinationType: 'profile', destinationId: session.user.id })); }} />
    </View>
    {saved.isPending && post.viewerHasSaved === undefined && <Loading />}
    {action.error && <ErrorNotice error={action.error} />}
  </Card>;
}

const postStyles = StyleSheet.create({
  card: { padding: 0, overflow: 'hidden' },
  header: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 16, paddingTop: 15 },
  content: { fontSize: 16, lineHeight: 24, paddingHorizontal: 16, paddingTop: 12 },
  media: { marginTop: 12, overflow: 'hidden' },
  meta: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: 16, paddingVertical: 11 },
  metaLeft: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  reactionDot: { width: 19, height: 19, borderRadius: 10, alignItems: 'center', justifyContent: 'center' },
  actions: { borderTopWidth: 1, flexDirection: 'row', alignItems: 'center', paddingHorizontal: 8, paddingVertical: 5 },
});
