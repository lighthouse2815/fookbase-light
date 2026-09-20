import { router } from 'expo-router';
import { useQuery } from '@tanstack/react-query';
import { storiesApi } from '../../api/stories';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Label, Loading, Screen } from '../../components/ui';
export default function Stories() {
  const { session } = useAuth(); const query = useQuery({ queryKey: ['stories', session?.user.id], queryFn: storiesApi.tray });
  return <Screen><Label title>Tin của bạn bè</Label><Button title="Đăng tin" onPress={() => router.push({ pathname: '/media/create', params: { kind: 'story' } })} />{query.isPending && <Loading />}{query.error && <ErrorNotice error={query.error} retry={() => void query.refetch()} />}{query.data?.items.map(item => <Card key={item.author.userId}><Label>{item.author.displayName}{item.hasUnseenStories ? ' · Có tin mới' : ''}</Label>{item.stories.map(s => <Button secondary key={s.id} title={s.caption || 'Xem tin'} onPress={() => router.push(`/stories/${s.id}`)} />)}</Card>)}</Screen>;
}
