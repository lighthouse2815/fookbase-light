import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { resolveProfileImageUrl, usersApi } from '../api/users';
import { useAuth } from '../auth/AuthProvider';
import { Avatar, Button, Card, Label } from './ui';
import { Text, View } from 'react-native';
import type { PropsWithChildren } from 'react';
export function UserCard({ userId, children }: PropsWithChildren<{ userId: string }>) {
  const { session } = useAuth();
  const user = useQuery({ queryKey: ['user', session?.user.id, userId], queryFn: () => usersApi.getById(userId) });
  const displayName = user.data?.displayName ?? 'Thành viên';
  const avatarUrl = user.data?.avatarUrl ? resolveProfileImageUrl(user.data.avatarUrl) : null;
  return <Card style={userCardStyles.card}>
    <View style={userCardStyles.identity}><Avatar label={displayName} uri={avatarUrl} size={48} /><View style={{ flex: 1, gap: 1 }}><Label style={{ fontWeight: '800' }} numberOfLines={1}>{displayName}</Label>{user.data?.username && <Text style={userCardStyles.handle}>@{user.data.username}</Text>}</View></View>
    <View style={userCardStyles.actions}><Button compact secondary title="Xem trang cá nhân" onPress={() => router.push(`/profile/${userId}`)} />{children}</View>
  </Card>;
}

const userCardStyles = {
  card: { gap: 11 },
  identity: { flexDirection: 'row' as const, alignItems: 'center' as const, gap: 10 },
  actions: { flexDirection: 'row' as const, flexWrap: 'wrap' as const, gap: 7 },
  handle: { color: '#8a8d91', fontSize: 12 },
};
