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
ZOLA_ANDROID_PACKAGE=com.fookbase.zola
ZOLA_IOS_BUNDLE=com.fookbase.zola
EXPO_PUBLIC_MOBILE_CALLBACK_URL=https://zola.example.com/auth/callback
# Chỉ cần khi dùng EAS project khác project Zola mặc định.
# EXPO_PUBLIC_EAS_PROJECT_ID=your-eas-project-id
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

Backend cần bật Google và cấu hình `GoogleAuthentication__ZolaMobileCallbackUrl` trùng với
`EXPO_PUBLIC_MOBILE_CALLBACK_URL`. Callback phải là HTTPS App Link đã được xác minh. App chỉ
nhận completion code và state; access token và refresh token không nằm trong URL.

`frontend/mobile` dùng `GoogleAuthentication__MobileCallbackUrl`, còn Zola dùng
`GoogleAuthentication__ZolaMobileCallbackUrl`; hai app không dùng chung callback/App Link.

## Push notification

MVP gửi push qua Expo Push Service khi app bị đóng. App đăng ký Expo push token tại
`POST /api/notifications/push-tokens/zola`; API lưu token, gửi thông báo tin nhắn mới và kiểm
tra Expo receipt sau ít nhất 15 phút để vô hiệu token `DeviceNotRegistered`.

Để bật trên production, liên kết app với EAS, tạo credentials FCM/APNs trong EAS và đặt:

```dotenv
# Chỉ cần khi dùng EAS project khác project Zola mặc định.
# EXPO_PUBLIC_EAS_PROJECT_ID=<EAS project UUID>
PushNotifications__Enabled=true
PushNotifications__ReceiptCheckIntervalMinutes=15
```

Remote push không chạy trong Expo Go từ SDK 53; dùng development build hoặc build phát hành.
Expo Push không đảm bảo exactly-once, vì vậy app luôn tải lại HTTP data sau SignalR/reconnect.

## APK preview trên GitHub Actions

Workflow thủ công **Zola Android preview APK** kiểm tra app và tạo APK nội bộ, giữ artifact 14 ngày.
Trước khi chạy, tạo GitHub repository variables sau:

```text
ZOLA_MOBILE_API_BASE_URL=https://your-api-domain
ZOLA_MOBILE_ANDROID_PACKAGE=com.fookbase.zola
ZOLA_MOBILE_CALLBACK_URL=https://zola.example.com/auth/callback
ZOLA_MOBILE_EAS_PROJECT_ID=<EAS project UUID>
```

Callback và EAS project ID override là tùy chọn nếu chưa bật Google/push, nhưng API URL và Android package
là bắt buộc. APK dùng development signing, chỉ phù hợp phân phối nội bộ; phát hành store dùng EAS
với signing credentials riêng và tăng `android.versionCode`.
