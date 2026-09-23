import { useState } from 'react';
import { Alert } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { messengerApi } from '../../api/messages';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Field, Label, Loading, Screen } from '../../components/ui';
import { UserCard } from '../../components/UserCard';
export default function Members() {
  const { id } = useLocalSearchParams<{ id: string }>(); const { session } = useAuth(); const cache = useQueryClient();
  const [search, setSearch] = useState(''); const [term, setTerm] = useState(''); const [title, setTitle] = useState('');
  const query = useQuery({ queryKey: ['conversation', session?.user.id, id], queryFn: () => messengerApi.conversation(id) });
  const users = useQuery({ queryKey: ['chat-user-search', session?.user.id, term], enabled: !!term, queryFn: () => messengerApi.searchUsers(term) });
  const action = useMutation({ mutationFn: (fn: () => Promise<unknown>) => fn(), onSuccess: () => cache.invalidateQueries() });
  const conversation = query.data; const me = conversation?.participants.find(p => p.userId === session?.user.id && !p.leftAtUtc);
  const manage = conversation?.type === 'group' && (me?.role === 'owner' || me?.role === 'admin');
  const confirm = (label: string, fn: () => Promise<unknown>) => Alert.alert(label, 'Thay đổi sẽ áp dụng cho cuộc trò chuyện này.', [{ text: 'Hủy', style: 'cancel' }, { text: 'Xác nhận', onPress: () => action.mutate(fn) }]);
  if (query.error || !conversation) return <Screen>{query.error ? <ErrorNotice error={query.error} retry={() => void query.refetch()} /> : <Loading />}</Screen>;
  return <Screen><Label title>{conversation.title ?? 'Thành viên'}</Label>
    {conversation.participants.filter(p => !p.leftAtUtc).map(p => <UserCard key={p.userId} userId={p.userId}><Label muted>{p.role}</Label>
      {manage && p.userId !== session?.user.id && p.role !== 'owner' && <>
        <Button secondary title="Xóa khỏi nhóm" disabled={action.isPending} onPress={() => confirm('Xóa thành viên?', () => messengerApi.removeParticipant(id, p.userId))} />
        {me?.role === 'owner' && <><Button secondary title={p.role === 'admin' ? 'Bỏ quyền quản trị' : 'Đặt làm quản trị viên'} disabled={action.isPending} onPress={() => confirm('Đổi quyền thành viên?', () => messengerApi.changeRole(id, p.userId, p.role === 'admin' ? 'member' : 'admin'))} /><Button secondary title="Chuyển quyền chủ nhóm" disabled={action.isPending} onPress={() => confirm('Chuyển quyền chủ nhóm?', () => messengerApi.transferOwnership(id, p.userId))} /></>}
      </>}
    </UserCard>)}
    {manage && <Card><Field label="Tên nhóm mới" value={title} onChangeText={setTitle} /><Button title="Đổi tên nhóm" disabled={action.isPending || !title.trim()} onPress={() => action.mutate(() => messengerApi.updateConversation(id, { title: title.trim() }))} /><Field label="Tìm người để thêm" value={search} onChangeText={setSearch} /><Button secondary title="Tìm kiếm" disabled={!search.trim()} onPress={() => setTerm(search.trim())} />{users.error && <ErrorNotice error={users.error} />}{users.data?.items.filter(u => !conversation.participants.some(p => p.userId === u.userId && !p.leftAtUtc)).map(u => <Card key={u.userId}><Label>{u.displayName}</Label><Button title="Thêm vào nhóm" disabled={action.isPending} onPress={() => action.mutate(() => messengerApi.addParticipants(id, [u.userId]))} /></Card>)}</Card>}
    {conversation.type === 'group' && me?.role !== 'owner' && <Button secondary title="Rời nhóm" disabled={action.isPending} onPress={() => confirm('Rời nhóm?', async () => { await messengerApi.leave(id); cache.removeQueries({ queryKey: ['messages', session?.user.id, id] }); router.replace('/messages'); })} />}
    {action.error && <ErrorNotice error={action.error} />}
  </Screen>;
}
