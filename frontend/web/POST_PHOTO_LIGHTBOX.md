# Post Photo Lightbox

Hoàn thiện lightbox có sẵn từ `ca6f0b2`, chỉ trong `frontend/web`. Giữ modal, `useDialogFocus`, layout mobile và sidebar reaction/comment theo Post. Dùng `postsApi.getMediaAccess`, `MediaAccess[]` và `resolveProfileImageUrl` hiện có; không thêm dependency hoặc thay đổi backend/API.

## Hành vi

- Click ảnh thứ N mở đúng attachment theo `mediaId` và index trong toàn bộ danh sách media, kể cả danh sách có video. Previous/Next và phím ←/→ không vòng về đầu/cuối; counter chỉ xuất hiện khi có nhiều media.
- Navigation giữ nguyên node sidebar, scroll, comments, bản nháp và reaction. Không đọc lại Post/comments khi chỉ chuyển ảnh. Arrow keys trong input, textarea, contenteditable, video hoặc menu không điều hướng ảnh. ESC, focus trap và trả focus về ảnh đã click vẫn dùng hook modal hiện có.
- Vùng media nền đen giữ kích thước khi tải/lỗi. Ảnh dùng `object-fit: contain`; loading là placeholder nhỏ. URL hết hạn hoặc ảnh lỗi được resolve lại một lần qua access endpoint hiện có. Nút **Thử lại** chỉ resolve/tải media đang xem, giữ nguyên sidebar.
- Preload chỉ ảnh ngay trước/sau bằng `Image`, dùng URL đã resolve còn hạn; bỏ qua video và URL trùng. Không gọi API để preload, không tải trước toàn album. Lỗi preload không thay UI ảnh đang xem. Cleanup bỏ nguồn và handler của các Image tạm.
- Zoom có các mức **1×, 1,5×, 2×, 3×**. Nút +/−/đặt lại và double-click ảnh (2×/1×) dùng state riêng trong vùng media. Đổi media hoặc đóng/mở reset scale và pan.
- Khi zoom, kéo chuột/touch pan với cursor grab/grabbing. Translation bị clamp theo kích thước ảnh thực sự render × scale và viewport; ảnh nhỏ vẫn ở giữa. `ResizeObserver` clamp lại khi resize. Pointer move dùng `requestAnimationFrame` và không cập nhật state của Feed.
- Ở **1×**, touch ngang từ **60px**, với `abs(dx) > abs(dy)`, đổi ảnh. Ở **>1×**, gesture chỉ pan. Vùng sidebar và controls không khởi tạo gesture media; scroll dọc/cancel pointer không đổi ảnh. Video dùng controls native, không zoom và được pause khi rời media/đóng modal.
- Fade ảnh 160ms, tắt với `prefers-reduced-motion: reduce`; zoom/pan không có animation kéo dài. Controls có label tiếng Việt, focus-visible, hit target 44px và trạng thái `aria-disabled` để giữ focus tại boundary.

## Async và signed URL

Selection theo `mediaId`, fallback index được clamp nếu danh sách thay đổi. Refresh access deduplicate request đang chạy theo `postId:mediaId`. Response chỉ thay ảnh đang xem khi generation và media ID còn khớp; response muộn có thể cập nhật URL của đúng attachment trong danh sách nhưng không đổi selection. Image/video có key `mediaId:url:attempt`; handler load/error kiểm tra đúng element và source hiện tại. Các nguồn cũ không overwrite ảnh mới khi bấm next/next/previous nhanh.

## File

Các đường dẫn tính từ `frontend/web`:

