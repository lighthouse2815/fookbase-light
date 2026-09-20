import { Button, Card, Label, Screen } from '../components/ui';
import { authApi } from '../api/auth';
import { clearSession, getSession } from '../auth/session';
export default function Account() {
 return <Screen><Label title>Xin chào, {getSession()?.user.username}</Label><Card><Label>Bạn đã đăng nhập Fookbase Light.</Label><Button title="Đăng xuất" onPress={() => { const token = getSession()?.refreshToken; void (token ? authApi.logout(token) : Promise.resolve()).finally(() => clearSession()).catch(() => {}); }} /></Card></Screen>;
}
