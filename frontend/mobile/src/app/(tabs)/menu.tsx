import { Pressable, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { authApi } from '../../api/auth';
import { clearSession, getSession } from '../../auth/session';
import { Avatar, Button, Card, Icon, Label, Screen, useTheme } from '../../components/ui';

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
    <View style={[menuStyles.hero, { backgroundColor: t.primary }]}><View style={menuStyles.heroGlow} /><View style={menuStyles.heroTop}><Avatar label={username} size={64} online /><View style={{ flex: 1, gap: 2 }}><Text style={menuStyles.eyebrow}>KHÔNG GIAN CỦA BẠN</Text><Text style={menuStyles.heroName} numberOfLines={2} adjustsFontSizeToFit minimumFontScale={0.75}>Xin chào, {username}</Text><Text style={menuStyles.heroCopy}>Mỗi lượt ghé qua đều để lại một tia sáng.</Text></View><Icon name="sparkle" color="#ffffffaa" size={24} /></View><View style={menuStyles.status}><View style={menuStyles.statusDot} /><Text style={{ color: '#ffffffd9', fontSize: 12, fontWeight: '700' }}>Đang hoạt động trên Fookbase</Text></View></View>
    <View style={menuStyles.sectionHeader}><View><Label title style={{ fontSize: 20 }}>Lối tắt của bạn</Label><Text style={{ color: t.muted, fontSize: 12 }}>Chạm một lần để đi thẳng vào điều cần tìm</Text></View><Icon name="arrow" color={t.primary} size={19} /></View>
    <View style={menuStyles.grid}>{shortcuts.map(([path, title, icon, caption]) => <Pressable key={path} accessibilityRole="button" accessibilityLabel={title} onPress={() => router.push(path === '/profile' ? `/profile/${session?.user.id}` : path)} style={({ pressed }) => [menuStyles.tile, { backgroundColor: t.card, borderColor: t.border, opacity: pressed ? 0.72 : 1 }]}><View style={[menuStyles.tileIcon, { backgroundColor: `${t.primary}1c` }]}><Icon name={icon} color={t.primary} size={20} /></View><Text style={{ color: t.text, fontSize: 14, fontWeight: '800' }}>{title}</Text><Text style={{ color: t.muted, fontSize: 11, lineHeight: 16 }} numberOfLines={2}>{caption}</Text></Pressable>)}</View>
    <Card tone="soft" style={menuStyles.discovery}><View style={[menuStyles.discoveryIcon, { backgroundColor: `${t.accent}22` }]}><Icon name="sparkle" color={t.accent} size={24} /></View><View style={{ flex: 1, gap: 2 }}><Label style={{ fontWeight: '800' }}>Bất ngờ một chút?</Label><Text style={{ color: t.muted, fontSize: 12, lineHeight: 18 }}>Mở Trợ lý AI và biến một ý tưởng vụn thành điều có thể chia sẻ.</Text></View><Pressable accessibilityRole="button" accessibilityLabel="Mở Trợ lý AI" onPress={() => router.push('/ai-chat')}><Icon name="arrow" color={t.accent} size={21} /></Pressable></Card>
    <Button title="Đăng xuất" danger icon={<Icon name="arrow" color={t.danger} size={18} />} onPress={() => { const token = session?.refreshToken; const revoke = token ? authApi.logout(token) : Promise.resolve(); void clearSession().catch(() => {}); void revoke.catch(() => {}); }} />
    <Text style={{ color: t.subtle, textAlign: 'center', fontSize: 12, paddingTop: 3 }}>Fookbase Light · v1.0</Text>
  </Screen>;
}

const menuStyles = StyleSheet.create({
  hero: { marginHorizontal: -16, marginTop: -14, paddingHorizontal: 20, paddingTop: 24, paddingBottom: 20, borderBottomLeftRadius: 28, borderBottomRightRadius: 28, overflow: 'hidden' },
  heroGlow: { position: 'absolute', width: 190, height: 190, right: -58, top: -76, borderRadius: 100, backgroundColor: '#ffffff18' },
  heroTop: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  eyebrow: { color: '#ffffffb8', fontSize: 10, fontWeight: '900', letterSpacing: 1.4 },
  heroName: { color: '#fff', fontSize: 25, fontWeight: '900', letterSpacing: -0.5 },
  heroCopy: { color: '#ffffffc7', fontSize: 12, lineHeight: 18 },
  status: { flexDirection: 'row', alignItems: 'center', gap: 7, marginTop: 18 },
  statusDot: { width: 8, height: 8, borderRadius: 8, backgroundColor: '#7df0a6' },
  sectionHeader: { flexDirection: 'row', alignItems: 'flex-end', justifyContent: 'space-between', marginTop: 4 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 10 },
  tile: { width: '48.4%', minHeight: 122, padding: 13, borderRadius: 17, borderWidth: 1, gap: 7 },
  tileIcon: { width: 38, height: 38, borderRadius: 13, alignItems: 'center', justifyContent: 'center' },
  discovery: { flexDirection: 'row', alignItems: 'center', gap: 11, padding: 14 },
  discoveryIcon: { width: 45, height: 45, borderRadius: 15, alignItems: 'center', justifyContent: 'center' },
});
