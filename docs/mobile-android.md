# Fookbase Light Mobile

App React Native + Expo ở `frontend/mobile`, độc lập với web. Dùng Node 24 và npm 11 để tương thích công cụ tạo Expo; React/React Native theo Expo SDK 57.

## Cấu hình

Tạo `frontend/mobile/.env.local` (không commit) với `EXPO_PUBLIC_API_BASE_URL` là origin API HTTPS thật. App hiển thị lỗi cấu hình nếu thiếu, không tự kết nối endpoint giả. `FOOKBASE_ANDROID_PACKAGE` và `FOOKBASE_IOS_BUNDLE` đặt application ID thuộc tổ chức trước phát hành. ID `dev.fookbase.light` chỉ dùng phát triển nội bộ.

## Chạy và kiểm tra

```bash
cd frontend/mobile
npm ci
npm run typecheck
npm run lint
npm test -- --runInBand
npm run android
```

Android cần Java/Android SDK và thiết bị hoặc emulator. `npm run android` tạo development build dùng Metro. Native files được Expo sinh từ app.config.ts và không commit; không sửa trực tiếp generated files.

## Trạng thái triển khai

Đang triển khai theo [kế hoạch](superpowers/plans/2026-09-20-fookbase-light-mobile.md). Chưa có APK phát hành hoặc kết quả QA thiết bị. Google mobile và deep links cần cấu hình domain/certificate và hợp đồng backend riêng.
