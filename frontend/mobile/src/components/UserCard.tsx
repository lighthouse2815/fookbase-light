import { useQuery } from '@tanstack/react-query';
import { router } from 'expo-router';
import { usersApi } from '../api/users';
import { useAuth } from '../auth/AuthProvider';
import { Button, Card, Label } from './ui';
import type { PropsWithChildren } from 'react';
export function UserCard({ userId, children }: PropsWithChildren<{ userId: string }>) {
  const { session } = useAuth();
  const user = useQuery({ queryKey: ['user', session?.user.id, userId], queryFn: () => usersApi.getById(userId) });
  return <Card><Label>{user.data?.displayName ?? 'Thành viên'}</Label><Button secondary title="Xem trang cá nhân" onPress={() => router.push(`/profile/${userId}`)} />{children}</Card>;
}
