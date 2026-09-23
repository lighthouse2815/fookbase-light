# Zola Mobile

Zola Mobile là app React Native + Expo độc lập trong `frontend/zola-mobile`. App dùng chung
Fookbase API, PostgreSQL và SignalR với Zola Light web; app không có database hoặc backend
riêng.

## Phạm vi MVP

- Đăng nhập, đăng ký, OTP/2FA, refresh session và Google mobile login.
- Danh sách cuộc trò chuyện, chat riêng/nhóm, đọc tin và SignalR realtime.
- Tìm người để mở cuộc trò chuyện mới.
- Gửi ảnh/video trong tin nhắn.
- Thông báo trong app và đăng xuất an toàn.

Feed, reels, groups, game và admin vẫn thuộc app Fookbase Mobile tại `frontend/mobile`.

## Cấu hình local

Tạo file `frontend/zola-mobile/.env.local` từ `.env.example`:

```dotenv
EXPO_PUBLIC_API_BASE_URL=https://your-api-domain
ZOLA_ANDROID_PACKAGE=com.example.zola
ZOLA_IOS_BUNDLE=com.example.zola
EXPO_PUBLIC_MOBILE_CALLBACK_URL=https://zola.example.com/auth/callback
```

`EXPO_PUBLIC_API_BASE_URL` phải là HTTPS origin, không có path. Không đặt secret trong các
biến `EXPO_PUBLIC_*`; các giá trị này được đóng vào bundle khi build.

## Chạy và build

```bash
cd frontend/zola-mobile
npm ci
npm run typecheck
npm run lint
npm test -- --runInBand
npm run export:android
npm run android
```

`npm run android` cần development build, thiết bị hoặc emulator và Metro. Bản phát hành dùng
EAS với package Android, bundle ID iOS và signing riêng của Zola. Tăng `versionCode` trước mỗi
bản Android mới.

## Google mobile login

Backend cần bật Google và cấu hình `GoogleAuthentication__MobileCallbackUrl` trùng với
`EXPO_PUBLIC_MOBILE_CALLBACK_URL`. Callback phải là HTTPS App Link đã được xác minh. App chỉ
nhận completion code và state; access token và refresh token không nằm trong URL.

Nếu đồng thời phát hành `frontend/mobile`, hai app cần callback/App Link riêng để tránh tranh
quyền xử lý cùng một URL. Backend hiện có một mobile callback nên cần tách cấu hình callback
cho Zola trước khi phát hành cả hai app với Google login.

## Push notification

MVP hiện nhận cập nhật khi app đang mở qua SignalR và đọc thông báo đã lưu trong API. Push khi
app bị đóng cần thêm `expo-notifications`, endpoint đăng ký device token và worker gửi FCM/APNs.
