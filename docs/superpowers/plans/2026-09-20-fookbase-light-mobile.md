# Fookbase Light Mobile — Implementation Plan

> **For agentic workers:** Khi được yêu cầu triển khai, dùng `superpowers:executing-plans` để thực hiện từng task. Chỉ dùng `superpowers:subagent-driven-development` nếu người dùng chọn cách giao việc cho subagent. Các checkbox bên dưới theo dõi tiến độ triển khai, không phải trạng thái đã hoàn thành.

**Goal:** Đưa web người dùng Fookbase Light lên app Android bằng Capacitor, giữ các chức năng hiện có và bảo đảm thao tác tốt trên điện thoại; mở rộng iOS sau.

**Architecture:** Đặt cấu hình Capacitor và project Android trong `frontend/web`, đóng gói kết quả Vite `dist` vào ứng dụng. Tái sử dụng React, router, API client và ASP.NET Core API hiện tại. Chỉ bổ sung tích hợp native tại những chỗ cần thiết: lưu phiên an toàn, vòng đời app, nút Back, liên kết và vùng hiển thị hệ thống.

**Tech Stack:** React 19, TypeScript 6, Vite 8, Tailwind CSS 4, React Router 7, Capacitor 8, Android Studio/Gradle; backend .NET 10 và SignalR hiện có.

**Spec:** Phạm vi và quyết định thiết kế trong mục 1–3 của tài liệu này; tham chiếu [kiến trúc backend](../../modular-monolith.md), [Zola Light](../../zola-light-v1.md), [triển khai production](../../production-deployment.md). Khi tài liệu cũ khác implementation, đối chiếu source trước khi làm.

**Trạng thái:** Kế hoạch ngày 20/09/2026. Người dùng đã chọn **Capacitor, tận dụng web, Android trước**. Việc tạo file này chưa bao gồm cài dependency, sửa app hoặc phát hành lên store.

## Global Constraints

- Tái sử dụng `frontend/web`; không nhân bản sang một frontend mobile hoặc đổi sang Ionic UI/React Native.
- Giữ API, phân quyền và database hiện tại trong bản Android nội bộ. Thay đổi schema cho push notification thuộc giai đoạn riêng.
- Giao diện tiếng Việt, giữ dark/light theme và mọi hành động quan trọng đang có trên web.
- Release dùng bundle cục bộ, API/media HTTPS; không dùng `server.url`, wildcard điều hướng hoặc cleartext trong cấu hình release.
- Không lưu refresh token native trong localStorage, Preferences, URL hoặc log. Không đóng gói secret của backend vào ứng dụng.
- Không tạo SSO bằng cách truyền token sang Zola Light qua query string hoặc giả định hai ứng dụng dùng chung localStorage.
- Không tự tạo hệ thống offline sync, hàng đợi gửi bài hoặc background service trong V1.
- Mỗi task hoàn chỉnh: kiểm tra diff, chạy check liên quan, commit riêng bằng tiếng Việt có dấu và push remote GitHub hiện có. Không gom thay đổi ngoài phạm vi.

## Review Focus

1. App bị hệ điều hành đóng giữa lúc refresh: khởi động lại không dùng refresh token cũ hoặc gửi request trước khi phục hồi phiên — Task 3.
2. Mất mạng khi mở lại app: giữ phiên để thử lại, không tự đăng xuất; sau reconnect không nhân đôi tin nhắn/thông báo — Task 3 và 4.
3. Bàn phím, thanh điều hướng hệ thống và chữ lớn che nút: vẫn bấm được Gửi, Đăng, Đóng và menu tài khoản — Task 5.
4. Chuyển Zola hoặc mở URL lạ: app không rời khỏi bundle ngoài ý muốn, không lộ token, Back quay về đúng màn hình — Task 4 và 6.
5. Chọn video lớn, hủy chọn file hoặc app bị đưa xuống nền khi upload: không báo thành công giả, không tạo bài tham chiếu media chưa hoàn tất — Task 5.

## 1. Phạm vi sản phẩm

### Mốc A — Android nội bộ sử dụng được

- Cài APK trên thiết bị thật; mở được màn hình đăng nhập và tải dữ liệu từ API staging/production đã cấu hình.
- Dùng lại đăng nhập mật khẩu, đăng ký, OTP/2FA, khôi phục tài khoản mà web/API đang hỗ trợ; kiểm thử từng luồng đang bật trên môi trường thử nghiệm.
- Feed, xem/đăng bài, bình luận, reaction, chia sẻ nội bộ, ảnh/video, Reels, hồ sơ, bạn bè, nhóm, tìm kiếm, thông báo trong app và AI chat dùng API hiện tại.
- Giữ các trang phụ đang có trong router, không loại bỏ tính năng để làm menu mobile ngắn hơn.
- Giữ chat nổi hiện có. Nút mở đầy đủ Zola Light mở bằng trình duyệt hệ thống, có mô tả cần phiên đăng nhập riêng.
- Phục hồi phiên, xử lý mất mạng, upload thất bại, Back, bàn phím và vùng an toàn trên Android.
- Google login trong app là cổng chức năng riêng tại mục 7. Bản nội bộ chỉ dành cho tài khoản có phương thức đăng nhập đã được kiểm thử; không quảng bá hỗ trợ đầy đủ tài khoản Google ở mốc này.

