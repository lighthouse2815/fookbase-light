import { useState } from 'react';
import { router } from 'expo-router';
import { useMutation, useQuery } from '@tanstack/react-query';
import { messengerApi } from '../../api/messages';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Field, Label, Screen } from '../../components/ui';
export default function CreateConversation() {
  const [search, setSearch] = useState(''); const [term, setTerm] = useState(''); const [selected, setSelected] = useState<string[]>([]); const [title, setTitle] = useState(''); const { session } = useAuth();
  const users = useQuery({ queryKey: ['chat-user-search', session?.user.id, term], enabled: !!term, queryFn: () => messengerApi.searchUsers(term) });
  const create = useMutation({ mutationFn: () => selected.length === 1 && !title.trim() ? messengerApi.direct(selected[0]) : messengerApi.group(title, selected), onSuccess: c => router.replace(`/conversations/${c.id}`) });
  return <Screen><Label title>Trò chuyện mới</Label><Field label="Tìm bạn theo tên" value={search} onChangeText={setSearch} /><Button title="Tìm kiếm" disabled={!search.trim()} onPress={() => setTerm(search.trim())} />
    {users.data?.items.filter(u => u.userId !== session?.user.id).map(u => <Card key={u.userId}><Label>{u.displayName}</Label><Button secondary={!selected.includes(u.userId)} title={selected.includes(u.userId) ? 'Bỏ chọn' : 'Chọn'} onPress={() => setSelected(selected.includes(u.userId) ? selected.filter(id => id !== u.userId) : [...selected, u.userId])} /></Card>)}
    <Label muted>{selected.length} người đã chọn</Label><Field label="Tên nhóm (để trống khi nhắn riêng)" value={title} onChangeText={setTitle} />{(users.error || create.error) && <ErrorNotice error={users.error || create.error} />}<Button title="Bắt đầu trò chuyện" disabled={create.isPending || !selected.length || (selected.length > 1 && !title.trim())} onPress={() => create.mutate()} />
  </Screen>;
}
