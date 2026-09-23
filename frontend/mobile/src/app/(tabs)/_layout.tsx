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
  return <SafeAreaView edges={['top']} style={{ flex: 1, backgroundColor: theme.bg }}><Tabs initialRouteName="feed" screenOptions={{ headerShown: false, tabBarStyle: { height: 62 + bottom, paddingTop: 6, paddingBottom: 6 + bottom, backgroundColor: theme.card, borderTopColor: theme.border, elevation: 8, shadowColor: '#000', shadowOpacity: 0.1, shadowRadius: 6, shadowOffset: { width: 0, height: -2 } }, tabBarItemStyle: { borderRadius: 10, marginHorizontal: 4 }, tabBarLabelStyle: { fontSize: 10, fontWeight: '700' }, tabBarActiveTintColor: theme.primary, tabBarInactiveTintColor: theme.muted }}>
    <Tabs.Screen name="feed" options={{ title: 'Bảng tin', tabBarIcon: ({ color }) => <Icon name="home" color={color} size={22} /> }} />
    <Tabs.Screen name="messages" options={{ title: 'Tin nhắn', tabBarIcon: ({ color }) => <Icon name="messages" color={color} size={22} /> }} />
    <Tabs.Screen name="notifications" options={{ title: 'Thông báo', tabBarBadge: unread.data?.unreadNotificationCount || undefined, tabBarIcon: ({ color }) => <Icon name="notifications" color={color} size={22} /> }} />
    <Tabs.Screen name="menu" options={{ title: 'Thêm', tabBarIcon: ({ color }) => <Icon name="menu" color={color} size={22} /> }} />
  </Tabs></SafeAreaView>;
}
