import { View } from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { privacyApi, type PrivacySettings } from '../../api/privacy';
import { useAuth } from '../../auth/AuthProvider';
import { Button, Card, ErrorNotice, Label, Loading, Screen, styles } from '../../components/ui';
const options = { defaultPostPrivacy: ['public','friends','onlyMe'], friendRequestPolicy: ['everyone','friendsOfFriends'], friendListVisibility: ['public','friends','onlyMe'], followListVisibility: ['public','friends','onlyMe'] };
const labels: Record<string,string> = { public:'Công khai', friends:'Bạn bè', onlyMe:'Chỉ mình tôi', everyone:'Mọi người', friendsOfFriends:'Bạn của bạn bè', defaultPostPrivacy:'Đối tượng mặc định của bài viết', friendRequestPolicy:'Ai có thể kết bạn', friendListVisibility:'Ai xem danh sách bạn bè', followListVisibility:'Ai xem danh sách theo dõi' };
export default function Privacy() {
  const { session } = useAuth(); const cache = useQueryClient();
  const query = useQuery({ queryKey:['privacy',session?.user.id], queryFn:privacyApi.get });
  const save = useMutation({ mutationFn:(change:Partial<PrivacySettings>) => privacyApi.update(change), onSuccess:() => cache.invalidateQueries({ queryKey:['privacy'] }) });
  return <Screen><Label title>Quyền riêng tư</Label>{query.isPending && <Loading />}{query.error && <ErrorNotice error={query.error} retry={() => void query.refetch()} />}{query.data && Object.entries(options).map(([key, values]) => <Card key={key}><Label>{labels[key]}</Label><View style={styles.row}>{values.map(value => <Button key={value} title={labels[value]} secondary={query.data[key as keyof PrivacySettings] !== value} disabled={save.isPending} onPress={() => save.mutate({ [key]:value })} />)}</View></Card>)}{save.error && <ErrorNotice error={save.error} />}{save.isSuccess && <Label>Đã lưu thay đổi.</Label>}</Screen>;
}
