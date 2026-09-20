import type { PropsWithChildren } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, TextInput, View, useColorScheme, type TextInputProps } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

export function useTheme() {
  return useColorScheme() === 'dark'
    ? { bg: '#10141e', card: '#1b2230', text: '#f3f5fb', muted: '#acb8cd', border: '#303c50', primary: '#8c9eff' }
    : { bg: '#f3f5fa', card: '#ffffff', text: '#18233b', muted: '#66748d', border: '#dce3ef', primary: '#4056db' };
}
export function Label({ children, muted = false, title = false }: PropsWithChildren<{ muted?: boolean; title?: boolean }>) {
  const t = useTheme();
  return <Text style={{ color: muted ? t.muted : t.text, fontSize: title ? 24 : 16, fontWeight: title ? '700' : '400', lineHeight: title ? 32 : 24 }}>{children}</Text>;
}
export function Screen({ children }: PropsWithChildren) {
  const t = useTheme();
  return <SafeAreaView edges={['bottom', 'left', 'right']} style={{ flex: 1, backgroundColor: t.bg }}><ScrollView keyboardShouldPersistTaps="handled" automaticallyAdjustKeyboardInsets contentContainerStyle={styles.screen}>{children}</ScrollView></SafeAreaView>;
}
export function Card({ children }: PropsWithChildren) {
  const t = useTheme();
  return <View style={[styles.card, { backgroundColor: t.card, borderColor: t.border }]}>{children}</View>;
}
export function Button({ title, onPress, disabled = false, secondary = false }: { title: string; onPress: () => void; disabled?: boolean; secondary?: boolean }) {
  const t = useTheme();
  return <Pressable accessibilityRole="button" accessibilityLabel={title} accessibilityState={{ disabled }} disabled={disabled} onPress={onPress} style={({ pressed }) => [styles.button, { backgroundColor: secondary ? t.card : t.primary, borderColor: t.border, opacity: disabled ? 0.5 : pressed ? 0.7 : 1 }]}><Text style={{ color: secondary ? t.text : '#fff', fontSize: 15, fontWeight: '600', textAlign: 'center' }}>{title}</Text></Pressable>;
}
export function Field({ label, ...props }: TextInputProps & { label: string }) {
  const t = useTheme();
  return <View style={{ gap: 6 }}><Label muted>{label}</Label><TextInput accessibilityLabel={label} placeholderTextColor={t.muted} {...props} style={[styles.field, { color: t.text, backgroundColor: t.card, borderColor: t.border }, props.style]} /></View>;
}
export function ErrorNotice({ error, retry }: { error: unknown; retry?: () => void }) {
  return <Card><Text accessibilityRole="alert" style={{ color: '#d94747' }}>{error instanceof Error ? error.message : 'Không thể hoàn tất yêu cầu.'}</Text>{retry && <Button title="Thử lại" onPress={retry} secondary />}</Card>;
}
export function Loading() { return <ActivityIndicator accessibilityLabel="Đang tải" style={{ padding: 24 }} />; }
export const styles = StyleSheet.create({
  screen: { padding: 16, gap: 14, flexGrow: 1 },
  card: { padding: 16, gap: 12, borderRadius: 18, borderWidth: 1 },
  button: { minHeight: 46, justifyContent: 'center', paddingHorizontal: 14, paddingVertical: 10, borderRadius: 12, borderWidth: 1 },
  field: { borderWidth: 1, borderRadius: 12, padding: 12, minHeight: 48, fontSize: 16 },
  row: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 8 },
});
