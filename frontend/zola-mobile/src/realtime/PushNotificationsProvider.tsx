import { useEffect, type PropsWithChildren } from 'react';
import { Platform } from 'react-native';
import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { router } from 'expo-router';
import { notificationsApi } from '../api/notifications';
import { useAuth } from '../auth/AuthProvider';

Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowBanner: true,
    shouldShowList: true,
    shouldPlaySound: true,
    shouldSetBadge: false,
  }),
});

export function PushNotificationsProvider({ children }: PropsWithChildren) {
  const { session } = useAuth();

  useEffect(() => {
    if (!session) return;
    let active = true;
    let token: string | null = null;

    const register = async () => {
      if (!Device.isDevice) return;
      if (Platform.OS === 'android') {
        await Notifications.setNotificationChannelAsync('messages', {
          name: 'Tin nhắn Zola',
          importance: Notifications.AndroidImportance.MAX,
          vibrationPattern: [0, 250, 250, 250],
          lightColor: '#2d88ff',
        });
      }
      const permission = await Notifications.getPermissionsAsync();
      const finalStatus = permission.status === 'granted'
        ? permission.status
        : (await Notifications.requestPermissionsAsync()).status;
      if (finalStatus !== 'granted' || !active) return;

      const projectId = process.env.EXPO_PUBLIC_EAS_PROJECT_ID ?? Constants.easConfig?.projectId ?? Constants.expoConfig?.extra?.eas?.projectId;
      if (!projectId) return;
      token = (await Notifications.getExpoPushTokenAsync({ projectId })).data;
      if (active) await notificationsApi.registerZolaPushToken(token);
    };

    void register().catch(() => {});
    const tokenListener = Notifications.addPushTokenListener(value => {
      token = value.data;
      void notificationsApi.registerZolaPushToken(value.data).catch(() => {});
    });
    const responseListener = Notifications.addNotificationResponseReceivedListener(response => {
      const conversationId = response.notification.request.content.data?.conversationId;
      if (typeof conversationId === 'string' && /^[a-f0-9-]{36}$/i.test(conversationId)) {
        router.push(`/conversations/${conversationId}`);
      }
    });
    return () => {
      active = false;
      tokenListener.remove();
      responseListener.remove();
      if (token) void notificationsApi.unregisterZolaPushToken(token).catch(() => {});
    };
  }, [session?.user.id]);

  return children;
}
