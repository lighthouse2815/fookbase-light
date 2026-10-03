import { useEffect, useState } from 'react';
import { Linking, Pressable, StyleSheet, Text, View } from 'react-native';
import { authApi } from '../api/auth';
import { saveSession } from './session';
import { Button, Card, ErrorNotice, Field, Icon, Label, Screen, styles, useTheme } from '../components/ui';
import { beginGoogleLogin, completeGoogleLogin, isGoogleCallback } from './google';

export function AuthScreen() {
  const t = useTheme();
  const [mode, setMode] = useState<'login' | 'register' | 'forgot' | 'reset'>('login');
  const [identifier, setIdentifier] = useState(''); const [password, setPassword] = useState('');
  const [visible, setVisible] = useState(false); const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState(''); const [birthday, setBirthday] = useState('');
  const [challenge, setChallenge] = useState<{ kind: 'registration' | '2fa'; value: string } | null>(null);
  const [code, setCode] = useState(''); const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false); const [error, setError] = useState<unknown>(null);
  const [googleUrl, setGoogleUrl] = useState<string | null>(null); const [googleEnabled, setGoogleEnabled] = useState(false);
  async function finishGoogle(url: string, linkPassword?: string) {
    const result = await completeGoogleLogin(url, linkPassword);
    if (result.kind === 'link') { setGoogleUrl(result.url); setNotice('Nhập mật khẩu tài khoản hiện có để liên kết Google.'); return; }
    setGoogleUrl(null);
    if ('twoFactorRequired' in result.response) setChallenge({ kind: '2fa', value: result.response.challenge });
    else await saveSession(result.response, result.generation);
  }
  useEffect(() => {
    void authApi.providers().then(p => setGoogleEnabled(!!p.googleMobile && !!process.env.EXPO_PUBLIC_MOBILE_CALLBACK_URL)).catch(() => {});
    const handle = (url: string) => { if (isGoogleCallback(url)) void finishGoogle(url).catch(setError); };
    void Linking.getInitialURL().then(url => { if (url) handle(url); }).catch(setError);
    const listener = Linking.addEventListener('url', ({ url }) => handle(url));
    return () => listener.remove();
  }, []);
  async function submit() {
    setError(null); setBusy(true);
    try {
      if (googleUrl) await finishGoogle(googleUrl, password);
      else if (challenge) await saveSession(challenge.kind === '2fa' ? await authApi.verifyTwoFactor(challenge.value, code) : await authApi.verifyRegistration(challenge.value, code));
      else if (mode === 'login') {
        const value = await authApi.login({ identifier, password });
        if ('twoFactorRequired' in value) setChallenge({ kind: '2fa', value: value.challenge }); else await saveSession(value);
      } else if (mode === 'register') {
        const value = await authApi.startRegistration({ firstName, lastName, dateOfBirth: birthday, gender: 'preferNotToSay', contact: identifier, password });
        setChallenge({ kind: 'registration', value: value.challengeId });
      } else if (mode === 'forgot') {
        await authApi.requestPasswordReset(identifier); setMode('reset'); setNotice('Kiểm tra email hoặc SMS để lấy mã đặt lại mật khẩu.');
      } else {
        await authApi.resetPassword(identifier.includes('@') ? { identifier, token: code, password, confirmPassword: password } : { identifier, code, password, confirmPassword: password });
        setMode('login'); setNotice('Đã đổi mật khẩu. Bạn có thể đăng nhập.');
      }
    } catch (e) { setError(e); } finally { setBusy(false); }
  }
  const title = challenge ? 'Xác minh tài khoản' : { login: 'Chào bạn trở lại', register: 'Tạo tài khoản', forgot: 'Quên mật khẩu', reset: 'Đặt lại mật khẩu' }[mode];
  const subtitle = challenge ? 'Một bước nhỏ để giữ không gian của bạn an toàn.' : mode === 'login' ? 'Những cuộc trò chuyện hay đang chờ bạn.' : 'Tạo một góc nhỏ cho những điều bạn yêu thích.';
  const switchMode = (next: 'login' | 'register' | 'forgot') => { setMode(next); setGoogleUrl(null); setChallenge(null); setError(null); setNotice(''); setCode(''); };
  const isLogin = mode === 'login' && !challenge && !googleUrl;
  return <Screen>
    <View style={authStyles.hero}>
      <View style={[authStyles.brandMark, { backgroundColor: t.primary }]}><Text style={authStyles.brandLetter}>f</Text></View>
      <Text style={[authStyles.brandName, { color: t.primary }]}>fookbase</Text>
      <Text style={[authStyles.heroCopy, { color: t.muted }]}>Kết nối, chia sẻ và theo dõi những điều bạn quan tâm.</Text>
    </View>
    <Card tone="raised" style={authStyles.formCard}>
      <View style={authStyles.formIntro}><Label title style={{ fontSize: 22, lineHeight: 27 }}>{title}</Label><Label muted style={{ marginTop: 2 }}>{subtitle}</Label></View>
      {!!notice && <View style={[authStyles.notice, { backgroundColor: `${t.success}16`, borderColor: `${t.success}44` }]}><Icon name="sparkle" color={t.success} size={17} /><Text style={{ color: t.success, flex: 1, lineHeight: 20 }}>{notice}</Text></View>}
      {!challenge && <Field label="Email, số điện thoại hoặc tên đăng nhập" autoCapitalize="none" value={identifier} onChangeText={setIdentifier} autoComplete="username" />}
      {mode === 'register' && !challenge && <><Field label="Tên" value={firstName} onChangeText={setFirstName} /><Field label="Họ" value={lastName} onChangeText={setLastName} /><Field label="Ngày sinh (YYYY-MM-DD)" value={birthday} onChangeText={setBirthday} /></>}
      {(challenge || mode === 'reset') && <Field label="Mã xác minh hoặc token khôi phục" value={code} onChangeText={setCode} autoCapitalize="none" autoComplete="one-time-code" />}
      {!challenge && mode !== 'forgot' && <><Field label="Mật khẩu" value={password} onChangeText={setPassword} secureTextEntry={!visible} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} /><Button title={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} secondary compact onPress={() => setVisible(!visible)} /></>}
      {error ? <ErrorNotice error={error} /> : null}
      <Button title={busy ? 'Đang xử lý…' : challenge ? 'Xác minh' : mode === 'login' ? 'Đăng nhập' : 'Tiếp tục'} icon={<Icon name="arrow" color="#fff" size={19} />} onPress={() => void submit()} disabled={busy || (googleUrl ? !password : challenge ? !code.trim() : !identifier.trim() || (mode !== 'forgot' && !password))} />
      {isLogin && <Pressable accessibilityRole="button" accessibilityLabel="Quên mật khẩu" onPress={() => switchMode('forgot')} style={({ pressed }) => [authStyles.forgotLink, { opacity: pressed ? 0.65 : 1 }]}><Text style={{ color: t.primary, fontWeight: '700' }}>Quên mật khẩu?</Text></Pressable>}
      {googleEnabled && !challenge && !googleUrl && <Button title="Tiếp tục với Google" icon={<Text style={{ color: t.text, fontWeight: '900', fontSize: 16 }}>G</Text>} secondary disabled={busy} onPress={() => { setBusy(true); setError(null); void beginGoogleLogin().then(url => url ? finishGoogle(url) : undefined).catch(setError).finally(() => setBusy(false)); }} />}
      {challenge?.kind === 'registration' && <Button title="Gửi lại mã" secondary disabled={busy} onPress={() => { setBusy(true); void authApi.resendRegistration(challenge.value).then(v => { setChallenge({ kind: 'registration', value: v.challengeId }); setNotice('Đã gửi lại mã.'); }).catch(setError).finally(() => setBusy(false)); }} />}
      {isLogin && <><View style={[authStyles.separator, { backgroundColor: t.border }]} /><Pressable accessibilityRole="button" accessibilityLabel="Tạo tài khoản mới" disabled={busy} onPress={() => switchMode('register')} style={({ pressed }) => [authStyles.createAccount, { backgroundColor: t.success, opacity: busy ? 0.45 : pressed ? 0.75 : 1 }]}><Text style={authStyles.createAccountText}>Tạo tài khoản mới</Text></Pressable></>}
    </Card>
    {!isLogin && <View style={[styles.row, authStyles.switchRow]}>{(['login', 'register', 'forgot'] as const).filter(v => v !== mode || challenge).map(v => <Button key={v} title={{ login: 'Đăng nhập', register: 'Tạo tài khoản', forgot: 'Quên mật khẩu' }[v]} secondary compact disabled={busy} onPress={() => switchMode(v)} />)}</View>}
    <Text style={[authStyles.footer, { color: t.subtle }]}>Fookbase · Kết nối mọi người</Text>
  </Screen>;
}