| File | Vai trò |
| --- | --- |
| `src/pages/feed/components/LivePostCard.tsx` | Selection, navigation và keyboard; giữ modal/sidebar |
| `src/pages/feed/components/PostLightboxMedia.tsx` | Vùng media, loading/error/retry, preload, zoom/pan/swipe |
| `src/pages/feed/components/postPhotoLightbox.ts` | Index, neighbor preload, pan bounds, swipe và editable guard |
| `src/pages/feed/components/postLightbox.css` | Focus và fade/reduced motion |
| `tests/postPhotoLightbox.test.mjs` | 13 test logic |
| `tests/browser/postPhotoLightboxFixture.mjs` | API/media fixture chỉ dùng trong browser test |
| `tests/browser/postPhotoLightbox.mjs` | 21 browser checks desktop/laptop/375px/768px |
| `POST_INTERACTIONS.md`, `POST_PHOTO_LIGHTBOX.md` | Tài liệu tương tác và kiểm chứng |

## Kiểm chứng thực tế — 04/10/2026

| Kiểm tra | Kết quả |
| --- | --- |
| `npm ci` | Đạt, không thay lockfile/dependency |
| `npm run lint` | Đạt |
| `npm run build` (gồm TypeScript) | Đạt |
| `npm run test:games` | 128/128 đạt, gồm 13 test lightbox |
| `npm run test:auth` | 18/18 đạt |
| `npm run test:group-header` | 1/1 đạt |
| Lightbox browser — dev `:5184` | 21/21 đạt |
| Lightbox browser — production preview `:5197` | 21/21 đạt |
| Post interactions browser — dev `:5184` | 24/24 đạt |
| Post interactions browser — production preview `:5197` | 24/24 đạt |
| `git diff --check` | Đạt |

Browser checks đi qua UI/React/API client thật, với response API và media có kiểm soát trong test. Gồm ảnh đơn/nhiều ảnh, click ảnh thứ hai, boundaries, keyboard/textbox, sidebar scroll/draft/reaction persistence, expired URL, API/decode error và retry, preload ±1, response muộn khi next/next/previous, video phát native rồi pause, zoom/double-click/pan/clamp/resize/reset, mobile/tablet, reduced motion và focus/ESC. Native Chromium touch qua CDP kiểm tra cả swipe, pan và scroll sidebar; các ca threshold bổ sung dùng PointerEvents. Không có lỗi console/page ngoài lỗi API cố ý và SignalR StrictMode đã tồn tại. Đây là kiểm chứng dev/production build preview, không phải thao tác ghi trên tài khoản production thật.

Runner dùng Playwright/Chromium và FFmpeg đã có trong môi trường test, không thêm vào dependency ứng dụng. Có thể dùng `CHROMIUM_EXECUTABLE` nếu browser đã được cài riêng:

```bash
npm ci
npm run lint
npm run build
npm run test:games
npm run test:auth
npm run test:group-header
node --test tests/postPhotoLightbox.test.mjs

# Chạy dev hoặc production preview ở terminal riêng.
npm run dev -- --host 127.0.0.1 --port 5184 --strictPort
# npm run preview -- --host 127.0.0.1 --port 5197 --strictPort

PLAYWRIGHT_MODULE=/path/to/playwright/index.mjs \
PHOTO_BASE_URL=http://127.0.0.1:5184 PHOTO_VARIANT=dev \
node tests/browser/postPhotoLightbox.mjs

PLAYWRIGHT_MODULE=/path/to/playwright/index.mjs \
POST_BASE_URL=http://127.0.0.1:5184 \
node tests/browser/postInteractions.mjs
```

Đổi hai base URL sang `:5197` và `PHOTO_VARIANT=preview` để kiểm tra build production. Screenshot desktop/mobile/tablet mặc định ghi vào `/tmp/post-photo-lightbox-artifacts`; đổi bằng `PHOTO_ARTIFACTS`.

Pinch-to-zoom và zoom tại vị trí con trỏ không triển khai trong scope này; double-click zoom vào giữa ảnh. Nếu media bị xóa hoặc access endpoint vẫn trả lỗi, UI giữ error/retry thay vì tự tạo URL khác. Build còn warning chunk game 3D lớn đã có trước task.

Commit chức năng: `9b240a8` (navigation/loading/preload/retry), `a16ec08` (zoom/pan/swipe và browser checks), đã push `origin/main`.
