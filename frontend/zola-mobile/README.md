# Zola Mobile

Ứng dụng chat React Native + Expo độc lập cho Zola. App dùng chung API Fookbase,
không tạo database hoặc backend riêng.

## Cấu hình

Tạo `frontend/zola-mobile/.env.local` (không commit):

```dotenv
EXPO_PUBLIC_API_BASE_URL=https://your-api-domain
ZOLA_ANDROID_PACKAGE=com.example.zola
ZOLA_IOS_BUNDLE=com.example.zola
# Cần HTTPS App Link khi bật Google mobile login.
EXPO_PUBLIC_MOBILE_CALLBACK_URL=https://zola.example.com/auth/callback
```

`EXPO_PUBLIC_API_BASE_URL` phải là HTTPS origin, không có path. Các biến
`EXPO_PUBLIC_*` được đóng vào bundle và không được chứa secret.

## Chạy và kiểm tra

```bash
npm ci
npm run typecheck
npm run lint
npm test -- --runInBand
npm run export:android
npm run android
```

Development build cần thiết bị/emulator Android hoặc iOS. Bản production dùng
EAS với package/bundle ID và signing riêng của Zola.

## Phạm vi MVP

- Đăng nhập, đăng ký, refresh session và Google mobile login.
- Danh sách cuộc trò chuyện, chat riêng/nhóm, đọc tin và SignalR realtime.
- Tìm người để mở cuộc trò chuyện mới.
- Gửi ảnh/video trong tin nhắn.
- Thông báo trong app và quản lý phiên.

Push notification khi app bị đóng cần bổ sung Expo Notifications cùng endpoint
đăng ký device token ở backend.
