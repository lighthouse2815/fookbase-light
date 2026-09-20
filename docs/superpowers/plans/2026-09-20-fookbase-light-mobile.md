# Fookbase Light — Standalone Mobile App Implementation Plan

> Cập nhật triển khai 20/09/2026: source app native và các module Tasks 1–8 đã được triển khai tại `frontend/mobile`; backend Google có test tích hợp riêng. CI/check/export và workflow APK ở Task 9 đã thêm. Xem [hướng dẫn Android](../../mobile-android.md) để build/cấu hình. Các checklist nghiệm thu thiết bị/API thật bên dưới chưa được đánh dấu hoàn tất: cần domain API, App Links/certificate và kiểm thử hai tài khoản/thiết bị. Không coi source hoặc JS export là bằng chứng hoàn thành production.


> **For agentic workers:** Khi được yêu cầu triển khai, dùng `superpowers:executing-plans` và thực hiện từng task. Chỉ giao việc cho subagent nếu người dùng chọn phương thức đó. Checkbox mô tả công việc cần làm, không xác nhận app đã được xây dựng.

**Goal:** Xây dựng ứng dụng Fookbase Light có codebase và giao diện mobile riêng bằng React Native + Expo, ưu tiên Android và dùng chung backend hiện tại.

**Architecture:** Tạo `frontend/mobile` độc lập với web/admin/Zola, có package, navigation, màn hình, phiên đăng nhập và quy trình build riêng. App gọi trực tiếp ASP.NET Core API và SignalR hiện có qua HTTPS/WSS. Màn hình được viết bằng React Native; chat nằm trong app và dùng cùng dữ liệu với Zola Light.

**Tech Stack:** React Native, Expo, TypeScript, Expo Router, Expo SecureStore, TanStack Query, SignalR; Expo ImagePicker/Video theo tính năng; Jest Expo và React Native Testing Library. Giữ backend .NET 10 và PostgreSQL hiện có.

**Spec:** Quyết định và phạm vi ở mục 1–4; tham chiếu [kiến trúc backend](../../modular-monolith.md), [hợp đồng Zola Light](../../zola-light-v1.md), [triển khai production](../../production-deployment.md).

**Trạng thái ngày 20/09/2026:** Người dùng đã chọn **app riêng, React Native + Expo**. Tài liệu này thay thế phương án Capacitor trước đó. Android vẫn là nền tảng đầu tiên; iOS triển khai sau. Chưa tạo project mobile hoặc cài dependency.

## Global Constraints

- `frontend/mobile` có package/lockfile riêng, nằm trong repository hiện tại. App riêng không đòi hỏi repository hoặc backend riêng.
- Dùng native components; không dựng ứng dụng bằng WebView tải website. Không import React DOM, CSS/Tailwind web, BrowserRouter, localStorage hoặc window vào mobile.
- Dùng chung tài khoản, API, dữ liệu và phân quyền với web. Mobile có phiên đăng nhập riêng; không sao chép token trình duyệt.
- Giữ web/admin/Zola hoạt động độc lập. Chỉ sửa backend khi mobile cần hợp đồng còn thiếu, bằng thay đổi nhỏ và có test riêng.
- Dùng phiên bản React/React Native do Expo SDK đã chọn hỗ trợ; không sao chép version từ package web. Cài module native bằng `npx expo install`.
- Giữ npm theo convention repository; chưa chuyển toàn repo sang workspace/Turborepo hoặc tạo shared package chỉ để đặt vài kiểu dữ liệu.
- Refresh token dùng SecureStore; access token ở bộ nhớ. Không lưu token trong AsyncStorage, URL, log hoặc cache dữ liệu màn hình.
- Release dùng API HTTPS đã cấu hình. `EXPO_PUBLIC_*` là thông tin công khai, không chứa secret.
- Mỗi phần độc lập: kiểm tra, chạy check liên quan, commit tiếng Việt có dấu và push GitHub. Không commit code đang fail do thay đổi mới.

## Review Focus

