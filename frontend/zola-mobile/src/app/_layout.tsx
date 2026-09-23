import { Stack } from 'expo-router';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { AuthProvider, useAuth } from '../auth/AuthProvider';
import { AuthScreen } from '../auth/AuthScreen';
import { clearSession, initializeSession } from '../auth/session';
import { Button, ErrorNotice, Loading, Screen, useTheme } from '../components/ui';
import { getApiBaseUrl } from '../config/env';
import { RealtimeProvider } from '../realtime/RealtimeProvider';
import { PushNotificationsProvider } from '../realtime/PushNotificationsProvider';
function Routes() {
  const { state, session } = useAuth();
  const theme = useTheme();
  try { getApiBaseUrl(); } catch (error) { return <Screen><ErrorNotice error={error} /></Screen>; }
  if (state === 'loading') return <Screen><Loading /></Screen>;
  if ((state === 'offline' && !session) || state === 'storage-error') return <Screen><ErrorNotice error={new Error(state === 'offline' ? 'Không thể kết nối. Phiên vẫn được giữ trên thiết bị.' : 'Không thể truy cập kho lưu phiên an toàn.')} retry={() => { void initializeSession().catch(() => {}); }} /><Button secondary title="Xóa phiên trên thiết bị" onPress={() => { void clearSession().catch(() => {}); }} /></Screen>;
  if (state === 'anonymous') return <AuthScreen />;
  return <PushNotificationsProvider><RealtimeProvider><Stack screenOptions={{ headerTitle: 'Zola', headerStyle: { backgroundColor: theme.bg }, headerTintColor: theme.text, headerShadowVisible: false, contentStyle: { backgroundColor: theme.bg } }}><Stack.Screen name="(tabs)" options={{ headerShown: false }} /><Stack.Screen name="index" options={{ headerShown: false }} /></Stack></RealtimeProvider></PushNotificationsProvider>;
}
export default function RootLayout() {
  return <SafeAreaProvider><AuthProvider><Routes /></AuthProvider></SafeAreaProvider>;
}
