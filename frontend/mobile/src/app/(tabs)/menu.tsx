import { Button, Card, Label, Screen } from '../../components/ui';
import { authApi } from '../../api/auth';
import { clearSession, getSession } from '../../auth/session';
import { router } from 'expo-router';
export default function Account() {
 return <Screen><Label title>Xin chào, {getSession()?.user.username}</Label><Label>Không gian của bạn trên Fookbase.</Label>
 {[[ '/feed', 'Bảng tin' ], ['/messages', 'Tin nhắn'], ['/notifications', 'Thông báo'], ['/search', 'Tìm kiếm'], [`/profile/${getSession()?.user.id}`, 'Trang cá nhân'], ['/friends', 'Bạn bè'], ['/groups', 'Nhóm']]].map(([path, title]) => <Button key={path} title={title} secondary onPress={() => router.push(path)} />)}
 <Card><Button title="Đăng xuất" onPress={() => { const token = getSession()?.refreshToken; void (token ? authApi.logout(token) : Promise.resolve()).finally(() => clearSession()).catch(() => {}); }} /></Card></Screen>;
}
