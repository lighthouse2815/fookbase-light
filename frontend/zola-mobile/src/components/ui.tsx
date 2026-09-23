import type { ComponentProps, PropsWithChildren, ReactNode } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, useColorScheme, View, type ColorValue, type StyleProp, type TextInputProps, type TextStyle, type ViewStyle } from 'react-native';
import { Image } from 'expo-image';
import { SymbolView } from 'expo-symbols';
import { SafeAreaView } from 'react-native-safe-area-context';

const darkTheme = {
  bg: '#18191a', card: '#242526', surface2: '#303132', surface3: '#3a3b3c',
  text: '#e4e6eb', muted: '#b0b3b8', subtle: '#8a8d91', border: '#3e4042',
  primary: '#2d88ff', primarySoft: '#1f4f8f', accent: '#f0b84f', success: '#4bbf73', danger: '#ff7c8a', like: '#ff6b7d',
};
const lightTheme = {
  bg: '#f0f2f5', card: '#ffffff', surface2: '#e4e6eb', surface3: '#d8dadf',
  text: '#1c1e21', muted: '#606770', subtle: '#8a8d91', border: '#d8dadf',
  primary: '#1877f2', primarySoft: '#dceaff', accent: '#b76b00', success: '#258a3f', danger: '#d54040', like: '#d54040',
};

export type Theme = typeof darkTheme;
export function useTheme(): Theme { return useColorScheme() === 'light' ? lightTheme : darkTheme; }

export function Label({ children, muted = false, title = false, style, numberOfLines }: PropsWithChildren<{ muted?: boolean; title?: boolean; style?: StyleProp<TextStyle>; numberOfLines?: number }>) {
  const t = useTheme();
  return <Text numberOfLines={numberOfLines} style={[{ color: muted ? t.muted : t.text, fontSize: title ? 25 : 16, fontWeight: title ? '800' : '400', lineHeight: title ? 31 : 24, letterSpacing: title ? -0.5 : 0 }, style]}>{children}</Text>;
}

export function Screen({ children, scroll = true, style }: PropsWithChildren<{ scroll?: boolean; style?: StyleProp<ViewStyle> }>) {
  const t = useTheme();
  const content = scroll ? <ScrollView keyboardShouldPersistTaps="handled" automaticallyAdjustKeyboardInsets contentContainerStyle={[styles.screen, style]}>{children}</ScrollView> : <View style={[styles.screen, style]}>{children}</View>;
  return <SafeAreaView edges={['bottom', 'left', 'right']} style={{ flex: 1, backgroundColor: t.bg }}>{content}</SafeAreaView>;
}

export function Card({ children, style, tone = 'default' }: PropsWithChildren<{ style?: StyleProp<ViewStyle>; tone?: 'default' | 'raised' | 'soft' }>) {
  const t = useTheme();
  return <View style={[styles.card, tone === 'raised' && styles.cardRaised, tone === 'soft' && { backgroundColor: t.surface2 }, { backgroundColor: t.card }, style]}>{children}</View>;
}

type ButtonProps = { title: string; onPress: () => void; disabled?: boolean; secondary?: boolean; compact?: boolean; danger?: boolean; icon?: ReactNode };
export function Button({ title, onPress, disabled = false, secondary = false, compact = false, danger = false, icon }: ButtonProps) {
  const t = useTheme();
  const backgroundColor = danger ? `${t.danger}1c` : secondary ? t.surface2 : t.primary;
  const foreground = danger ? t.danger : secondary ? t.text : '#fff';
  return <Pressable accessibilityRole="button" accessibilityLabel={title} accessibilityState={{ disabled }} disabled={disabled} onPress={onPress} style={({ pressed }) => [styles.button, compact && styles.buttonCompact, { backgroundColor, borderColor: danger ? `${t.danger}66` : secondary ? t.border : t.primary, opacity: disabled ? 0.45 : pressed ? 0.75 : 1, transform: [{ scale: pressed ? 0.985 : 1 }] }]}><View style={styles.buttonContent}>{icon}{<Text style={{ color: foreground, fontSize: compact ? 13 : 15, fontWeight: '700', textAlign: 'center' }}>{title}</Text>}</View></Pressable>;
}