### Mốc B — Android sẵn sàng phát hành

- Hoàn thành ma trận kiểm thử, signing, versionCode/versionName, CI và tài liệu build.
- Hoàn tất Google login native nếu phát hành cho các tài khoản Google hiện có; không để nút dẫn vào luồng OAuth web không chạy được trong app.
- Chốt trải nghiệm Zola mở ngoài là phạm vi chấp nhận được; nếu cần chat đầy đủ ngay trong app, lập và triển khai hạng mục tích hợp chat trước mốc phát hành.
- Rà soát yêu cầu store tại thời điểm nộp, trang quyền riêng tư, quản lý tài khoản, quyền thiết bị và thông tin phát hành. Việc build AAB thành công không đồng nghĩa store đã duyệt.

### Mốc C — Mở rộng

- Push notification khi app ở nền/đã đóng, sau khi có thiết kế device registration và backend gửi push.
- iOS/TestFlight trên macOS, sau khi Android ổn định.
- Chụp ảnh, chia sẻ qua menu hệ thống hoặc biometric chỉ thêm khi có nhu cầu cụ thể; không cài trước toàn bộ plugin.

## 2. Hiện trạng đã đối chiếu với source

| Thành phần | Source hiện tại | Hệ quả khi làm mobile |
| --- | --- | --- |
| Build web | `frontend/web/package.json`, `vite.config.ts` | Có Vite/Tailwind; proxy `/api`, `/hubs` chỉ phục vụ dev, không đi theo APK. |
| Bootstrap/router | `src/main.tsx`, `src/App.tsx`, `src/routes/index.tsx` | Có lazy routes và BrowserRouter; cần thử route sau reload/cold start trong WebView. |
| Phiên | `src/auth/session.ts`, `src/auth/AuthProvider.tsx` | Đọc phiên đồng bộ từ localStorage; phải thêm bước khởi tạo bất đồng bộ trước khi mount app native. |
| API | `src/api/client.ts` | Đọc access token riêng từ localStorage; đã có single-flight refresh nhưng đang xóa phiên khi refresh lỗi bất kỳ, kể cả lỗi mạng. |
| Realtime | `src/realtime/RealtimeProvider.tsx` | SignalR dùng token từ session; cần phối hợp refresh khi resume và tải lại dữ liệu HTTP. |
| Google | `src/pages/auth/LoginPage.tsx`, `src/api/auth.ts` | Frontend exchange dùng `client: 'web'`; không phải hợp đồng mobile sẵn có. |
| Allowlist Google | `backend/Fookbase.Src/Main/Code/Modules/Identity/Config/GoogleAuthenticationOptions.cs` | Có client web/zola-light; thêm mobile cần hợp đồng callback riêng. |
| Chat đầy đủ | `src/pages/ZolaLightRedirect.tsx` | Đang `window.location.replace` sang URL Zola, mặc định localhost:5175; phải tách hành vi native. |
| Media | `src/api/media.ts` | Tạo upload intent → POST FormData qua XHR tới storage → gọi complete. Giữ hợp đồng này, không đổi thành PUT theo tài liệu cũ. |
| Production CORS | `backend/Fookbase.Src/Main/Code/Shared/Config/ProductionConfigurationValidator.cs` | Chỉ chấp nhận origin HTTPS; Android mặc định phù hợp, origin iOS cần xử lý ở giai đoạn iOS. |
| CI | `.github/workflows/ci.yml` | Đã build/lint ba frontend, kiểm thử backend; thêm job Android, không thay thế job hiện tại. |

Các đường dẫn `src/...` trong bảng thuộc `frontend/web`.

## 3. Thiết kế và cấu hình cần thống nhất

### 3.1. Shell native và môi trường

