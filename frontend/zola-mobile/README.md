# Zola Mobile

Ứng dụng chat React Native + Expo độc lập cho Zola. App dùng chung API Fookbase,
không tạo database hoặc backend riêng.

## Cấu hình

Tạo `frontend/zola-mobile/.env.local` (không commit):

```dotenv
EXPO_PUBLIC_API_BASE_URL=https://your-api-domain
ZOLA_ANDROID_PACKAGE=com.fookbase.zola
ZOLA_IOS_BUNDLE=com.fookbase.zola
# Cần HTTPS App Link khi bật Google mobile login.
EXPO_PUBLIC_MOBILE_CALLBACK_URL=https://zola.example.com/auth/callback
# Optional override when using another EAS project:
# EXPO_PUBLIC_EAS_PROJECT_ID=your-eas-project-id
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

Push notification dùng Expo Notifications khi app bị đóng. Cần development/production build
đã liên kết EAS, cấu hình FCM/APNs trong EAS và bật
`PushNotifications__Enabled=true` ở API.
