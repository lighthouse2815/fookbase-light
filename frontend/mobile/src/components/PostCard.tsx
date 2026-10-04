import { useState } from 'react';
import { Alert, Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { postsApi, type Post } from '../api/posts';
import { reportsApi, type ReportReason } from '../api/reports';
import { resolveProfileImageUrl } from '../api/users';
import { useAuth } from '../auth/AuthProvider';
import { Avatar, Button, Card, ErrorNotice, Field, Icon, Label, Loading, PostAction, styles, useTheme } from './ui';
import { MediaView } from './MediaView';

export function PostCard({ post, detail = false }: { post: Post; detail?: boolean }) {
  const { session } = useAuth(); const cache = useQueryClient(); const t = useTheme();
  const [menu, setMenu] = useState<'options' | 'edit' | 'report' | null>(null);
  const [content, setContent] = useState(post.content);
  const [reason, setReason] = useState<ReportReason>('spam');
  const [details, setDetails] = useState('');
  const saved = useQuery({ queryKey: ['post-saved', session?.user.id, post.id], queryFn: () => postsApi.getById(post.id), enabled: post.viewerHasSaved === undefined });
  const hasSaved = post.viewerHasSaved ?? saved.data?.viewerHasSaved;
  const action = useMutation({ mutationFn: (fn: () => Promise<unknown>) => fn(), onSuccess: () => cache.invalidateQueries() });
  const authorName = post.displayAuthor?.name ?? 'Thành viên Fookbase';
  const handle = post.displayAuthor?.username ? `@${post.displayAuthor.username}` : 'fookbase';
  const avatarUrl = post.displayAuthor?.avatarUrl ? resolveProfileImageUrl(post.displayAuthor.avatarUrl) : null;
  const profileUserId = post.authorUserId ?? (post.displayAuthor?.type === 'user' ? post.displayAuthor.id : null);
  const own = post.authorUserId === session?.user.id;
  const reactionTotal = Object.values(post.reactionCounts).reduce((a, b) => a + b, 0);
  const privacyLabel = post.privacy === 'friends' ? 'Bạn bè' : post.privacy === 'onlyMe' ? 'Chỉ mình tôi' : 'Công khai';
  return <Card style={[postStyles.card, { borderBottomColor: t.border }]}>
    <View style={postStyles.header}>
      <Pressable accessibilityRole={profileUserId ? 'button' : undefined} accessibilityLabel={profileUserId ? `Xem trang cá nhân ${authorName}` : undefined} disabled={!profileUserId} onPress={() => { if (profileUserId) router.push(`/profile/${profileUserId}`); }} style={({ pressed }) => [postStyles.author, { opacity: pressed && profileUserId ? 0.72 : 1 }]}><Avatar label={authorName} uri={avatarUrl} size={46} /><View style={{ flex: 1, gap: 1 }}><Label style={{ fontSize: 15, fontWeight: '800' }}>{authorName}</Label><Text style={{ color: t.muted, fontSize: 12 }}>{handle} · {new Date(post.createdAtUtc).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })} · {privacyLabel}</Text></View></Pressable>
      <Pressable accessibilityRole="button" accessibilityLabel="Tùy chọn bài viết" hitSlop={8} onPress={() => { action.reset(); setContent(post.content); setMenu('options'); }} style={postStyles.more}><Icon name="more" color={t.muted} size={19} /></Pressable>
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
    <Modal visible={menu !== null} transparent animationType="fade" onRequestClose={() => { if (!action.isPending) setMenu(null); }}>
      <View style={postStyles.modalBackdrop}><Card style={postStyles.modalCard}><ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={postStyles.modalContent}>
        <Label title style={{ fontSize: 22 }}>{menu === 'edit' ? 'Chỉnh sửa bài viết' : menu === 'report' ? 'Báo cáo bài viết' : 'Tùy chọn bài viết'}</Label>
        {menu === 'options' && <>
          {own && post.contentType !== 'reel' && <Button title="Chỉnh sửa" secondary onPress={() => setMenu('edit')} />}
          {own && <Button title="Xóa bài viết" danger disabled={action.isPending} onPress={() => Alert.alert('Xóa bài viết?', 'Bài viết sẽ không còn hiển thị.', [{ text: 'Giữ lại', style: 'cancel' }, { text: 'Xóa', style: 'destructive', onPress: () => action.mutate(async () => { await postsApi.delete(post.id); setMenu(null); if (detail) router.replace('/feed'); }) }])} />}
          {!own && <Button title="Báo cáo" secondary onPress={() => { setReason('spam'); setDetails(''); setMenu('report'); }} />}
        </>}
        {menu === 'edit' && <><Field label="Nội dung bài viết" multiline value={content} onChangeText={setContent} editable={!action.isPending} style={{ minHeight: 120, textAlignVertical: 'top' }} /><Button title={action.isPending ? 'Đang lưu…' : 'Lưu thay đổi'} disabled={action.isPending || (!content.trim() && !post.mediaIds.length)} onPress={() => action.mutate(async () => { await postsApi.update(post.id, { content, privacy: post.privacy, mediaIds: post.mediaIds, textBackground: post.textBackground }); setMenu(null); })} /></>}
        {menu === 'report' && <><Label muted>Chọn lý do báo cáo</Label><View style={styles.row}>{reportReasons.map(([value, label]) => <Button key={value} compact title={label} secondary={reason !== value} disabled={action.isPending} onPress={() => setReason(value)} />)}</View><Field label="Chi tiết (không bắt buộc)" multiline value={details} onChangeText={setDetails} editable={!action.isPending} /><Button title={action.isPending ? 'Đang gửi…' : 'Gửi báo cáo'} disabled={action.isPending} onPress={() => action.mutate(async () => { await reportsApi.reportPost(post.id, { reason, details: details.trim() || undefined }); setMenu(null); Alert.alert('Đã gửi báo cáo', 'Báo cáo của bạn đã được tiếp nhận.'); })} /></>}
        {action.error && <ErrorNotice error={action.error} />}
        <Button title="Đóng" secondary disabled={action.isPending} onPress={() => setMenu(null)} />
      </ScrollView></Card></View>
    </Modal>
  </Card>;
}

const reportReasons: [ReportReason, string][] = [['spam', 'Spam'], ['harassment', 'Quấy rối'], ['hateSpeech', 'Ngôn từ thù ghét'], ['nudity', 'Khỏa thân'], ['violence', 'Bạo lực'], ['scam', 'Lừa đảo'], ['other', 'Khác']];

const postStyles = StyleSheet.create({
  card: { padding: 0, overflow: 'hidden', borderRadius: 0, borderBottomWidth: StyleSheet.hairlineWidth },
  header: { flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: 16, paddingTop: 14 },
  author: { flex: 1, flexDirection: 'row', alignItems: 'center', gap: 10 },
  more: { width: 32, height: 32, alignItems: 'center', justifyContent: 'center' },
  content: { fontSize: 15, lineHeight: 22, paddingHorizontal: 16, paddingTop: 11 },
  media: { marginTop: 12, overflow: 'hidden' },
  meta: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: 16, paddingVertical: 11 },
  metaLeft: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  reactionDot: { width: 19, height: 19, borderRadius: 10, alignItems: 'center', justifyContent: 'center' },
  actions: { borderTopWidth: 1, flexDirection: 'row', alignItems: 'center', paddingHorizontal: 8, paddingVertical: 5 },
  modalBackdrop: { flex: 1, justifyContent: 'center', padding: 20, backgroundColor: '#00000080' },
  modalCard: { padding: 0, maxHeight: '90%' },
  modalContent: { padding: 16, gap: 12 },
});
