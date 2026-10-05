# Cải thiện UX frontend tiếp theo

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Checkboxes record completed steps.

**Goal:** Hoàn thành năm cải thiện FE đã được người dùng duyệt trong cuộc trò chuyện.

**Architecture:** Giữ cấu trúc hiện tại; dùng React Query, Expo Router/React Navigation và React Router sẵn có. Tái sử dụng API hiện tại, không sửa backend hoặc thêm dependency.

**Tech Stack:** React 19, TypeScript, Expo SDK 57, React Query, React Router, Jest, Playwright ngoài repository.

**Spec:** Thiết kế ngắn trong cuộc trò chuyện: chat mobile chỉ đọc tin đang nhìn thấy; giữ bản nháp mobile và đính kèm khi hủy picker; album không tạo trùng và thử lại upload trong cùng album; tìm kiếm mobile dùng API tổng hợp có sẵn; web có giao diện phục hồi lỗi.

## Global Constraints

- Chỉ sửa frontend và tài liệu phục vụ đợt FE này; giữ nguyên backend đang sửa.
- Không thêm dependency, đổi kiến trúc, thay contract API hoặc commit credential.
- Dùng convention hiện tại và đọc tài liệu Expo đúng SDK 57 trước khi sửa mobile.
- Mỗi phần phải có test có ý nghĩa, typecheck/build, lint, commit tiếng Việt có dấu; root tích hợp và push main sau mỗi phần.
- Những phần ở commit trước đã hoàn tất; không làm lại auth, feed web, post composer web hoặc Reels.

## Review Focus

- Tin mới ngoài vùng nhìn, màn hình mất focus hoặc ứng dụng ở nền không được đánh dấu đọc.
- Tải lên lỗi giữa chừng phải giữ nội dung và album/media đã hoàn thành để thử lại.
- Hủy picker không xóa tệp cũ; bản nháp không trộn tài khoản hoặc hội thoại.
- Đáp ứng tìm kiếm cũ không thay kết quả truy vấn mới; kết quả mở đúng route đã có.
- Lỗi tải chunk và lỗi render có nút phục hồi thực; không làm mất bản nháp hoặc hiển thị stack trace.

### Task 1: Chat mobile đọc tin chính xác

**Files:** `frontend/mobile/src/app/conversations/[conversationId].tsx`, bản tương ứng trong `frontend/zola-mobile`, tests ngoài thư mục app.
**Interfaces:** Dùng `messengerApi.read(conversationId, messageId)` và `FlatList.onViewableItemsChanged`, AppState, focus có sẵn. Không thay API.

- [x] Viết và chạy test thất bại cho tin ngoài viewport, màn hình unfocus/background, đọc thành công/thất bại và retry.
- [x] Chỉ ghi read khi incoming message đang nhìn thấy, màn hình focus, AppState active; khóa request đồng thời và giữ unread khi lỗi.
- [x] Giữ vùng đọc khi tải lịch sử hoặc tin mới; cung cấp retry không làm mất lịch sử.
- [x] Chạy Jest liên quan, typecheck/lint cả mobile và Zola mobile; tự rà diff và gửi report cho root.
- [x] Root review, commit riêng và tích hợp/push main.

### Task 2: Bản nháp mobile và picker

**Files:** `frontend/mobile/src/app/posts/create.tsx`, `media/create.tsx`, chat ở cả hai ứng dụng; helpers nhỏ gần features nếu thật sự dùng chung, tests ngoài app.
**Interfaces:** Dùng `usePreventRemove` theo màn security; tận dụng state/storage đang có. Tài khoản + route/group/conversation phải tạo context độc lập.