export type IconName = 'home' | 'messages' | 'notifications' | 'menu' | 'search' | 'add' | 'image' | 'reels' | 'sparkle' | 'bell' | 'bookmark' | 'people' | 'arrow' | 'heart' | 'comment' | 'share' | 'more' | 'close' | 'lock' | 'globe';
type NativeSymbol = ComponentProps<typeof SymbolView>['name'];

// Expo Symbols renders SF Symbols on iOS and Material Symbols on Android/web.
// Keeping the mapping here makes the icon language consistent across the app.
const iconSymbols: Record<IconName, NativeSymbol> = {
  home: { ios: 'house', android: 'home', web: 'home' },
  messages: { ios: 'message', android: 'chat', web: 'chat' },
  notifications: { ios: 'bell', android: 'notifications', web: 'notifications' },
  menu: { ios: 'line.3.horizontal', android: 'menu', web: 'menu' },
  search: { ios: 'magnifyingglass', android: 'search', web: 'search' },
  add: { ios: 'plus', android: 'add', web: 'add' },
  image: { ios: 'photo', android: 'image', web: 'image' },
  reels: { ios: 'play.rectangle', android: 'video_library', web: 'video_library' },
  sparkle: { ios: 'sparkles', android: 'auto_awesome', web: 'auto_awesome' },
  bell: { ios: 'bell', android: 'notifications', web: 'notifications' },
  bookmark: { ios: 'bookmark', android: 'bookmark', web: 'bookmark' },
  people: { ios: 'person.2', android: 'group', web: 'group' },
  arrow: { ios: 'arrow.right', android: 'arrow_forward', web: 'arrow_forward' },
  heart: { ios: 'heart', android: 'favorite', web: 'favorite' },
  comment: { ios: 'bubble.right', android: 'chat_bubble', web: 'chat_bubble' },
  share: { ios: 'square.and.arrow.up', android: 'share', web: 'share' },
  more: { ios: 'ellipsis', android: 'more_horiz', web: 'more_horiz' },
  close: { ios: 'xmark', android: 'close', web: 'close' },
  lock: { ios: 'lock', android: 'lock', web: 'lock' },
  globe: { ios: 'globe', android: 'language', web: 'language' },
};
export function Icon({ name, color, size = 20 }: { name: IconName; color?: ColorValue; size?: number }) {
  const t = useTheme();
  return <SymbolView accessible={false} name={iconSymbols[name]} tintColor={color ?? t.text} size={size} weight="semibold" />;
}

export function IconButton({ label, icon, onPress, active = false, disabled = false }: { label: string; icon: IconName; onPress: () => void; active?: boolean; disabled?: boolean }) {
  const t = useTheme();
  return <Pressable accessibilityRole="button" accessibilityLabel={label} accessibilityState={{ disabled, selected: active }} disabled={disabled} onPress={onPress} style={({ pressed }) => [styles.iconButton, { backgroundColor: active ? t.primarySoft : t.surface2, opacity: disabled ? 0.45 : pressed ? 0.7 : 1 }]}><Icon name={icon} color={active ? t.primary : t.text} size={20} /></Pressable>;
}

export function Avatar({ label, uri, size = 44, online = false }: { label: string; uri?: string | null; size?: number; online?: boolean }) {
  const t = useTheme();
  const initials = label.trim().split(/\s+/).map(part => part[0]).join('').slice(0, 2).toUpperCase() || 'F';
  const palette = [t.primary, '#7b61ff', '#d96845', '#3fa877', '#c95787'];
  const color = palette[label.length % palette.length];
  return <View style={{ width: size, height: size }}><View accessibilityLabel={`Ảnh đại diện của ${label}`} style={{ width: size, height: size, borderRadius: size / 2, backgroundColor: color, overflow: 'hidden', alignItems: 'center', justifyContent: 'center', borderWidth: 2, borderColor: t.card }}>{uri ? <Image source={{ uri }} contentFit="cover" style={{ width: '100%', height: '100%' }} /> : <Text style={{ color: '#fff', fontSize: size * 0.34, fontWeight: '800' }}>{initials}</Text>}</View>{online && <View style={{ position: 'absolute', right: -1, bottom: 0, width: Math.max(10, size * 0.24), height: Math.max(10, size * 0.24), borderRadius: size, backgroundColor: t.success, borderWidth: 2, borderColor: t.card }} />}</View>;
}

