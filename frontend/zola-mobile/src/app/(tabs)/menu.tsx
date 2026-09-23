import { StyleSheet, Text, View } from 'react-native';
import { authApi } from '../../api/auth';
import { clearSession, getSession } from '../../auth/session';
import { Avatar, Button, Card, Label, Screen, useTheme } from '../../components/ui';

export default function Account() {
  const session = getSession(); const t = useTheme(); const username = session?.user.username ?? 'bạn';
  return <Screen>
    <Label title>Tài khoản</Label>
    <Card style={menuStyles.profile}><Avatar label={username} size={58} online /><View style={{ flex: 1, gap: 2 }}><Text style={{ color: t.text, fontSize: 18, fontWeight: '900' }}>{username}</Text><Text style={{ color: t.muted, fontSize: 13 }}>Đang đăng nhập trên Zola</Text></View></Card>
    <Card tone="soft" style={menuStyles.settings}><View style={{ flex: 1 }}><Text style={{ color: t.text, fontWeight: '800' }}>Phiên đăng nhập</Text><Text style={{ color: t.muted, fontSize: 12, marginTop: 2 }}>Refresh token được lưu trong SecureStore.</Text></View></Card>
    <Button title="Đăng xuất" danger onPress={() => { const token = session?.refreshToken; const revoke = token ? authApi.logout(token) : Promise.resolve(); void clearSession().catch(() => {}); void revoke.catch(() => {}); }} />
    <Text style={{ color: t.subtle, textAlign: 'center', fontSize: 12, paddingTop: 3 }}>Zola · v1.0</Text>
  </Screen>;
}

const menuStyles = StyleSheet.create({
  profile: { flexDirection: 'row', alignItems: 'center', gap: 12, padding: 14 },
  settings: { flexDirection: 'row', alignItems: 'center', gap: 11, padding: 14 },
});