- [ ] Viết và chạy test thất bại cho back khi có bản nháp, giữ/khôi phục theo context, hủy đổi media, gửi lỗi và xóa bản nháp khi thành công.
- [ ] Giữ bản nháp trong session hoặc cảnh báo rời màn soạn theo phạm vi đã duyệt; không ghi file nhị phân/secret vào storage.
- [ ] Nếu người dùng chọn rời, giữ draft để mở lại; chỉ thao tác bỏ draft hoặc gửi thành công mới xóa. Đính kèm cần được giữ hoặc báo rõ cần chọn lại.
- [ ] Hủy picker giữ tệp cũ; khóa trường và hành động lúc gửi, hiển thị file preview và tiến trình có thể biết từ uploader hiện tại.
- [ ] Kiểm tra account switch/signout, Jest, typecheck/lint; report, root review và commit/push riêng.

### Task 3: Album ảnh web đáng tin cậy

**Files:** `frontend/web/src/pages/photos/PhotosPage.tsx`, `AlbumDetailPage.tsx`, `PhotoViewer.tsx` nếu cần, tests/browser/albumRecovery.mjs.
**Interfaces:** Tái dùng `photosApi` và `mediaApi.uploadFile`; reuse AppDialog và useDialogFocus.

- [x] Viết browser tests chứng minh gửi lặp, lỗi upload ảnh thứ hai, tải album lỗi, caption/delete lỗi và late photo detail responses.
- [x] Khóa submit ngay lập tức; giữ ID album đã tạo và ID media đã upload/added để retry chỉ phần lỗi, không tạo album khác.
- [x] Hiển thị tiến trình theo số ảnh/progress uploader; ảnh đã xong còn trong album khi phần sau lỗi. Chặn đổi input khi đang upload.
- [x] Có loading/empty/error/retry riêng; tải trang/cuộn thêm lỗi giữ dữ liệu cũ, bảo vệ request khi đổi album/ảnh và thao tác đồng thời.
- [x] Kiểm tra input, confirm delete, dialog focus theo components hiện có, browser tests + web build/lint; report, root commit/push.

### Task 4: Tìm kiếm tổng hợp mobile

**Files:** `frontend/mobile/src/app/search.tsx`, `frontend/mobile/src/api/search.ts` chỉ nếu cần; feature/component nhỏ và tests ngoài app.
**Interfaces:** Dùng API tổng hợp/suggestions đã có; mở people/groups/posts/pages/events/hashtags qua route có thật. Không thêm route giả.

- [x] Viết test thất bại cho nhiều loại kết quả, lọc loại, empty/error/retry, truy vấn cũ trả trễ và context tài khoản.
- [x] Dùng React Query để khóa kết quả theo account/query/type, debounce hợp lý; Enter/tìm kiếm rõ ràng, loading không giả empty.
- [x] Hiển thị kết quả loại đã hỗ trợ và navigation đúng. Loại chưa có màn detail mở màn/list phù hợp đã có, không tạo placeholder.
- [x] Chạy Jest, typecheck/lint, self-review và report; root review, commit/push riêng.

### Task 5: Phục hồi lỗi route web

**Files:** `frontend/web/src/routes/index.tsx`, component RouteErrorPage gần routes, tests/browser/routeRecovery.mjs; preferences nếu cần.
**Interfaces:** Dùng `errorElement`, `useRouteError`, `useLocation`, Link và cơ chế reload trình duyệt có sẵn. Không thêm thư viện error tracking.

- [x] Browser test làm lỗi lazy module và route render, xác nhận giao diện tiếng Việt không stack trace và nút thử lại/về feed hoạt động.
- [x] Thêm error boundary cho login/root và child route để phục hồi cả chunk load/render, giữ shell khi có thể.
- [x] Retry đúng URL bằng tải lại thật để React.lazy không giữ rejected promise; route fallback không lặp lỗi, người chưa login mở login hợp lý.
- [x] Tôn trọng language/theme, semantic alert/status và keyboard focus; browser tests + web build/lint.
- [ ] Root review, commit/push riêng; chạy checks chung và export Android, xác nhận main và frontend sạch.
