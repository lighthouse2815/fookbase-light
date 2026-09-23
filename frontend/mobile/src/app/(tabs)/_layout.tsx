import { Tabs } from 'expo-router';
import { useQuery } from '@tanstack/react-query';
import { notificationsApi } from '../../api/notifications';
import { useAuth } from '../../auth/AuthProvider';
import { Icon, useTheme } from '../../components/ui';
export default function MainTabs() {
  const theme = useTheme(); const { session } = useAuth();
  const unread = useQuery({ queryKey: ['notification-count', session?.user.id], queryFn: notificationsApi.getUnreadCount });
  return <Tabs initialRouteName="feed" screenOptions={{ headerShown: false, tabBarStyle: { height: 72, paddingTop: 8, paddingBottom: 10, backgroundColor: theme.card, borderTopColor: theme.border }, tabBarItemStyle: { borderRadius: 14, marginHorizontal: 4 }, tabBarLabelStyle: { fontSize: 11, fontWeight: '700' }, tabBarActiveTintColor: theme.primary, tabBarInactiveTintColor: theme.muted }}>
    <Tabs.Screen name="feed" options={{ title: 'Bảng tin', tabBarIcon: ({ color }) => <Icon name="home" color={color} size={22} /> }} />
    <Tabs.Screen name="messages" options={{ title: 'Tin nhắn', tabBarIcon: ({ color }) => <Icon name="messages" color={color} size={22} /> }} />
    <Tabs.Screen name="notifications" options={{ title: 'Thông báo', tabBarBadge: unread.data?.unreadNotificationCount || undefined, tabBarIcon: ({ color }) => <Icon name="notifications" color={color} size={22} /> }} />
    <Tabs.Screen name="menu" options={{ title: 'Thêm', tabBarIcon: ({ color }) => <Icon name="menu" color={color} size={22} /> }} />
  </Tabs>;
}