1. App bị đóng lúc refresh/logout: mở lại không dùng phiên cũ hoặc khôi phục tài khoản đã logout — Task 2.
2. Mất mạng/resume: giữ phiên, tải bù dữ liệu, không nhân đôi tin nhắn hoặc gửi lại thao tác chưa rõ kết quả — Task 2, 3, 6.
3. Bàn phím/chữ lớn che nút: Đăng, Gửi, Quay lại và menu vẫn thao tác được — Task 3–7.
4. URI media và link không hợp lệ: upload có trạng thái thật, deep link không vượt phân quyền hoặc lộ token — Task 4, 6, 8.
5. Đổi user hoặc quyền bị thu hồi: cache không hiện dữ liệu tài khoản trước/nhóm cũ — Task 2, 5, 6, 9.

## 1. Lựa chọn công nghệ

| Hướng | Điểm phù hợp | Chi phí với dự án này |
| --- | --- | --- |
| **React Native + Expo — đã chọn** | React/TypeScript, UI native, một codebase hướng Android và iOS; có công cụ build và module thiết bị. | Viết lại UI; tận dụng hợp đồng API và kỹ năng React hiện có. |
| Flutter | App native đa nền tảng với hệ widget riêng. | Bổ sung Dart và hệ UI khác, ít tận dụng TypeScript. |
| Kotlin Android | Kiểm soát trực tiếp nền tảng Android. | iOS cần codebase khác; chưa cần với phạm vi mạng xã hội hiện tại. |

