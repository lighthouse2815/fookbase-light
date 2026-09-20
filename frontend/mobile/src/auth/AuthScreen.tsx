import { useEffect, useState } from 'react';
import { Linking, View } from 'react-native';
import { authApi } from '../api/auth';
import { saveSession } from './session';
import { Button, Card, ErrorNotice, Field, Label, Screen, styles } from '../components/ui';
import { beginGoogleLogin, completeGoogleLogin, isGoogleCallback } from './google';

export function AuthScreen() {
  const [mode, setMode] = useState<'login' | 'register' | 'forgot' | 'reset'>('login');
  const [identifier, setIdentifier] = useState(''); const [password, setPassword] = useState('');
  const [visible, setVisible] = useState(false); const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState(''); const [birthday, setBirthday] = useState('');
  const [challenge, setChallenge] = useState<{ kind: 'registration' | '2fa'; value: string } | null>(null);
  const [code, setCode] = useState(''); const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false); const [error, setError] = useState<unknown>(null);
  const [googleUrl,setGoogleUrl]=useState<string|null>(null); const [googleEnabled,setGoogleEnabled]=useState(false);
  async function finishGoogle(url:string, linkPassword?:string) {
    const result=await completeGoogleLogin(url,linkPassword);
    if(result.kind==='link'){setGoogleUrl(result.url);setNotice('Nhập mật khẩu tài khoản hiện có để liên kết Google.');return;}
    setGoogleUrl(null);
    if('twoFactorRequired' in result.response)setChallenge({kind:'2fa',value:result.response.challenge});
    else await saveSession(result.response, result.generation);
  }
  useEffect(()=>{
    void authApi.providers().then(p=>setGoogleEnabled(!!p.googleMobile && !!process.env.EXPO_PUBLIC_MOBILE_CALLBACK_URL)).catch(()=>{});
    const handle=(url:string)=>{if(isGoogleCallback(url))void finishGoogle(url).catch(setError);};
    void Linking.getInitialURL().then(url=>{if(url)handle(url);}).catch(setError);
    const listener=Linking.addEventListener('url',({url})=>handle(url));return()=>listener.remove();
  },[]);
  async function submit() {
    setError(null); setBusy(true);
    try {
      if(googleUrl) await finishGoogle(googleUrl,password);
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
        await authApi.resetPassword(identifier.includes('@') ? { email: identifier, token: code, password, confirmPassword: password } : { identifier, code, password, confirmPassword: password });
        setMode('login'); setNotice('Đã đổi mật khẩu. Bạn có thể đăng nhập.');
      }
    } catch (e) { setError(e); } finally { setBusy(false); }
  }
  return <Screen><Label title>Fookbase Light</Label><Label muted>Gần nhau hơn, mỗi ngày.</Label><Card>
    <Label title>{challenge ? 'Xác minh tài khoản' : { login: 'Chào bạn trở lại', register: 'Tạo tài khoản', forgot: 'Quên mật khẩu', reset: 'Đặt lại mật khẩu' }[mode]}</Label>
    {!!notice && <Label>{notice}</Label>}
    {!challenge && <Field label="Email, số điện thoại hoặc tên đăng nhập" autoCapitalize="none" value={identifier} onChangeText={setIdentifier} autoComplete="username" />}
    {mode === 'register' && !challenge && <><Field label="Tên" value={firstName} onChangeText={setFirstName} /><Field label="Họ" value={lastName} onChangeText={setLastName} /><Field label="Ngày sinh (YYYY-MM-DD)" value={birthday} onChangeText={setBirthday} /></>}
    {(challenge || mode === 'reset') && <Field label="Mã xác minh hoặc token khôi phục" value={code} onChangeText={setCode} autoCapitalize="none" autoComplete="one-time-code" />}
    {!challenge && mode !== 'forgot' && <><Field label="Mật khẩu" value={password} onChangeText={setPassword} secureTextEntry={!visible} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} /><Button title={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} secondary onPress={() => setVisible(!visible)} /></>}
    {error ? <ErrorNotice error={error} /> : null}
    <Button title={busy ? 'Đang xử lý…' : challenge ? 'Xác minh' : mode === 'login' ? 'Đăng nhập' : 'Tiếp tục'} onPress={() => void submit()} disabled={busy || (googleUrl ? !password : challenge ? !code.trim() : !identifier.trim() || (mode !== 'forgot' && !password))} />
    {googleEnabled && !challenge && !googleUrl && <Button title="Tiếp tục với Google" secondary disabled={busy} onPress={()=>{setBusy(true);setError(null);void beginGoogleLogin().then(url=>url?finishGoogle(url):undefined).catch(setError).finally(()=>setBusy(false));}} />}
    {challenge?.kind === 'registration' && <Button title="Gửi lại mã" secondary disabled={busy} onPress={() => { setBusy(true); void authApi.resendRegistration(challenge.value).then(v => { setChallenge({ kind: 'registration', value: v.challengeId }); setNotice('Đã gửi lại mã.'); }).catch(setError).finally(() => setBusy(false)); }} />}
  </Card><View style={styles.row}>{(['login', 'register', 'forgot'] as const).filter(v => v !== mode || challenge).map(v => <Button key={v} title={{ login: 'Đăng nhập', register: 'Tạo tài khoản', forgot: 'Quên mật khẩu' }[v]} secondary disabled={busy} onPress={() => { setMode(v); setGoogleUrl(null); setChallenge(null); setError(null); setNotice(''); setCode(''); }} />)}</View></Screen>;
}