export function ActionChip({ title, icon, onPress, active = false }: { title: string; icon: IconName; onPress: () => void; active?: boolean }) {
  const t = useTheme();
  return <Pressable accessibilityRole="button" accessibilityLabel={title} onPress={onPress} style={({ pressed }) => [styles.actionChip, { backgroundColor: active ? t.primarySoft : 'transparent', opacity: pressed ? 0.72 : 1 }]}><Icon name={icon} color={active ? t.primary : t.muted} size={17} /><Text style={{ color: active ? t.primary : t.muted, fontSize: 13, fontWeight: '700' }}>{title}</Text></Pressable>;
}

export function PostAction({ title, icon, onPress, active = false, disabled = false }: { title: string; icon: IconName; onPress: () => void; active?: boolean; disabled?: boolean }) {
  const t = useTheme();
  return <Pressable accessibilityRole="button" accessibilityLabel={title} accessibilityState={{ disabled, selected: active }} disabled={disabled} onPress={onPress} style={({ pressed }) => [styles.postAction, { backgroundColor: pressed ? t.surface2 : 'transparent', opacity: disabled ? 0.45 : 1 }]}><Icon name={icon} color={active ? t.primary : t.muted} size={18} /><Text style={{ color: active ? t.primary : t.muted, fontSize: 13, fontWeight: '700' }}>{title}</Text></Pressable>;
}

export function Field({ label, ...props }: TextInputProps & { label: string }) {
  const t = useTheme();
  return <View style={{ gap: 7 }}><Label muted style={styles.fieldLabel}>{label}</Label><TextInput accessibilityLabel={label} placeholderTextColor={t.subtle} selectionColor={t.primary} {...props} style={[styles.field, { color: t.text, backgroundColor: t.surface2, borderColor: t.border }, props.style]} /></View>;
}

export function ErrorNotice({ error, retry }: { error: unknown; retry?: () => void }) {
  const t = useTheme();
  return <View style={[styles.error, { backgroundColor: `${t.danger}12`, borderColor: `${t.danger}55` }]}><Text accessibilityRole="alert" style={{ color: t.danger, flex: 1, lineHeight: 21 }}>{error instanceof Error ? error.message : 'Không thể hoàn tất yêu cầu.'}</Text>{retry && <Button title="Thử lại" onPress={retry} secondary compact danger />}</View>;
}

export function Loading() { const t = useTheme(); return <View style={styles.loading}><ActivityIndicator accessibilityLabel="Đang tải" color={t.primary} /><Text style={{ color: t.muted, fontSize: 13 }}>Đang tải thêm điều hay…</Text></View>; }

export const styles = StyleSheet.create({
  screen: { paddingHorizontal: 16, paddingTop: 14, paddingBottom: 34, gap: 14, flexGrow: 1 },
  card: { padding: 16, gap: 12, borderRadius: 16 },
  cardRaised: { shadowColor: '#000', shadowOpacity: 0.14, shadowRadius: 8, shadowOffset: { width: 0, height: 3 }, elevation: 3 },
  button: { minHeight: 48, justifyContent: 'center', paddingHorizontal: 16, paddingVertical: 11, borderRadius: 14, borderWidth: 1 },
  buttonCompact: { minHeight: 38, paddingHorizontal: 12, paddingVertical: 7, borderRadius: 11 },
  buttonContent: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8 },
  iconButton: { width: 40, height: 40, borderRadius: 20, alignItems: 'center', justifyContent: 'center' },
  actionChip: { minHeight: 38, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 7, paddingHorizontal: 8, borderRadius: 10, flex: 1 },
  postAction: { minHeight: 38, flex: 1, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 6, borderRadius: 10, paddingHorizontal: 6 },
  fieldLabel: { fontSize: 13, fontWeight: '700', lineHeight: 18, letterSpacing: 0.1 },
  field: { borderWidth: 1, borderRadius: 14, paddingHorizontal: 14, paddingVertical: 12, minHeight: 50, fontSize: 16 },
  row: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 8 },
  error: { borderWidth: 1, borderRadius: 14, padding: 12, gap: 10, flexDirection: 'row', alignItems: 'center' },
  loading: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 10, paddingVertical: 24 },
});