Expo Router hỗ trợ điều hướng theo file cho React Native. Dùng development build khi kiểm thử tích hợp native, đăng nhập và liên kết của chính ứng dụng; không lấy việc chạy Expo Go làm bằng chứng release chạy đúng. [Expo Router](https://docs.expo.dev/router/introduction/), [development builds](https://docs.expo.dev/develop/development-builds/introduction/).

## 2. Tái sử dụng source hiện có

| Thành phần | Quyết định | Source tham chiếu |
| --- | --- | --- |
| Backend/database | Dùng API và phân quyền hiện có. | `backend/Fookbase.Src/Main/Code/Modules/` |
| Request/response | Cùng endpoint/DTO; chỉ chuyển type/hàm thuần thực sự cần vào app. | `frontend/web/src/api/` |
| API client/phiên | Client native nhỏ với SecureStore/fetch/refresh; không import client web đọc localStorage. | `frontend/web/src/api/client.ts`, `frontend/web/src/auth/session.ts` |
| Giao diện | Xây màn hình native theo nghiệp vụ; lấy màu/nhận diện làm theme tokens. | `frontend/web/src/pages/`, `frontend/web/src/index.css` |
| Feed | Giữ cursor, asOfUtc, home/following và post/reel/share. | `frontend/web/src/api/feed.ts` |
| Chat | Màn hình native dùng hợp đồng đầy đủ của Zola. | `frontend/zola-light/src/api.ts` |
| Realtime | Giữ tên event/endpoint; lifecycle dùng React Native AppState. | `frontend/web/src/realtime/RealtimeProvider.tsx` |
| Media | Giữ create intent → POST FormData → complete; chuyển URI native thành upload. | `frontend/web/src/api/media.ts` |
| Google | Client mobile và callback riêng, không gửi client web từ app. | `backend/Fookbase.Src/Main/Code/Modules/Identity/Config/GoogleAuthenticationOptions.cs` |
| CI | Job mobile riêng, giữ các job hiện tại. | `.github/workflows/ci.yml` |

Không sao chép nguyên thư mục API rồi sửa hàng loạt. Chỉ đưa module được task sử dụng vào app; khi có nhu cầu chia sẻ code thuần rõ ràng giữa nhiều client mới xem xét shared package. DTO phải đối chiếu với endpoint/backend đang chạy để tránh hai client lệch hợp đồng.

React Native gọi mạng native, không áp dụng CORS trình duyệt như WebView. Không mang origin localhost/Capacitor của plan cũ sang cấu hình backend; HTTPS, xác thực và phân quyền vẫn bắt buộc. [React Native networking](https://reactnative.dev/docs/network).

## 3. Phạm vi và trải nghiệm

### Mốc A — APK nội bộ cho luồng chính

- Đăng nhập mật khẩu, đăng ký, OTP/2FA, quên mật khẩu, phục hồi phiên và logout.
- Feed home/following, bài chi tiết, đăng bài ảnh/video, comment, reaction và chia sẻ nội bộ.
- Hồ sơ, tìm kiếm, bạn bè, nhóm và hành động tham gia/duyệt theo quyền API.
- Chat 1–1/nhóm ngay trong app: danh sách, lịch sử, tạo nhóm, gửi nội dung/attachment, read state và reconnect.
- Thông báo trong app. Tài khoản chỉ có Google cần Task 8 trước khi đăng nhập mobile; không quảng bá mốc A hỗ trợ mọi phương thức đăng nhập.

### Mốc B — Hoàn thiện trước phát hành rộng rãi

- Reels, stories, AI chat, saved và privacy/security ở Task 7.
- Google native, deep links, signing và QA. Push là hạng mục riêng nếu được đưa vào phạm vi release.
- Đối chiếu bảng tính năng dưới đây; không coi APK nội bộ là bản đã ngang toàn bộ tính năng web.

| Nhóm chức năng | Đích mobile | Mốc/task |
| --- | --- | --- |
| Feed/post/comment/reaction/share | Màn hình native và composer | A / 3–4 |
| Profile/search/friends/groups | Màn hình và hành động native | A / 5 |
| Chat/thông báo | Tab và stack native, cùng dữ liệu Zola | A / 6 |
| Reels/stories/AI/saved/privacy/security | Native theo từng module | B / 7 |
| Page/event/album/photo/memories/birthday | Port từng module, lập checklist API/màn hình/test trước mỗi module | Sau B / kế hoạch mở rộng riêng |
| Game web | Đánh giá tương tác/engine mobile riêng, không nhúng web để tính là đã port | Ngoài release đầu |
| Admin | Giữ ứng dụng admin riêng | Ngoài app người dùng |

Navigation chính: **Bảng tin, Tin nhắn, Thông báo, Menu**. Search và Tạo bài luôn truy cập được ở feed; Menu chứa hồ sơ, bạn bè, nhóm, Reels và tính năng đã hoàn thành. Detail/composer/chat dùng stack; không tạo tab rỗng hoặc nút không có chức năng.

Giao diện tiếng Việt, light/dark, safe area, font scaling. Dùng primitives React Native và icon có sẵn trong template; chỉ tạo component chung khi có nơi sử dụng thật. Dùng FlatList và navigation chuẩn, không tự viết virtualized list hoặc bộ định tuyến.

## 4. Thiết kế kỹ thuật

### 4.1. Project và dependencies

`frontend/mobile` có package/lockfile, `app.config.ts`, `tsconfig.json`, `eas.json` và `src/app` riêng. Chọn Expo SDK stable khi khởi tạo, khóa phiên bản tương thích theo ma trận SDK; Node 24 theo CI, JDK/SDK theo template đã chọn. Không ép React Native mới nhất ngoài dải Expo hỗ trợ. [Ma trận Expo SDK](https://docs.expo.dev/versions/latest/).

Ban đầu dùng Router, dev-client, SecureStore, safe-area/screens theo template. TanStack Query giải quyết pagination/cache/mutation; thêm SignalR khi làm chat, ImagePicker/Video khi làm media. Không cài trước push/camera/biometric hoặc hệ state management thứ hai.

Dùng Expo Continuous Native Generation: cấu hình trong app config/config plugins; thư mục android/ios được sinh khi build và ignore. Không sửa generated native files rồi phụ thuộc vào việc chúng còn nguyên ở lần prebuild sau. Nếu cần native customization, đưa vào config plugin nhỏ và test tái sinh project.

### 4.2. Phiên, request và cache

SecureStore chỉ lưu refresh token cần phục hồi; access token và user ở bộ nhớ. Cold start đọc refresh token → refresh HTTPS → lưu token rotate → authenticated. Offline giữ token và hiện trạng thái chờ/thử lại, không tự đánh dấu đã xác thực. Lỗi ghi SecureStore phải được xử lý; không nhét toàn bộ profile/JWT vào blob lớn hoặc fallback sang AsyncStorage. [Expo SecureStore](https://docs.expo.dev/versions/latest/sdk/securestore/).

Hợp đồng `src/auth/session.ts` và `src/api/client.ts`:

```ts
export type SessionState = 'loading' | 'anonymous' | 'authenticated' | 'offline' | 'storage-error'
export function initializeSession(): Promise<void>
export function getSession(): AuthenticationResponse | null
export function saveSession(value: AuthenticationResponse): Promise<void>
export function clearSession(): Promise<void>
export function refreshSession(): Promise<string | null>
export function apiRequest<T>(path: string, init?: RequestInit): Promise<T>
```

AuthenticationResponse bám `frontend/web/src/api/auth.ts`. `refreshSession` thuộc session module, dùng fetch riêng tới `/api/auth/refresh` để không tạo vòng lặp client → refresh → client. Timer/resume/401 dùng chung một promise refresh. Lưu token mới trước khi báo thành công; serialize ghi/xóa, kiểm tra session generation để response cũ không phục hồi user vừa logout.

Refresh invalid theo status/body backend mới kết thúc phiên; network/5xx giữ token để thử lại. Không retry mutation nếu chưa rõ server đã ghi nhận. Query key có userId; logout hủy request, dừng hub, xóa cache và chặn response cũ ghi lại. AppState/focusManager và trạng thái mạng/onlineManager phối hợp refetch sau khi phiên sẵn sàng. [TanStack Query native](https://tanstack.com/query/latest/docs/framework/react/react-native).

### 4.3. Media và realtime

`PickedMedia = { uri: string; name: string; mimeType: string; sizeBytes: number }`. Picker trả URI native, không phải File trình duyệt; chuẩn hóa metadata trước tạo upload intent. Upload multipart từ URI theo API SDK hỗ trợ, giữ field server cung cấp; không base64 cả video hoặc tự đặt boundary. URL đọc hết hạn phải xin lại. [Expo ImagePicker](https://docs.expo.dev/versions/latest/sdk/imagepicker/).

Giữ `/hubs/messages` và `/hubs/notifications`; kiểm thử SignalR trên Android development build sớm, không suy từ việc web chạy được. HTTP là nguồn dữ liệu sau reconnect. Gộp response/event theo message.id, lấy lại badge từ server, không chỉ cộng dồn sự kiện.

### 4.4. Đầu vào trước build/phát hành

| Thông tin | Cách dùng |
| --- | --- |
| Android package ID của chủ dự án | Khóa trong app config trước phân phối; không dùng ID ví dụ. |
| API HTTPS staging/production | EXPO_PUBLIC_API_BASE_URL; không dùng localhost của máy phát triển trên điện thoại. |
| Domain web/callback | Verified App Links, OAuth, allowlist liên kết. |
| Tên/icon/splash | Tận dụng thương hiệu hiện có, sinh asset bằng công cụ. |
| Signing key/tài khoản store | Cấp khi làm release, khóa ngoài Git. EAS cloud là lựa chọn, không bắt buộc để build local. |

## 5. Task triển khai

Các đường dẫn `src/...` dưới đây thuộc **frontend/mobile**. Tạo file cùng implementation/test sử dụng nó. Task lớn chia theo luồng đã chạy được và commit riêng; không commit scaffold rỗng để báo hoàn thành chức năng.

### Task 1 — Project Expo và màn hình đăng nhập native

**Files:** Tạo package/config/lockfile mobile, `src/app/_layout.tsx`, `src/app/index.tsx`, `src/app/(auth)/login.tsx`, `src/config/env.ts`, `env.test.ts`, `src/theme.ts`, `docs/mobile-android.md`.

- [ ] Kiểm tra Git/CI/API và ghi package ID, endpoint, bộ công cụ đã thống nhất.
- [ ] Khởi tạo bằng create-expo-app template Router TypeScript stable; giữ version template hỗ trợ, bỏ demo. Thêm dev-client, SecureStore, Jest Expo và React Native Testing Library bằng công cụ Expo.
- [ ] Export `validateApiBaseUrl(value: string | undefined): string`; dùng new URL để chặn thiếu/relative/HTTP/loopback/credentials/query/fragment trong release. Local dev có cấu hình riêng.
- [ ] Viết test trước và xác nhận đỏ vì thiếu validator:

```ts
import { validateApiBaseUrl } from './env'

it('chặn endpoint không dùng được trong release', () => {
  for (const value of [undefined, '/api', 'http://api.example.test', 'https://localhost']) {
    expect(() => validateApiBaseUrl(value)).toThrow()
  }
  expect(validateApiBaseUrl('https://api.example.test/')).toBe('https://api.example.test')
})
```

- [ ] Implement config và form identifier/password, show/hide password, keyboard, validation. Kết nối login hoàn tất Task 2; Task 1 chưa phải mốc app sử dụng được.
- [ ] Scripts lint, typecheck = tsc --noEmit, test = jest. Chạy expo-doctor, lint/typecheck/test, `npx expo run:android`; mở development build trên máy thật.
- [ ] Commit `feat: khởi tạo ứng dụng mobile React Native` và push.

### Task 2 — Auth, SecureStore và API client

**Files:** `src/auth/session.ts`, `session.test.ts`, `AuthProvider.tsx`, `src/api/client.ts`, `client.test.ts`, `src/api/auth.ts`, root layout và route auth login/register/verify/forgot-password/reset-password.

**Interfaces:** Mục 4.2; auth DTO/payload bám API web. Route bảo vệ chỉ mở sau initializeSession đúng trạng thái.

- [ ] Test trước: không token, token invalid, offline giữ token, ba 401 một refresh, storage ghi lỗi, logout giữa refresh, đổi user khi request chưa xong. Mock chỉ trong test.

```ts
import * as SecureStore from 'expo-secure-store'
import { initializeSession, getSession } from './session'

jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn().mockResolvedValue(null),
  setItemAsync: jest.fn(),
  deleteItemAsync: jest.fn(),
}))
it('không có refresh token là phiên khách', async () => {
  await initializeSession()
  expect(SecureStore.getItemAsync).toHaveBeenCalled()
  expect(getSession()).toBeNull()
})
```

- [ ] Chạy test đỏ trước khi implement. API client xử lý 204, ProblemDetails, JSON/FormData, AbortSignal và refresh một lần; root có loading/offline/storage-error/thử lại, không splash vô hạn.
- [ ] Kết nối login, register/start/resend/verify, 2FA, reset, logout theo endpoint thật. Phân biệt registration challenge và 2FA challenge; không tự đổi schema OTP.
- [ ] Máy thật: login → kill → mở lại, offline → retry, token expired/revoked, logout/đổi user. Test/typecheck/lint; commit `feat: đăng nhập và quản lý phiên mobile` và push.

### Task 3 — Navigation, feed và bài chi tiết

**Files:** `src/app/(tabs)/_layout.tsx`, `(tabs)/feed.tsx`, `src/app/posts/[postId].tsx`, `src/features/feed/FeedScreen.tsx`, `FeedScreen.test.tsx`, `PostCard.tsx`, `src/api/feed.ts`, `src/api/posts.ts`, `src/query/client.ts`.

**Interfaces:** feedApi.getHome/getFollowing trả FeedPage gồm items/nextCursor/asOfUtc như web; query key gồm userId và feed mode.

- [ ] Dùng Expo Router stack/tab, FlatList, TanStack useInfiniteQuery. Chỉ thêm tab khi có màn hình; menu, messages và notifications xuất hiện ở task tương ứng.
- [ ] Test trước: empty/error/retry, lỗi trang tiếp theo giữ bài cũ, cursor null ngừng tải, đổi mode không trộn bài, response user cũ bị loại. Assert nội dung và action bằng Testing Library, fixture theo schema thật.
- [ ] Render đúng post/reel/share và quyền; nội dung deleted/private có trạng thái rõ. Video playback đầy đủ ở Task 7, không đánh dấu Reels xong từ preview card.
- [ ] Thử API staging, pull-to-refresh/pagination, Back, chữ lớn và scroll; test/typecheck/lint; commit `feat: xây dựng bảng tin và bài viết mobile` và push.

### Task 4 — Composer, media và tương tác

**Files:** `src/app/posts/create.tsx`, `src/features/posts/PostComposer.tsx`, `PostComposer.test.tsx`, `PostDiscussion.tsx`, `src/api/media.ts`, `media.test.ts`; sửa PostCard và detail.

**Interfaces:** `uploadMedia(file: PickedMedia, onProgress?: (value: number) => void): Promise<string>` trả mediaId sau complete; payload post/comment/reaction bám API web.

- [ ] Test trước chuỗi upload: intent fail không upload, upload fail không complete, complete fail không đăng bài, hủy picker không request. Assert method POST/FormData và field ký upload đúng server.
- [ ] ImagePicker lấy ảnh/video; metadata thiếu phải xác định lại hoặc báo lỗi trước intent. Chặn MIME/size theo backend, giữ nội dung soạn khi lỗi. Không giả định URI native là browser File.
- [ ] Implement đăng bài, comment, reaction, share; cache cập nhật sau thành công hoặc optimistic update có rollback. Không tự retry mutation timeout chưa rõ kết quả.
- [ ] Máy thật: content/file URI, ảnh lớn/video, từ chối quyền, hủy picker, upload đứt/background. Keyboard mở vẫn bấm Đăng/Gửi. Test/typecheck/lint và vòng đăng/xem bằng API thật; commit `feat: đăng bài và tương tác media trên mobile` rồi push.

### Task 5 — Profile, search, friends và groups

**Files:** `src/app/(tabs)/menu.tsx`, `src/app/profile/[userId].tsx`, `src/app/search.tsx`, `src/app/friends.tsx`, `src/app/groups/index.tsx`, `src/app/groups/[groupId].tsx`; API module tương ứng và test cạnh feature.

- [ ] Checklist thao tác từ web: xem/sửa profile, tìm kiếm/phân trang, gửi/hủy/chấp nhận lời mời, follow/block, tham gia/rời nhóm và action moderator đúng quyền.
- [ ] Test trước: kết quả search A đến sau B không đè B; quyền thu hồi hiện 403/refetch; block không có hành động trái phép; đổi user xóa cache.
- [ ] Implement màn hình và action thật theo API, không chỉ list. Menu/chữ lớn không làm mất nút; backend vẫn quyết định quyền.
- [ ] Hai tài khoản khác quyền, màn hình nhỏ/font lớn; test/typecheck/lint. Mỗi module chạy được commit riêng, ví dụ `feat: thêm hồ sơ và tìm kiếm mobile`, rồi push.

### Task 6 — Chat native, SignalR và thông báo

**Files:** `src/app/(tabs)/messages.tsx`, `(tabs)/notifications.tsx`, `src/app/conversations/[conversationId].tsx`, `src/app/conversations/create.tsx`, `src/features/messages/ConversationScreen.tsx`, test cạnh feature, `src/api/messages.ts`, `src/api/notifications.ts`, `src/realtime/RealtimeProvider.tsx`, `RealtimeProvider.test.tsx`.

**Interfaces:** Dùng hợp đồng đầy đủ `frontend/zola-light/src/api.ts`: direct/group, history cursor, send(content/mediaIds/replyToMessageId), read(lastReadMessageId), participants. Event chính: MessageReceived, MessagesRead, TypingStarted, PresenceSnapshot/Changed, NotificationReceived.

- [ ] Smoke test negotiate/connect/token/reconnect Android với API staging sớm, ngay sau Task 2. Chỉ thêm polyfill khi trace chứng minh thiếu runtime API; không suy từ web hoặc mock test.
- [ ] Test trước: gộp event/HTTP theo message.id dù thứ tự đảo, history cursor, unread, permission denied, offline/resume và logout đổi user.
- [ ] Xây chat 1–1/nhóm, tạo nhóm, attachment/read và quản lý thành viên theo quyền server. Dùng endpoint đọc media chat có phân quyền của Zola, không tự suy URL storage công khai.
- [ ] AppState resume refresh trước reconnect/refetch; stop/off/remove listeners khi logout/unmount. Không giả định socket sống ở background. Tin timeout cần kiểm tra history trước khi cho gửi lại, không âm thầm auto-retry.
- [ ] Notification mở theo type/entityId; đối tượng ngoài phạm vi release có giải thích hoặc nút mở web rõ ràng, không route rỗng. Không thêm token vào link.
- [ ] Hai thiết bị/tài khoản gửi/nhận file, read, vào/rời nhóm, block/reconnect; keyboard không che gửi. Test/typecheck/lint; commit/push chat và notification theo các phần độc lập.

### Task 7 — Reels, stories, AI và cài đặt

**Files theo module:** `src/app/reels.tsx`, `src/app/stories/[storyId].tsx`, `src/app/ai-chat.tsx`, `src/app/saved.tsx`, `src/app/settings/privacy.tsx`, `src/app/settings/security.tsx`; feature/API/test tương ứng.

- [ ] Trước mỗi module lập checklist endpoint/action từ web: xem/tạo/reaction stories, phát/tạo Reels, AI history/request, save/unsave, privacy, phiên và 2FA. Viết test hành vi trước code; mỗi module commit riêng.
- [ ] Reels dùng Expo Video, chỉ phát item đang xem, pause blur/background. Test URL hết hạn, âm thanh sau thoát, nhiều video/bộ nhớ; không giữ mọi player chạy cùng lúc. [Expo Video](https://docs.expo.dev/versions/latest/sdk/video/).
- [ ] Stories giữ thời hạn/quyền server; test xóa/hết hạn lúc xem. AI gọi `/api/ai/chat` với message/history, nhận JSON hiện có; không giả định SSE hoặc đưa model API key vào app.
- [ ] Saved/privacy/security dùng API thật; test đổi mật khẩu/revoke session/2FA và thông tin khôi phục theo flow hiện tại. Native keyboard/font scaling, test/typecheck/lint; commit/push từng module hoàn chỉnh.

### Task 8 — Google native và deep links

**Files mobile:** `src/auth/google.ts`, `google.test.ts`, `src/app/auth/callback.tsx`, `src/app/+native-intent.tsx`, `app.config.ts`. **Backend:** Identity GoogleAuthenticationOptions, GoogleAuthenticationService, AuthenticationEndpoints và bộ test Identity hiện có.

- [ ] Viết hợp đồng/spec con trước khi sửa OAuth: client mobile riêng, callback allowlist, transaction state/PKCE và nơi lưu giao dịch phù hợp backend. Giữ web/zola-light backward compatible.
- [ ] Luồng đích: browser hệ thống → Google/backend → mã một lần về verified App Link → exchange mã + verifier → SecureStore. Không WebView OAuth, token trong callback hoặc client secret trong app. Chọn Expo WebBrowser/AuthSession sau khi hợp đồng rõ.
- [ ] Test trước: state/verifier sai, replay, mã hết hạn, cancel, callback cold start, account link, 2FA và regression web/Zola. Parser scheme/host/path không được dẫn vào route không hợp lệ hoặc vượt quyền.
- [ ] Cấu hình intent filters và assetlinks.json trên domain chủ dự án với certificate đúng. Chạy test mobile, build/backend test Identity bằng cơ chế cô lập của repo; commit/push theo phần tương thích. Chưa qua máy thật thì chưa đánh dấu Google hỗ trợ.

### Task 9 — CI, signing và QA phát hành

**Files:** `frontend/mobile/eas.json`, `app.config.ts`, `.github/workflows/ci.yml`, `docs/mobile-android.md`; E2E native trong `frontend/mobile/.maestro/` khi đưa Maestro vào pipeline.

- [ ] Job mobile riêng: npm ci, expo-doctor, lint/typecheck, Jest, export Android; giữ job web/admin/Zola/backend. Dùng Jest Expo cho unit/component tests. [Expo testing](https://docs.expo.dev/develop/unit-testing/).
- [ ] Build APK thật bằng prebuild/Gradle runner có SDK/JDK theo template, hoặc EAS nếu chủ dự án chọn dịch vụ; JS export không thay native build.

```bash
# Trong frontend/mobile, sau khi cấu hình môi trường và Android SDK.
npm ci
npx expo-doctor
npm run lint
npm run typecheck
npm test -- --runInBand
npx expo export --platform android
npx expo prebuild --platform android
cd android
./gradlew assembleDebug
```

- [ ] Bản QA phân phối có bundle chạy khi không có Metro: dùng release-like preview APK. Dev-client/debug APK là công cụ phát triển. Nếu dùng EAS, preview buildType apk và production AAB. [Expo APK builds](https://docs.expo.dev/build-reference/apk/).
- [ ] Signing keystore/password ở secret store, tăng versionCode và thử upgrade. Không tự publish store khi mới làm artifact QA. Rà soát yêu cầu store và thông tin quyền riêng tư/tài khoản tại thời điểm nộp.
- [ ] Chạy ma trận mục 6; ghi thiết bị/OS/build SHA/API environment và kết quả thật. Commit `ci: kiểm tra và build ứng dụng mobile` và push.

## 6. Ma trận nghiệm thu

| Nhóm | Ca bắt buộc | Điều kiện đạt |
| --- | --- | --- |
| Build | Cài mới/upgrade, mở không Metro, cold/offline start | App chạy từ package, lỗi phục hồi được, không màn hình trắng. |
| Auth | Mật khẩu/OTP/2FA, refresh/revoked/logout/đổi user | Không lộ phiên cũ, không refresh trùng hoặc logout vì mạng. |
| Feed/post | Cursor/refresh, post/media/comment/reaction/share | Lưu dữ liệu thật, không trùng bài, giữ input khi lỗi phù hợp. |
| Social | Profile/search/friend/group với các quyền | Action chính dùng được, backend authorization còn hiệu lực. |
| Chat | Direct/group/file/read/reconnect và thứ tự event | Tin/badge đúng, không trùng, chat trong app. |
| UI | 320–412 dp, landscape, font lớn, gesture/3 nút | Không che Đăng/Gửi/Back, action có nhãn accessibility. |
| Lifecycle | Kill/background/đổi mạng/revoke quyền | Không rò listener/cache user cũ, refetch đúng lúc. |
| Google/link | Cancel/replay/cold callback/URL lạ | Đúng giao dịch/client, không lộ token hoặc vượt quyền. |
| Media/Reels | URI/size/MIME/expired URL/pause | Không OOM do base64/nhiều player; không báo thành công giả. |
| Regression | Các client và backend nếu sửa phần chung | Luồng hiện có tiếp tục hoạt động. |

Ít nhất một Android thật, emulator OS thấp nhất app/Expo hỗ trợ và một OS mới trong SDK đã khóa. Test JS/mock không chứng minh SecureStore, SignalR, picker, keyboard, deep links hoặc signing hoạt động native.

## 7. Push, iOS và mở rộng

**Push:** Kế hoạch riêng cho register/unregister/rotation theo user/device, permission, deep link và backend dispatch. Chọn Expo Push Service hoặc FCM theo hạ tầng; không trộn loại token. Persistence/migration review riêng. Test foreground/background/terminated, từ chối quyền và logout/đổi user; thông báo trong app vẫn hoạt động. SignalR không thay push.

**iOS:** Dùng cùng frontend/mobile; thêm bundle ID, signing/TestFlight trên macOS hoặc dịch vụ đã chọn. Kiểm tra SecureStore reinstall, universal links, media permission, keyboard/safe area và privacy theo SDK/store khi làm. Android chạy được không tự chứng minh iOS đã hoàn thành.

**Tính năng sau B:** Page/event/album/memories/birthday/game theo bảng mục 3. Mỗi module cần danh sách màn hình/API/test và tiêu chí native trước khi triển khai; không tạo placeholder để giả vờ ngang tính năng web.

## 8. Ước lượng và bàn giao

Ước lượng ban đầu cho một người quen React/repository, có API HTTPS và thiết bị: **mốc A khoảng 4–6 tuần**, **mốc B thêm 2–4 tuần**, tùy OAuth/media/độ hoàn thiện UI. Push/iOS/tính năng mở rộng và thời gian store tính riêng. Đánh giá lại sau Task 2 và smoke test SignalR; đây không phải cam kết ngày phát hành.

Thứ tự 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9; smoke test SignalR ngay sau Task 2. CI/check/build thiết bị chạy tăng dần, không chờ Task 9 mới kiểm tra.

Bàn giao source frontend/mobile, hướng dẫn build, commit SHA/push, APK/AAB nếu đã build, QA thực và giới hạn còn lại. Tài liệu này là kế hoạch; việc tạo project và triển khai app bắt đầu khi người dùng yêu cầu thực hiện kế hoạch.
