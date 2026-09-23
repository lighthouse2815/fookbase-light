import { Pressable, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { authApi } from '../../api/auth';
import { clearSession, getSession } from '../../auth/session';
import { Avatar, Button, Card, Icon, IconButton, Label, Screen, useTheme } from '../../components/ui';

const shortcuts = [
  ['/profile', 'Trang cá nhân', 'people', 'Nhìn lại không gian của bạn'],
  ['/friends', 'Bạn bè', 'people', 'Kết nối và lời mời mới'],
  ['/messages', 'Tin nhắn', 'messages', 'Những cuộc nói chuyện đang mở'],
  ['/notifications', 'Thông báo', 'notifications', 'Những điều cần bạn chú ý'],
  ['/groups', 'Nhóm', 'people', 'Cộng đồng cùng sở thích'],
  ['/pages', 'Trang', 'people', 'Theo dõi thương hiệu và cộng đồng'],
  ['/events', 'Sự kiện', 'sparkle', 'Khám phá điều sắp diễn ra'],
  ['/reels', 'Reels', 'reels', 'Một vòng video ngắn'],
  ['/saved', 'Đã lưu', 'bookmark', 'Góc cảm hứng của bạn'],
  ['/memories', 'Kỷ niệm', 'sparkle', 'Nhìn lại ngày này năm xưa'],
  ['/birthdays', 'Sinh nhật', 'people', 'Gửi lời chúc đến bạn bè'],
  ['/ai-chat', 'Trợ lý AI', 'sparkle', 'Một tia ý tưởng bất ngờ'],
] as const;

export default function Account() {
  const session = getSession(); const t = useTheme(); const username = session?.user.username ?? 'bạn';
  return <Screen>
    <View style={menuStyles.heading}><Label title style={{ fontSize: 28 }}>Menu</Label><IconButton label="Tìm kiếm" icon="search" onPress={() => router.push('/search')} /></View>
    <Pressable accessibilityRole="button" accessibilityLabel="Mở trang cá nhân" onPress={() => router.push(`/profile/${session?.user.id}`)} style={({ pressed }) => [menuStyles.profile, { backgroundColor: t.card, opacity: pressed ? 0.72 : 1 }]}><Avatar label={username} size={58} online /><View style={{ flex: 1, gap: 2 }}><Text style={{ color: t.text, fontSize: 18, fontWeight: '900' }}>{username}</Text><Text style={{ color: t.primary, fontSize: 13, fontWeight: '700' }}>Xem trang cá nhân của bạn</Text></View><Icon name="arrow" color={t.muted} size={20} /></Pressable>
    <View style={menuStyles.sectionHeader}><Label title style={{ fontSize: 20 }}>Lối tắt</Label><Text style={{ color: t.primary, fontSize: 13, fontWeight: '800' }}>Xem tất cả</Text></View>
    <View style={menuStyles.grid}>{shortcuts.map(([path, title, icon]) => <Pressable key={path} accessibilityRole="button" accessibilityLabel={title} onPress={() => router.push(path === '/profile' ? `/profile/${session?.user.id}` : path)} style={({ pressed }) => [menuStyles.tile, { backgroundColor: t.card, opacity: pressed ? 0.72 : 1 }]}><View style={[menuStyles.tileIcon, { backgroundColor: `${t.primary}1c` }]}><Icon name={icon} color={t.primary} size={24} /></View><Text style={{ color: t.text, fontSize: 13, fontWeight: '800' }} numberOfLines={2}>{title}</Text></Pressable>)}</View>
    <Card tone="soft" style={menuStyles.settings}><Icon name="lock" color={t.muted} size={21} /><View style={{ flex: 1 }}><Text style={{ color: t.text, fontWeight: '800' }}>Cài đặt & quyền riêng tư</Text><Text style={{ color: t.muted, fontSize: 12, marginTop: 2 }}>Bảo mật, thông báo và dữ liệu tài khoản</Text></View><Pressable accessibilityRole="button" accessibilityLabel="Mở cài đặt quyền riêng tư" onPress={() => router.push('/settings/privacy')}><Icon name="arrow" color={t.muted} size={20} /></Pressable></Card>
    <Button title="Đăng xuất" danger icon={<Icon name="arrow" color={t.danger} size={18} />} onPress={() => { const token = session?.refreshToken; const revoke = token ? authApi.logout(token) : Promise.resolve(); void clearSession().catch(() => {}); void revoke.catch(() => {}); }} />
    <Text style={{ color: t.subtle, textAlign: 'center', fontSize: 12, paddingTop: 3 }}>Fookbase Light · v1.0</Text>
  </Screen>;
}

const menuStyles = StyleSheet.create({
  heading: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  profile: { flexDirection: 'row', alignItems: 'center', gap: 12, padding: 14, borderRadius: 12 },
  sectionHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginTop: 4 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  tile: { width: '31.7%', minHeight: 98, padding: 11, borderRadius: 11, gap: 9 },
  tileIcon: { width: 39, height: 39, borderRadius: 20, alignItems: 'center', justifyContent: 'center' },
  settings: { flexDirection: 'row', alignItems: 'center', gap: 11, padding: 14 },
});
