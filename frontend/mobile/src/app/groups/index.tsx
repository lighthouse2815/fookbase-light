import { useState } from 'react';
import { router } from 'expo-router';
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { groupsApi } from '../../api/groups';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Field, Label, Loading, Screen } from '../../components/ui';
export default function Groups() {
  const { session } = useAuth(); const cache = useQueryClient(); const [name, setName] = useState(''); const [search, setSearch] = useState(''); const [term, setTerm] = useState('');
  const groups = useInfiniteQuery({ queryKey: ['groups', session?.user.id, term], initialPageParam: undefined as string | undefined, queryFn: ({ pageParam }) => groupsApi.discover(term, pageParam), getNextPageParam: p => p.nextCursor ?? undefined });
  const create = useMutation({ mutationFn: () => groupsApi.create({ name, privacy: 'private' }), onSuccess: async g => { await cache.invalidateQueries(); router.push(`/groups/${g.id}`); } });
  return <Screen><Label title>Nhóm cộng đồng</Label><Field label="Tìm nhóm" value={search} onChangeText={setSearch} /><Button title="Tìm kiếm" onPress={() => setTerm(search)} /><Card><Field label="Tên nhóm mới (riêng tư)" value={name} onChangeText={setName} /><Button title="Tạo nhóm" disabled={create.isPending || !name.trim()} onPress={() => create.mutate()} />{create.error && <ErrorNotice error={create.error} />}</Card>
    {groups.isPending && <Loading />}{groups.data?.pages.flatMap(p => p.items).map(g => <Card key={g.id}><Label title>{g.name}</Label><Label muted>{g.memberCount} thành viên · {g.privacy}</Label><Label>{g.description}</Label><Button secondary title="Xem nhóm" onPress={() => router.push(`/groups/${g.id}`)} /></Card>)}{groups.error && <ErrorNotice error={groups.error} retry={() => void groups.refetch()} />}{groups.hasNextPage && <Button secondary title="Xem thêm" disabled={groups.isFetchingNextPage} onPress={() => void groups.fetchNextPage()} />}
  </Screen>;
}
