import { useState } from 'react';
import { useLocalSearchParams } from 'expo-router';
import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { postsApi } from '../../api/posts';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Field, Label, Loading, Screen } from '../../components/ui';
import { PostCard } from '../../components/PostCard';
export default function PostDetail() {
  const { postId } = useLocalSearchParams<{ postId: string }>(); const { session } = useAuth(); const cache = useQueryClient(); const [text, setText] = useState('');
  const post = useQuery({ queryKey: ['post', session?.user.id, postId], queryFn: () => postsApi.getById(postId) });
  const comments = useInfiniteQuery({ queryKey: ['comments', session?.user.id, postId], initialPageParam: 0, queryFn: ({ pageParam }) => postsApi.getComments(postId, pageParam), getNextPageParam: page => page.offset + page.items.length < page.total ? page.offset + page.items.length : undefined });
  const send = useMutation({ mutationFn: () => postsApi.createComment(postId, text), onSuccess: async () => { setText(''); await cache.invalidateQueries(); } });
  return <Screen>{post.isPending ? <Loading /> : post.error ? <ErrorNotice error={post.error} retry={() => void post.refetch()} /> : post.data && <PostCard post={post.data} detail />}
    <Card><Field label="Viết bình luận" multiline value={text} onChangeText={setText} /><Button title="Gửi bình luận" disabled={send.isPending || !text.trim()} onPress={() => send.mutate()} />{send.error && <ErrorNotice error={send.error} />}</Card>
    {comments.data?.pages.flatMap(p => p.items).map(c => <Card key={c.id}><Label>{c.author?.displayName ?? 'Thành viên'}</Label><Label>{c.content}</Label><Label muted>{new Date(c.createdAtUtc).toLocaleString('vi-VN')}</Label></Card>)}
    {comments.error && <ErrorNotice error={comments.error} retry={() => void comments.refetch()} />}{comments.hasNextPage && <Button title="Thêm bình luận" secondary onPress={() => void comments.fetchNextPage()} disabled={comments.isFetchingNextPage} />}
  </Screen>;
}
