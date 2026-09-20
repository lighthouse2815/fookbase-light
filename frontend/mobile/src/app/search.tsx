import { useState } from 'react';
import { router } from 'expo-router';
import { useInfiniteQuery } from '@tanstack/react-query';
import { usersApi } from '../api/users';
import { useAuth } from '../auth/AuthProvider';
import { Button, Card, ErrorNotice, Field, Label, Loading, Screen } from '../components/ui';
export default function Search() {
  const [text, setText] = useState(''); const [term, setTerm] = useState(''); const { session } = useAuth();
  const query = useInfiniteQuery({ queryKey: ['search-users', session?.user.id, term], enabled: !!term, initialPageParam: 0, queryFn: ({ pageParam }) => usersApi.search(term, pageParam), getNextPageParam: p => p.offset + p.items.length < p.total ? p.offset + p.items.length : undefined });
  return <Screen><Label title>Tìm bạn bè</Label><Field label="Tên hoặc tên người dùng" value={text} onChangeText={setText} returnKeyType="search" onSubmitEditing={() => setTerm(text.trim())} /><Button title="Tìm kiếm" disabled={!text.trim()} onPress={() => setTerm(text.trim())} />
    {query.isFetching && <Loading />}{query.error && <ErrorNotice error={query.error} retry={() => void query.refetch()} />}
    {query.data?.pages.flatMap(p => p.items).map(user => <Card key={user.userId}><Label>{user.displayName}</Label><Label muted>@{user.username}</Label><Button secondary title="Xem hồ sơ" onPress={() => router.push(`/profile/${user.userId}`)} /></Card>)}
    {query.data?.pages[0].total === 0 && <Label>Không tìm thấy người dùng.</Label>}{query.hasNextPage && <Button title="Xem thêm" onPress={() => void query.fetchNextPage()} disabled={query.isFetchingNextPage} />}
  </Screen>;
}
