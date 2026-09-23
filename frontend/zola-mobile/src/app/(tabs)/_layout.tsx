import { Tabs } from 'expo-router';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import { useQuery } from '@tanstack/react-query';
import { notificationsApi } from '../../api/notifications';
import { useAuth } from '../../auth/AuthProvider';
import { Icon, useTheme } from '../../components/ui';
export default function MainTabs() {
  const theme = useTheme(); const { session } = useAuth();
  const { bottom } = useSafeAreaInsets();
  const unread = useQuery({ queryKey: ['notification-count', session?.user.id], queryFn: notificationsApi.getUnreadCount });
  return <SafeAreaView edges={['top']} style={{ flex: 1, backgroundColor: theme.bg }}><Tabs initialRouteName="messages" screenOptions={{ headerShown: false, tabBarStyle: { height: 62 + bottom, paddingTop: 6, paddingBottom: 6 + bottom, backgroundColor: theme.card, borderTopColor: theme.border }, tabBarItemStyle: { borderRadius: 12, marginHorizontal: 4 }, tabBarLabelStyle: { fontSize: 10, fontWeight: '700' }, tabBarActiveTintColor: theme.primary, tabBarInactiveTintColor: theme.muted }}>
    <Tabs.Screen name="messages" options={{ title: 'Trò chuyện', tabBarIcon: ({ color }) => <Icon name="messages" color={color} size={22} /> }} />
    <Tabs.Screen name="notifications" options={{ title: 'Thông báo', tabBarBadge: unread.data?.unreadNotificationCount || undefined, tabBarIcon: ({ color }) => <Icon name="notifications" color={color} size={22} /> }} />
    <Tabs.Screen name="menu" options={{ title: 'Tài khoản', tabBarIcon: ({ color }) => <Icon name="menu" color={color} size={22} /> }} />
  </Tabs></SafeAreaView>;
}