const authStyles = StyleSheet.create({
  hero: { alignItems: 'center', paddingTop: 52, paddingBottom: 30 },
  brandMark: { width: 58, height: 58, borderRadius: 16, justifyContent: 'center', alignItems: 'center' },
  brandLetter: { color: '#fff', fontSize: 45, fontWeight: '900', lineHeight: 51 },
  brandName: { fontSize: 35, fontWeight: '900', letterSpacing: -1.5, marginTop: 10 },
  heroCopy: { maxWidth: 320, fontSize: 15, lineHeight: 22, marginTop: 8, textAlign: 'center' },
  formCard: { padding: 18, gap: 14, borderRadius: 12 },
  formIntro: { marginBottom: 2, alignItems: 'center' },
  notice: { flexDirection: 'row', alignItems: 'center', gap: 8, borderWidth: 1, borderRadius: 13, padding: 11 },
  forgotLink: { alignSelf: 'center', paddingVertical: 3, paddingHorizontal: 12 },
  separator: { height: 1, marginVertical: 3 },
  createAccount: { minHeight: 48, borderRadius: 12, alignItems: 'center', justifyContent: 'center' },
  createAccountText: { color: '#fff', fontSize: 15, fontWeight: '800' },
  switchRow: { justifyContent: 'center', marginTop: 2 },
  footer: { textAlign: 'center', fontSize: 12, marginTop: 'auto', paddingTop: 18 },
});
