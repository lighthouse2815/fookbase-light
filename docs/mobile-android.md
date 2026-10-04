# Fookbase Light Mobile — Android

App React Native + Expo SDK 57 ở `frontend/mobile`, độc lập với web, dùng cùng API và tài khoản. Node 24, npm, Java 21 và Android SDK 36. Native project được sinh bởi Expo; không sửa trực tiếp thư mục `android`.

## Chức năng đã triển khai

- Đăng nhập/đăng ký, OTP/2FA, quên mật khẩu, SecureStore, refresh và đăng xuất.
- Feed, bài viết/ảnh/video, bình luận, thích, chia sẻ và lưu bài.
- Hồ sơ, tìm kiếm người dùng, bạn bè, follow/block, nhóm và duyệt tham gia.
- Chat riêng/nhóm, media, lịch sử, đọc tin, quản lý thành viên/quyền và SignalR.
- Thông báo trong app, Reels, stories, AI chat, privacy, phiên đăng nhập và bảo mật.
- Google mobile có endpoint riêng, state/PKCE, mã dùng một lần và callback HTTPS.

Đây là phạm vi release đầu, chưa bao gồm push khi app đóng, iOS, game, page/event/album/memories/birthday hoặc admin.

## Cấu hình bắt buộc để kết nối

Tạo `frontend/mobile/.env.local`, không commit:

```dotenv
EXPO_PUBLIC_API_BASE_URL=https://your-api-domain
FOOKBASE_ANDROID_PACKAGE=your.organization.fookbase
# Chỉ đặt khi đã cấu hình Google và verified App Link:
EXPO_PUBLIC_MOBILE_CALLBACK_URL=https://your-link-domain/auth/callback
```

Các giá trị trên là mô tả cần thay bằng domain/application ID thật, không phải endpoint chạy thử. API phải là HTTPS origin, không có path. Thiếu cấu hình API, app hiển thị lỗi cấu hình. `dev.fookbase.light` là ID mặc định cho phát triển nội bộ. `EXPO_PUBLIC_*` được đóng vào bundle và không được chứa secret. Đổi cấu hình cần build lại APK.

## Chạy, kiểm tra và build

```bash
cd frontend/mobile
npm ci
npx expo-doctor
npm run typecheck
npm run lint
npm test -- --runInBand
npm run export:android
# Development build, cần thiết bị/emulator và Metro:
npm run android
# APK có JavaScript đóng gói, chạy không cần Metro:
npx expo prebuild --platform android --no-install
cd android
CMAKE_BUILD_PARALLEL_LEVEL=1 ./gradlew assembleRelease --no-daemon --max-workers=1 -PreactNativeArchitectures=arm64-v8a,x86_64
```

Lệnh preview trên nhắm ARM64 và emulator x86_64; chưa nghiệm thu thiết bị 32-bit. APK ở `android/app/build/outputs/apk/release/app-release.apk`. Gradle template ký bản này bằng khóa development: chỉ phân phối nội bộ, không dùng để phát hành cửa hàng. Build đầu tải NDK/CMake và có thể mất nhiều thời gian.

CI thường chạy Doctor/type/lint/Jest/export. Workflow thủ công **Android preview APK** build và giữ APK 14 ngày; đặt GitHub repository variables `MOBILE_API_BASE_URL`, `MOBILE_ANDROID_PACKAGE`, tùy chọn `MOBILE_CALLBACK_URL` trước khi chạy. Không tự publish store.

`eas.json` có development, preview APK và production AAB. Nếu dùng EAS cần đăng nhập/liên kết project của chủ dự án, cấu hình môi trường và signing trong secret store. Tăng `android.versionCode` trước mỗi bản phát hành mới; không commit keystore/password.

## Google và App Links

Backend cần cấu hình Google hiện có và `GoogleAuthentication__MobileCallbackUrl` trùng `EXPO_PUBLIC_MOBILE_CALLBACK_URL`. Endpoint start là `/api/auth/google/mobile/start`; Google OAuth redirect URI là `/signin-google`, theo middleware backend. App nhận callback chứa mã một lần và state, không chứa access/refresh token.

Domain callback cần phục vụ `/.well-known/assetlinks.json` qua HTTPS với application ID và SHA-256 certificate đúng của APK. Với khóa development, lấy fingerprint bằng `keytool -list -v -keystore android/app/debug.keystore -alias androiddebugkey -storepass android`. Production dùng fingerprint Play App Signing hoặc khóa phát hành tương ứng. Không dùng certificate mẫu. Cho đến khi domain/certificate được thiết lập và thử trên thiết bị, Google mobile chưa được nghiệm thu.

## Kiểm thử và giới hạn bàn giao

Các test JS kiểm tra session, refresh, upload, Google state, logout, saved state, UI và cleanup realtime. Backend có integration tests Google mobile (verifier sai, replay, chống dùng callback web) chạy bằng `bash scripts/test-backend.sh` với PostgreSQL riêng.

Cần nghiệm thu trên API HTTPS thật với hai tài khoản và Android thật: đăng nhập/refresh/kill/logout, media picker/upload, gửi nhận chat/nhóm/reconnect, bàn phím/chữ lớn, phát video/background, Google cold callback/2FA và upgrade APK. Unit tests và export không thay cho kiểm thử native. Chưa có domain API, callback, package ID chính thức và signing từ chủ dự án thì không coi bản build nội bộ là bản production hoàn thành.

### Bản preview đã xác minh ngày 20/09/2026

- Artifact cục bộ: `frontend/mobile/artifacts/fookbase-light-1.0.0-preview.apk` (không commit vào Git).
- SHA-256: `1bd90798f487c512a3f707a5328c3124b9203a02a060906439fd4b4274db2b47`.
- Package/version: `dev.fookbase.light` / `1.0.0`; chữ ký APK v2 bằng khóa Android Debug.
- ABI: ARM64 và x86_64; bundle Hermes nằm trong APK và chạy không cần Metro.
- Đã cài và cold-start trên emulator Android 16/API 36. Activity mở thành công, không có fatal exception; app hiển thị đúng lỗi thiếu API vì bản preview này chưa được cung cấp URL production.
