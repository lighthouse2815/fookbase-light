import { Alert } from 'react-native';
import { friendsApi, type RelationshipStatus } from '../api/friends';
import { Button, Label } from './ui';
export function FriendActions({ userId, relationship, pending, run }: { userId: string; relationship: RelationshipStatus; pending: boolean; run: (fn: () => Promise<unknown>) => void }) {
  switch (relationship.status) {
    case 'none': return <Button title="Kết bạn" disabled={pending} onPress={() => run(() => friendsApi.sendRequest(userId))} />;
    case 'request_received': return <><Button title="Chấp nhận" disabled={pending || !relationship.requestId} onPress={() => run(() => friendsApi.acceptRequest(relationship.requestId!))} /><Button title="Từ chối" secondary disabled={pending || !relationship.requestId} onPress={() => run(() => friendsApi.declineRequest(relationship.requestId!))} /></>;
    case 'request_sent': return <Button title="Hủy lời mời" secondary disabled={pending || !relationship.requestId} onPress={() => run(() => friendsApi.cancelRequest(relationship.requestId!))} />;
    case 'friends': return <Button title="Hủy kết bạn" secondary disabled={pending} onPress={() => Alert.alert('Hủy kết bạn?', 'Bạn có thể gửi lại lời mời sau.', [{ text: 'Giữ lại', style: 'cancel' }, { text: 'Hủy kết bạn', style: 'destructive', onPress: () => run(() => friendsApi.unfriend(userId)) }])} />;
    default: return <Label muted>Không thể tương tác với tài khoản này. Bạn có thể quản lý danh sách chặn trong mục Bạn bè.</Label>;
  }
}
