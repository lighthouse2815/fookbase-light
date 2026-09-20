import { Tabs } from 'expo-router';
import { Text } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { notificationsApi } from '../../api/notifications';
import { useAuth } from '../../auth/AuthProvider';
import { useTheme } from '../../components/ui';
export default function MainTabs() {
  const theme = useTheme(); const { session } = useAuth();
  const unread = useQuery({ queryKey: ['notification-count', session?.user.id], queryFn: notificationsApi.getUnreadCount });
  return <Tabs initialRouteName="feed" screenOptions={{ headerStyle: { backgroundColor: theme.card }, headerTintColor: theme.text, tabBarStyle: { backgroundColor: theme.card, borderTopColor: theme.border }, tabBarActiveTintColor: theme.primary }}>
    <Tabs.Screen name="feed" options={{ title: 'Bảng tin', tabBarIcon: ({ color }) => <Text style={{ color, fontSize: 24 }}>⌂</Text> }} />
    <Tabs.Screen name="messages" options={{ title: 'Tin nhắn', tabBarIcon: ({ color }) => <Text style={{ color, fontSize: 22 }}>✉</Text> }} />
    <Tabs.Screen name="notifications" options={{ title: 'Thông báo', tabBarBadge: unread.data?.unreadNotificationCount || undefined, tabBarIcon: ({ color }) => <Text style={{ color, fontSize: 22 }}>♧</Text> }} />
    <Tabs.Screen name="menu" options={{ title: 'Menu', tabBarIcon: ({ color }) => <Text style={{ color, fontSize: 22 }}>☰</Text> }} />
  </Tabs>;
}