Chọn Capacitor 8, Node 24 theo CI hiện tại. Khóa phiên bản package bằng lockfile; kiểm tra peer dependencies trước khi cài. Android Studio tối thiểu 2025.2.1 theo tài liệu Capacitor 8; dùng SDK/JDK và Gradle wrapper phù hợp template đã chọn, ghi phiên bản cụ thể vào hướng dẫn build. iOS cần macOS và Xcode tối thiểu 26.0 theo tài liệu hiện tại. [Nguồn môi trường Capacitor](https://capacitorjs.com/docs/getting-started/environment-setup).

Thông tin cần chủ dự án cung cấp **khi bắt đầu tạo project native**, không cần để hoàn thành file plan:

| Thông tin | Cách dùng |
| --- | --- |
| Application ID thuộc tên miền/tổ chức sở hữu | Gán cố định khi sinh project; không phát hành với ID ví dụ. |
| API HTTPS cho môi trường thử và production | Giá trị `VITE_API_BASE_URL`; không dùng localhost của máy phát triển trên thiết bị. |
| URL web và Zola HTTPS | `VITE_WEB_URL`, `VITE_ZOLA_LIGHT_URL`; deep links và mở trình duyệt. |
| Icon/brand được chọn | Sinh icon và splash từ asset hiện có bằng công cụ, không vẽ thủ công nhiều kích thước. |
| Chủ sở hữu signing key và tài khoản phát hành | Giữ khóa ngoài Git, phân biệt APK debug với bản ký để phân phối. |

Cấu hình đích dùng `webDir: 'dist'`, hostname mặc định và Android scheme `https`, origin dự kiến `https://localhost`. Xác nhận origin thực tế từ thiết bị rồi thêm chính xác vào CORS API; không nhầm origin WebView với địa chỉ API. Cấu hình release không tải website từ xa bằng `server.url`. [Nguồn cấu hình Capacitor](https://capacitorjs.com/docs/config).

Các biến `VITE_*` là thông tin công khai nằm trong bundle. Thay môi trường phải build và sync lại; không phát hành APK staging dưới nhãn production. Giữ fetch/XHR và WebSocket hiện tại, không bật native HTTP để né lỗi CORS.

### 3.2. Phiên đăng nhập

Ưu tiên `@aparajita/capacitor-secure-storage`, đã có nhánh hỗ trợ Capacitor 8, dùng Android Keystore/iOS Keychain. Kiểm tra phiên bản, license và build Android trước khi chốt dependency. Chỉ gọi plugin trên native; web giữ cơ chế hiện tại. [Nguồn plugin](https://github.com/aparajita/capacitor-secure-storage).

Mở rộng module session hiện có, không tạo nhiều lớp repository/adapter. Native giữ session đang hoạt động trong bộ nhớ và lưu phiên trong secure storage. Dùng `AuthenticationResponse` hiện có làm dữ liệu, validate payload đọc từ storage. `main.tsx` chờ hydrate xong mới mount AuthProvider, router và SignalR. Lỗi đọc storage phải hiện trạng thái có thể thử lại; không âm thầm hạ xuống localStorage.

Hợp đồng mục tiêu trong `src/auth/session.ts`:

```ts
export type AuthSession = AuthenticationResponse
export function initializeAuthSession(): Promise<void>
export function getAuthSession(): AuthSession | null
export function saveAuthSession(session: AuthSession): Promise<void>
export function clearAuthSession(): Promise<void>
```

Chuyển các call site lưu/xóa sang await; giữ `getAuthSession()` đồng bộ sau hydrate cho provider/SignalR. Chỉ phát `authSessionChangedEvent` theo trạng thái đã được xử lý nhất quán. Serialize các lần ghi và dùng session generation để response refresh cũ không khôi phục tài khoản vừa logout/đổi user. Nếu ghi token mới thất bại, không báo đăng nhập bền vững thành công; giữ trạng thái lỗi có hướng phục hồi, không bỏ qua lỗi.

Refresh do timer, 401 và resume phải dùng cùng một promise đang chạy. Network error/timeout/5xx giữ phiên và cho thử lại; chỉ xóa khi backend xác nhận refresh không hợp lệ hoặc người dùng logout. Không tự retry thao tác tạo bài/gửi tin nhắn nếu chưa biết server đã nhận hay chưa.

### 3.3. UX và vòng đời

- Dùng header/menu đã có. Giữ ô tìm kiếm, tạo nội dung, thông báo, chat và tài khoản truy cập được trên màn hình nhỏ.
- Tính `env(safe-area-inset-*)` vào layout dùng chung; `--app-header-height` phải phản ánh chiều cao thực tế, không cộng safe area hai lần.
- Back: bàn phím đóng trước; sau đó overlay/dialog đang mở; sau đó route trước; tại feed gốc thu nhỏ app theo hành vi Android. Không gọi exit mỗi lần bấm Back.
- Khi resume, kiểm tra phiên trước khi mở lại SignalR; fetch lại unread và nội dung cần cập nhật bằng API hiện có. Không xem kết nối SignalR là dịch vụ push chạy nền.
- Đăng ký listener có cleanup theo từng handle, kể cả React StrictMode; tránh `removeAllListeners()` làm mất listener của module khác. Capacitor cung cấp các sự kiện vòng đời và API lấy launch URL. [Nguồn App plugin](https://capacitorjs.com/docs/apis/app).
- Liên kết ngoài mở bằng `@capacitor/browser`. Verified App Links chỉ cho domain đã xác minh; đọc URL cả lúc cold start và khi app đang chạy. Path lạ không được điều hướng hoặc thực thi.

## 4. Bản đồ file triển khai

| Tạo/sửa | File | Trách nhiệm |
| --- | --- | --- |
| Tạo | `frontend/web/capacitor.config.ts` | ID/name, bundle và cấu hình native. |
| Sinh bằng CLI | `frontend/web/android/` | Project Android, manifest, Gradle, tài nguyên icon/splash. |
| Sửa | `frontend/web/package.json`, `package-lock.json` | Capacitor, plugin cần thiết, scripts kiểm thử/build mobile. |
| Tạo | `frontend/web/scripts/check-mobile-env.mjs` | Từ chối cấu hình endpoint mobile sai trước build. |
| Tạo | `frontend/web/tests/mobile-env.test.mjs` | Kiểm thử cấu hình bằng Node test runner hiện có. |
| Tạo | `frontend/web/vitest.config.ts` | Test session và lifecycle với jsdom; giữ test Flappy hiện tại. |
| Sửa | `frontend/web/src/auth/session.ts`, `src/auth/AuthProvider.tsx`, `src/api/client.ts`, `src/main.tsx` | Hydrate, persistence, refresh và token access. |
| Tạo | `frontend/web/src/auth/session.test.ts`, `src/api/client.test.ts` | Lỗi storage, race logout, refresh, offline. |
| Tạo | `frontend/web/src/native/MobileLifecycle.tsx`, `src/native/links.ts` | Bridge vòng đời trong router và bộ lọc deep link. |
| Tạo | `frontend/web/src/native/MobileLifecycle.test.tsx`, `src/native/links.test.ts` | Cleanup, resume, Back và URL không hợp lệ. |
| Sửa | `frontend/web/src/routes/index.tsx`, `src/layout/MainLayout.tsx`, `src/layout/TopNavbar.tsx` | Gắn bridge và hành vi điều hướng theo platform. |
| Sửa khi có lỗi tái hiện | `frontend/web/index.html`, `src/index.css`, `src/shared/components/AppDialog.tsx`, `src/pages/feed/components/NewPostBox.tsx`, `src/pages/reels/ReelsPage.tsx` | Viewport, safe area, bàn phím, dialog và media. |
| Sửa | `frontend/web/src/pages/ZolaLightRedirect.tsx`, `src/pages/auth/LoginPage.tsx` | Zola mở ngoài, thông tin Google trong bản nội bộ. |
| Tạo | `frontend/web/src/pages/ZolaLightRedirect.test.tsx` | Native/web redirect và URL lỗi. |
| Sửa | `.github/workflows/ci.yml` | Job build/test Android. |
| Tạo | `docs/mobile-android.md` | Hướng dẫn môi trường, build, signing, kết quả QA, giới hạn bản phát hành. |

Trong các ô có nhiều đường dẫn, đường dẫn bắt đầu `src/` thuộc `frontend/web`. Không tạo trước file chỉ để trống; tạo cùng task có implementation và test sử dụng nó.

## 5. Các task cho mốc Android nội bộ

### Task 1 — Kiểm tra đầu vào và chặn build sai môi trường

**Files:** `frontend/web/scripts/check-mobile-env.mjs`, `frontend/web/tests/mobile-env.test.mjs`, `frontend/web/package.json`, `docs/mobile-android.md`.

**Interfaces:** Export `validateMobileEnvironment(env: Record<string, string | undefined>): void`; ném lỗi chứa tên biến bị thiếu/sai, không in toàn bộ giá trị môi trường.

- [ ] Chạy baseline từ `frontend/web`: `npm ci`, `npm run lint`, `npm run build`, `npm run test:flappy`; ghi rõ lỗi tồn tại trước thay đổi.
- [ ] Ghi Application ID, endpoint và bộ công cụ đã thống nhất vào hướng dẫn build; không ghi password/keystore vào tài liệu.
- [ ] Viết test và chạy `node --test tests/mobile-env.test.mjs`, xác nhận fail vì thiếu validator.

```js
import assert from 'node:assert/strict'
import test from 'node:test'
import { validateMobileEnvironment } from '../scripts/check-mobile-env.mjs'

const valid = {
  VITE_API_BASE_URL: 'https://api.example.test',
  VITE_WEB_URL: 'https://web.example.test',
  VITE_ZOLA_LIGHT_URL: 'https://chat.example.test',
}
test('chấp nhận endpoint HTTPS công khai', () => {
  assert.doesNotThrow(() => validateMobileEnvironment(valid))
})
test('chặn endpoint thiếu hoặc chỉ dùng được trên máy phát triển', () => {
  for (const value of ['', '/api', 'http://api.example.test', 'https://localhost', 'https://127.0.0.1']) {
    assert.throws(() => validateMobileEnvironment({ ...valid, VITE_API_BASE_URL: value }))
  }
})
```

- [ ] Validator dùng `new URL`, yêu cầu HTTPS, hostname không loopback, không credentials/query/fragment. Endpoint API là origin; kiểm tra cả ba biến. Thêm IPv6 loopback, username/password và các biến web/Zola vào test.
- [ ] Script CLI dùng `loadEnv('mobile', process.cwd(), 'VITE_')` của Vite kết hợp process env; npm script `build:mobile` chạy validator rồi `tsc -b && vite build --mode mobile`. Không chỉ đọc process env khiến `.env.mobile.local` bị bỏ qua.
- [ ] Chạy lại test/build/lint; commit `chore: chuẩn bị cấu hình build Android` và push.

### Task 2 — Đóng gói web thành APK chạy được

**Files:** `frontend/web/capacitor.config.ts`, `frontend/web/android/`, `frontend/web/package.json`, `frontend/web/package-lock.json`, `docs/mobile-android.md`.

**Interfaces:** `npm run build:mobile` tạo `dist/index.html`; `npx cap sync android` copy bundle đó vào project native. Cấu hình đã qua Task 1 là đầu vào.

- [ ] Kiểm tra metadata/peer dependencies của `@capacitor/core`, `cli`, `android`, `app`, `browser` dòng 8; chọn phiên bản tương thích và lưu exact version/lockfile. Dùng npm hiện có.
- [ ] Sinh project bằng Capacitor CLI với ID thật đã thống nhất. Cấu hình chính:

```ts
import type { CapacitorConfig } from '@capacitor/cli'

const appId = process.env.FOOKBASE_MOBILE_APP_ID
if (!appId) throw new Error('Thiếu FOOKBASE_MOBILE_APP_ID')

export default {
  appId,
  appName: 'Fookbase Light',
  webDir: 'dist',
  loggingBehavior: 'debug',
} satisfies CapacitorConfig
```

Giữ ID đã sinh cố định trong project Android; script/CI phải đối chiếu biến với applicationId native để tránh tưởng đổi env là đổi ID. Không thêm `server.url` vào config này.

- [ ] Thêm đúng origin WebView vào cấu hình triển khai CORS API; thử preflight, bearer API, SignalR và upload storage từ thiết bị qua HTTPS. Không mở `AllowAnyOrigin`.
- [ ] Chạy từ `frontend/web`:

```bash
npm run build:mobile
npx cap add android
npx cap sync android
cd android
./gradlew assembleDebug
```

`cap add` chỉ chạy lần đầu. Các lần sau bắt đầu bằng build và sync.

- [ ] Cài `android/app/build/outputs/apk/debug/app-debug.apk`; thử khởi động, route `/feed`, reload route chi tiết và mở khi API mất mạng. Kỳ vọng bundle vẫn hiển thị được trạng thái lỗi/thử lại, không màn hình trắng.
- [ ] Kiểm tra manifest/release config không có cleartext, remote server hoặc secret; commit `feat: đóng gói Fookbase Light bằng Capacitor Android` và push.

### Task 3 — Lưu phiên native và refresh nhất quán

**Files:** Các file auth/API/bootstrap ở mục 4; tìm mọi call site bằng `rg 'saveAuthSession|clearAuthSession|getAuthSession|fookbase.accessToken' frontend/web/src` và sửa các caller liên quan.

**Interfaces:** Hợp đồng session ở mục 3.2; export `refreshAuthSession(): Promise<string | null>` từ `src/api/client.ts` để timer, 401 và resume dùng chung. Hàm trả null khi không có phiên/refresh bị từ chối; lỗi tạm thời phải phân biệt bằng `ApiError.status` và không xóa phiên.

- [ ] Thêm Vitest và jsdom tương thích Vite đang dùng làm devDependencies để kiểm thử async session/race condition; script `test:mobile` = `vitest run`. Cấu hình chỉ chạy `src/**/*.test.ts` và `src/**/*.test.tsx`; không nuốt test `.mjs` hiện có.
- [ ] Cài secure-storage plugin đã kiểm tra tương thích để test resolve được module; chưa sửa logic session. Khi chạy bước đỏ, lỗi phải là hành vi/contract chưa có, không phải thiếu package hoặc cấu hình test.
- [ ] Viết test trước, mock biên native storage/fetch, không dùng mock trong bản app. Một ca tối thiểu trong `src/auth/session.test.ts`:

```ts
import { expect, it, vi } from 'vitest'

vi.mock('@capacitor/core', () => ({
  Capacitor: { isNativePlatform: () => true },
}))
vi.mock('@aparajita/capacitor-secure-storage', () => ({
  SecureStorage: { get: vi.fn().mockResolvedValue(null) },
}))

it('hydrate native không lấy token từ localStorage của web', async () => {
  localStorage.setItem('fookbase.accessToken', 'web-only-token')
  const { initializeAuthSession, getAuthSession } = await import('./session')
  await initializeAuthSession()
  expect(getAuthSession()).toBeNull()
})
```

- [ ] Thêm ca: storage đọc/ghi lỗi; session JSON sai; app chưa hydrate không phát API/hub; ba request 401 chỉ refresh một lần; lỗi mạng/503 giữ phiên; refresh invalid xóa phiên; logout trong lúc refresh không phục hồi user cũ; đổi user không nhận response của user trước.
- [ ] Chạy `npm run test:mobile -- src/auth/session.test.ts src/api/client.test.ts`, xác nhận test fail đúng hành vi trước khi sửa.
- [ ] Implement trong session module, await persistence tại login/refresh/logout. API token đọc từ session đã hydrate, không đọc key riêng trong localStorage trên native. Reuse cùng refresh operation từ AuthProvider.
- [ ] `main.tsx` hiển thị loading/thử lại khi hydrate, mount app sau thành công; không để unhandled rejection hoặc màn hình trắng. Giữ web đăng nhập và event cập nhật phiên hoạt động như trước.
- [ ] Chạy unit tests/build/lint. Trên thiết bị: login → đóng app → mở lại; offline → mở lại → online; hết token → refresh; logout → đóng/mở không tự login; kiểm tra localStorage native không có token.
- [ ] Commit `fix: quản lý phiên đăng nhập an toàn trên Android` và push.

### Task 4 — Vòng đời, Back và liên kết an toàn

**Files:** `src/native/MobileLifecycle.tsx`, `src/native/links.ts`, test tương ứng; `src/routes/index.tsx`, `src/layout/MainLayout.tsx`, `src/shared/components/AppDialog.tsx`, `src/realtime/RealtimeProvider.tsx` thuộc `frontend/web`.

**Interfaces:** `MobileLifecycle` được mount một lần trong root route để dùng router; gọi `refreshAuthSession` Task 3. `resolveAppPath(url: string, webOrigin: string): string | null` chỉ nhận URL HTTPS cùng origin và route được hỗ trợ.

- [ ] Viết test URL trước:

```ts
import { expect, it } from 'vitest'
import { resolveAppPath } from './links'

it('chỉ nhận đường dẫn app trên domain đã cho phép', () => {
  const origin = 'https://web.example.test'
  expect(resolveAppPath(`${origin}/feed`, origin)).toBe('/feed')
  expect(resolveAppPath('https://evil.example/feed', origin)).toBeNull()
  expect(resolveAppPath('javascript:alert(1)', origin)).toBeNull()
  expect(resolveAppPath(`${origin}/login?access_token=secret`, origin)).toBeNull()
  expect(resolveAppPath(`${origin}/unknown`, origin)).toBeNull()
})
```

- [ ] Dùng `new URL` và allowlist bám router thật: feed, notifications, posts/:id, profile/:id, groups/:id. Không bật callback OAuth trong parser này. URL đúng nhưng dữ liệu đã xóa/private phải hiện trạng thái API tương ứng, không vượt phân quyền.
- [ ] Test lifecycle bằng mock App listener và router: setup/cleanup/setup chỉ còn một listener; resume hai lần chỉ một refresh; refresh offline không logout; reconnect tải lại unread mà không thêm handler tin nhắn lần hai.
- [ ] Thêm listener `appStateChange`, `backButton`, `appUrlOpen` và kiểm tra `getLaunchUrl`; cleanup handle khi unmount. Triển khai thứ tự Back ở mục 3.3, dùng cơ chế đóng overlay hiện có và xác minh không đóng nhiều lớp cùng lúc.
- [ ] Chuẩn bị Android verified App Links với domain chủ dự án và `assetlinks.json` chứa applicationId/SHA-256 certificate đúng. File association được cấu hình trên host web thực tế; không tự thay một deployment chưa xác định. Nếu domain chưa sẵn sàng, nghiệm thu parser nội bộ nhưng chưa đánh dấu verified links hoàn thành.
- [ ] Chạy test/build/lint; kiểm tra máy thật: Back khi có bàn phím/dialog, Back ở feed, quay lại sau 5 phút background, mở link khi app đóng/mở, logout rồi mở link private.
- [ ] Commit `feat: xử lý vòng đời và điều hướng Android` và push.

### Task 5 — Responsive, bàn phím và media trên thiết bị

**Files:** Các file UI ở mục 4; `frontend/web/src/api/media.ts` chỉ sửa nếu tìm được lỗi upload cụ thể. Ghi kết quả theo thiết bị trong `docs/mobile-android.md`.

**Interfaces:** Giữ `mediaApi.uploadFileWithMetadata(file: File, onProgress?): Promise<Media>` và chuỗi create → POST FormData → complete; chỉ gắn media vào bài sau khi complete thành công.

- [ ] Tái hiện và ghi hình/capture lỗi trên viewport 320, 360, 390, 412 CSS px; xoay ngang và tăng font hệ thống. Liệt kê nút bị che trước khi sửa.
- [ ] Dùng `viewport-fit=cover` và biến CSS vùng an toàn tại layout gốc. Hướng tính kích thước:

```css
:root {
  --safe-top: env(safe-area-inset-top, 0px);
  --safe-bottom: env(safe-area-inset-bottom, 0px);
}
```

Kết hợp với `--app-header-height` và container hiện có; tránh thêm padding toàn cục làm sai modal/reels. Kiểm tra edge-to-edge thực tế; chỉ thêm plugin điều khiển insets/keyboard khi CSS và cấu hình WebView chưa giải quyết được lỗi đã ghi nhận.

- [ ] Kiểm tra input file HTML hiện có trên Android trước khi thêm Camera/Filesystem. Bắt đầu bằng chọn ảnh/video hệ thống; không xin quyền truy cập toàn bộ bộ nhớ.
- [ ] Kiểm thử ảnh, video trong giới hạn server, file sai MIME, file quá giới hạn, hủy picker, ngắt mạng giữa upload, chuyển nền rồi quay lại. Dùng giới hạn thật từ backend, không tự chọn con số khác. Upload lỗi giữ nội dung soạn và cho thử lại; không tự gửi lại bài khi kết quả request chưa rõ.
- [ ] Test dialog đăng bài/bình luận/AI chat: keyboard mở vẫn nhìn và bấm được Đăng/Gửi/Đóng. Reaction picker, menu tài khoản, tab nhóm/bạn bè và Reels không tràn ngang; vùng bấm chính đặt mục tiêu ít nhất 44×44 CSS px.
- [ ] Chạy build/lint và kiểm tra lại desktop 1280/1536 px để tránh hồi quy; commit `fix: tối ưu thao tác và tải media trên Android` nếu có sửa code, push. Nếu không có lỗi, lưu kết quả QA bằng commit tài liệu riêng.

### Task 6 — Zola và Google trong bản Android nội bộ

**Files:** `frontend/web/src/pages/ZolaLightRedirect.tsx`, test tương ứng, `src/pages/auth/LoginPage.tsx`, `docs/mobile-android.md`.

**Interfaces:** Zola lấy `VITE_ZOLA_LIGHT_URL` đã kiểm tra ở Task 1. Native mở `Browser.open({ url })`; web giữ điều hướng hiện tại. Callback Google native chưa được bật ở mốc A.

- [ ] Viết test native Zola: Browser.open nhận đúng HTTPS URL và không có access/refresh token; location WebView không bị replace; URL thiếu/sai hiện lỗi có nút quay lại. Test web vẫn redirect đúng như trước.
- [ ] Native hiện trang ngắn “Mở Zola Light” / “Quay lại Fookbase” và thông tin đăng nhập riêng; nút mở phải là thao tác chủ động, tránh StrictMode tự mở browser hai lần.
- [ ] Login native hiển thị rõ trạng thái hỗ trợ Google trong bản nội bộ và cách mở web hệ thống nếu cần; giữ nguyên Google login của web. Không dùng user-agent giả hoặc nhúng Google OAuth vào WebView.
- [ ] Chạy test/build/lint; máy thật thử đóng Custom Tab quay lại app, login/logout mỗi ứng dụng, gửi/nhận chat nổi và kiểm tra quyền bạn bè/block hiện có.
- [ ] Commit `feat: mở Zola Light phù hợp với app Android` và push.

### Task 7 — CI, signing và nghiệm thu APK nội bộ

**Files:** `.github/workflows/ci.yml`, `frontend/web/android/app/build.gradle`, `docs/mobile-android.md`; cấu hình ignore native do CLI tạo phải được kiểm tra.

**Interfaces:** Job Android tiêu thụ lockfile, config và tests của Task 1–6; xuất APK debug để QA. Release build dùng signing secrets riêng và không tự publish.

- [ ] Thêm job Android dùng Node 24, JDK/SDK tương ứng project đã sinh; chạy npm ci → env test → unit test → lint → build:mobile → cap sync → Gradle assembleDebug/lint/testDebugUnitTest. Giữ nguyên các job backend/web/admin/Zola.
- [ ] CI test dùng endpoint HTTPS dành cho cấu hình test, không gọi nhầm production để tạo dữ liệu. QA luồng thực dùng staging và tài khoản thử; mock không thay thế lần kiểm tra tích hợp thiết bị.
- [ ] Upload artifact APK với commit SHA. Ghi rõ artifact dùng mock/test endpoint thì chỉ kiểm tra đóng gói, không coi là bản QA kết nối thật.
- [ ] Cấu hình signing release bằng biến môi trường/CI secrets và file keystore tạm, không đưa khóa/password vào source. Kiểm tra tăng versionCode khi nâng cấp, cài bản nâng cấp giữ phiên và dữ liệu đúng.
- [ ] Chạy ma trận mục 6; ghi model thiết bị, Android/WebView version, build SHA, API environment, kết quả và lỗi còn mở. Không đánh dấu pass trường hợp chưa chạy.
- [ ] Commit `ci: kiểm tra và đóng gói ứng dụng Android` và push; bàn giao APK cùng hướng dẫn cài. Chỉ ghi “đã có AAB ký phát hành” khi `bundleRelease` chạy với signing hợp lệ và kiểm tra artifact thực tế.

## 6. Ma trận nghiệm thu

| Nhóm | Ca bắt buộc | Điều kiện đạt |
| --- | --- | --- |
| Cài đặt | Cài mới, nâng cấp, cold start, offline start | Không màn hình trắng; phiên/trạng thái lỗi đúng. |
| Auth | Mật khẩu, OTP/2FA, hết token, revoked session, logout, đổi user | Không lộ phiên cũ, không refresh trùng, không logout vì mạng chập chờn. |
| Điều hướng | Back, dialog, keyboard, external URL, verified link | Hành động theo đúng thứ tự; không thoát app hoặc mở browser ngoài ý muốn. |
| Nội dung | Feed, bài chi tiết, đăng bài, comment, reaction, share | Dữ liệu thực được lưu; không tạo trùng khi retry. |
| Media | Ảnh/video, hủy picker, MIME/size sai, upload đứt | Lỗi có thể hiểu và thử lại; không tạo media thành công giả. |
| Realtime | Resume, reconnect, đổi tài khoản | Unread và tin nhắn đúng; không trùng listener hoặc hiện dữ liệu tài khoản cũ. |
| Tính năng web khác | Bạn bè, nhóm, hồ sơ, tìm kiếm, Reels, AI, trang phụ | Route còn truy cập được; hành động chính dùng được trên mobile. |
| Zola | Mở/đóng browser, phiên độc lập, chat nổi | Quay lại app được; không truyền token qua URL. |
| Hiển thị | 320–412 px, landscape, font lớn, light/dark, gesture/3 nút | Không che nút, không tràn ngang không chủ ý. |
| Desktop | 1280/1536 px, web Google login và web Zola | Không hồi quy từ thay đổi dành cho native. |

Ít nhất một điện thoại Android thật và emulator ở API thấp nhất app hỗ trợ, cộng một API mới trong bộ SDK đã khóa. Browser responsive chỉ là bước hỗ trợ; không thay thế kiểm thử keyboard, picker, Back, secure storage và background trên native.

## 7. Các hạng mục sau mốc nội bộ

### 7.1. Google login native — cổng trước phát hành rộng rãi

Lập kế hoạch con dựa trên `GoogleAuthenticationOptions.cs`, `GoogleAuthenticationService.cs`, `AuthenticationEndpoints.cs`, `src/api/auth.ts`, `LoginPage.tsx` và bộ integration test Identity hiện có. Backend phải nhận diện client mobile riêng, giữ allowlist callback và không thay hợp đồng web/zola-light.

Luồng đích: app tạo verifier/challenge → browser hệ thống mở backend OAuth start → Google trả về backend → backend trả mã một lần về verified App Link → app exchange mã cùng verifier qua HTTPS → lưu phiên bằng Task 3. State/PKCE phải ràng buộc đúng giao dịch/client; không mang access/refresh token trong callback. Mã có TTL ngắn, chỉ dùng một lần; lưu trạng thái giao dịch phải đáp ứng cơ chế triển khai backend hiện tại.

Nghiệm thu: cancel, callback khi app tắt, state sai, verifier sai, replay, mã hết hạn, account cần link/2FA, hai lần đăng nhập đồng thời và regression web/zola-light. Callback chưa được xác minh hoặc chưa qua các ca này thì Google native chưa được coi là hỗ trợ.

### 7.2. Push notification — không suy ra từ SignalR

Tạo kế hoạch riêng cho đăng ký token theo user/device/platform, token rotation, logout/unregister, quyền người nhận và gửi qua FCM; thêm APNs khi làm iOS. Việc này cần API và persistence mới, migration được review riêng, thông tin xác thực ở backend. `@capacitor/push-notifications` cung cấp phía thiết bị; nó không tự thay backend gửi thông báo. [Nguồn Push Notifications](https://capacitorjs.com/docs/apis/push-notifications).

Nghiệm thu foreground/background/terminated, từ chối quyền, đổi tài khoản, token cũ, notification bị gửi lặp, mở nội dung đã xóa/private. Người từ chối push vẫn dùng thông báo trong app được; nội dung push không để lộ thông tin riêng tư trên màn hình khóa.

### 7.3. iOS

Sau mốc Android, thêm `@capacitor/ios` tương thích rồi `npx cap add ios` trên macOS. Kiểm tra origin `capacitor://localhost` thực tế và cập nhật validator/CORS bằng ngoại lệ allowlist hẹp kèm test; validator hiện tại chỉ cho HTTPS nên không thể chỉ thêm env rồi coi là xong.

Kiểm tra Keychain khi reinstall, tắt đồng bộ tài khoản ngoài ý muốn, universal links, keyboard/insets, media picker, plugin privacy manifest và signing/TestFlight. Chốt macOS/Xcode theo Capacitor đã khóa và yêu cầu phát hành đang có hiệu lực khi thực hiện.

## 8. Dự kiến tiến độ và bàn giao

Ước lượng cho một người đã quen repository, có endpoint HTTPS và thiết bị Android: Task 1–2 khoảng 1–2 ngày; Task 3–4 khoảng 2–4 ngày; Task 5–6 khoảng 2–3 ngày; Task 7 khoảng 1–2 ngày. Tổng **6–11 ngày làm việc cho APK nội bộ**, chưa gồm Google native, push, iOS, thời gian cung cấp signing/domain hoặc store review. Điều chỉnh sau Task 2 nếu plugin/toolchain hoặc API thực tế có vướng mắc.

Thứ tự phụ thuộc: Task 1 → 2 → 3 → 4 → 5 → 6 → 7. Chỉ đánh dấu mốc A hoàn thành khi QA thiết bị qua các ca trong phạm vi và bàn giao APK kết nối được môi trường đã ghi rõ.

Bàn giao mỗi mốc gồm commit SHA, trạng thái push, câu lệnh build, artifact và checksum nếu đã build, kết quả test thực chạy, danh sách giới hạn còn lại. File plan là đầu vào triển khai; không phải bằng chứng ứng dụng đã được xây dựng.
