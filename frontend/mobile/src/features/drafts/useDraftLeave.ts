import { useNavigation } from 'expo-router';
import { usePreventRemove } from 'expo-router/react-navigation';
import { Alert } from 'react-native';

export function useDraftLeave(hasDraft: boolean, pending: boolean) {
  const navigation = useNavigation();
  usePreventRemove(hasDraft || pending, ({ data }) => {
    if (pending) { Alert.alert('Đang gửi', 'Hãy chờ gửi hoàn tất trước khi rời màn hình.'); return; }
    Alert.alert('Rời màn soạn?', 'Bản nháp và tệp đã chọn được giữ trong phiên đăng nhập này.', [
      { text: 'Tiếp tục soạn', style: 'cancel' },
      { text: 'Rời và giữ bản nháp', onPress: () => navigation.dispatch(data.action) },
    ]);
  });
}
export function confirmDiscard(discard: () => void) {
  Alert.alert('Bỏ bản nháp?', 'Nội dung và tệp đã chọn sẽ được xóa khỏi bản nháp.', [
    { text: 'Giữ lại', style: 'cancel' }, { text: 'Bỏ bản nháp', style: 'destructive', onPress: discard },
  ]);
}
