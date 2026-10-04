# Global Search UX

Hoàn thiện Search hiện có trong `frontend/web`, giữ router, API, debounce 300ms, cursor pagination và design tokens. Không sửa backend, thêm dependency hoặc thay kiến trúc Search.

## Audit và destination

Search cũ nằm trong `TopNavbar.tsx` và `SearchPage.tsx`. Autocomplete API hiện trả People/Groups/Pages. Backend Search đã có `type=events`, DTO Event và cursor, kể cả phần preview trong `type=all`; Web được nối trực tiếp vào contract đó.

Bug Post trước đây dùng container profile/group/page làm destination. Mọi kết quả Post giờ dùng PostId để mở route detail đã có, kể cả Post thuộc Event. Không fetch container để tìm destination.

| Kết quả | Route hiện có |
| --- | --- |
| Mọi người | `/profile/:userId` |
| Bài viết | `/posts/:postId` |
| Nhóm | `/groups/:groupId` |
| Trang | `/pages/:username` |
| Reels | `/reels?reel=:reelId` |
| Sự kiện | `/events/:eventId` |

`getSearchDestination` được dùng chung cho autocomplete và result cards, encode identifier, giữ convention username của Trang và query của Reel.

## Hành vi

- Input dùng combobox/listbox/option, `aria-expanded`, `aria-selected`, `aria-activedescendant`. ↑/↓ chọn suggestion có giới hạn; Enter mở suggestion đang chọn hoặc tìm query; ESC đóng dropdown và giữ text/focus. Hover cập nhật lựa chọn; click ngoài đóng; click lại input đang focus mở lại dropdown. Không submit Enter khi IME đang composition.
- Query đổi reset selection. Skeleton gồm 4 row ở dropdown và trang kết quả, không hiện khi chưa đủ 2 ký tự. Dropdown có empty/error/retry và action xem tất cả. Clear input giữ focus và mở lịch sử. Chỉ thêm/bớt khoảng trắng vẫn giữ response hợp lệ, không kẹt skeleton hoặc request lại query giống nhau.
- Lịch sử lưu tối đa 10 query theo viewer ở `fookbase.search.recent.<userId>`. Chỉ lưu tìm kiếm thực sự bằng Enter, xem tất cả hoặc chọn query gần đây. Trim, bỏ rỗng, giới hạn 100 ký tự, dedupe không phân biệt hoa/thường và đưa query mới lên đầu. Click entity không lưu raw query. Xóa từng query/xóa tất cả không navigate; JSON hỏng, storage bị chặn hoặc quota lỗi không crash Search.
- Highlight dùng substring không phân biệt hoa/thường và dấu tiếng Việt, kể cả `dang` → `Đăng`. `Intl.Segmenter` giữ grapheme/emoji/combining marks; nội dung luôn là React text spans, không dùng HTML injection hoặc fuzzy search.
- Mobile dưới breakpoint `sm` mở overlay full-height qua portal và hook `useDialogFocus` hiện có. Autofocus, body scroll lock, Tab trap, ESC/back button, đóng khi navigate/Browser Back và restore focus về trigger. Header cố định trong overlay, danh sách scroll riêng; safe-area và `100dvh` giữ layout khi viewport thấp. Tablet 768px giữ input/dropdown desktop.
- Trang kết quả giữ `q`/`type` trên URL; đổi tab reset cursor và Back/Forward restore query/category. Tab Sự kiện có cover/calendar fallback, tên, thời gian, địa điểm/online, host và số người tham gia từ DTO; không fetch detail để trang trí card.
- Loading/empty/error đều có tiếng Việt. Retry initial fetch giữ query/tab; load-more lỗi giữ card/cursor và retry ở cuối. Chuỗi dài không có khoảng trắng trong Post/Event được ngắt dòng, tránh overflow. Skeleton dùng `motion-safe:animate-pulse`; kiểm tra theme sáng/tối và reduced motion.

## Requests và dedupe

Autocomplete giữ debounce 300ms, hủy request cũ bằng `AbortController` và kiểm tra generation trước khi cập nhật response. Full results cũng hủy request/generation khi query/tab đổi; state gắn với query/type nên không flash kết quả hoặc empty của tab cũ.

Suggestions dedupe theo type + ID, không theo tên. Cursor results merge theo ID riêng của mỗi loại, giữ thứ tự đã có và cursor mới. Guard request đang chạy chống double load-more. Không gọi thêm API profile/avatar/detail cho từng kết quả.

## File thay đổi

Đường dẫn tính từ `frontend/web`:

| File | Vai trò |
| --- | --- |
| `src/layout/TopNavbar.tsx` | Dùng Search chung cho desktop/mobile, giữ các popup khác |
| `src/api/search.ts` | Type/DTO Events đã có ở backend |
| `src/pages/search/GlobalSearch.tsx` | Autocomplete, keyboard, recent UI và mobile overlay |
| `src/pages/search/SearchPage.tsx` | Post destination, Events, URL/cursor và các trạng thái kết quả |
| `src/pages/search/searchPresentation.ts` | Mapping, selection, dedupe và highlight dùng chung |
| `src/pages/search/HighlightedText.tsx` | Render các đoạn text match an toàn |
| `src/pages/search/recentSearches.ts` | Lịch sử localStorage có xử lý lỗi |
| `tests/globalSearch.test.mjs` | 8 test routes/selection/highlight/dedupe/backward compatibility |
| `tests/recentSearches.test.mjs` | 5 test lịch sử, giới hạn và storage lỗi |
| `tests/browser/globalSearchFixture.mjs` | API fixture, delay/error/cursor và Post detail cho browser tests |
| `tests/browser/globalSearch.mjs` | 30 checks Search, mặc định chạy toàn bộ; screenshots ngoài repo |
| `GLOBAL_SEARCH.md` | Audit, behavior, kiểm chứng và hướng chạy lại |

## Kiểm chứng thực tế — 04/10/2026

| Kiểm tra | Kết quả |
| --- | --- |
| `npm ci` | Đạt, không đổi lockfile/dependency; audit 0 vulnerabilities |
| `npm run lint` | Đạt |
| `npm run build` | Đạt, gồm TypeScript |
| `npm run test:games` | 141/141 đạt, gồm 13 focused Search tests |
| `npm run test:auth` | 18/18 đạt |
| `npm run test:group-header` | 1/1 đạt |
| Search browser — dev `:5186` | 30/30 đạt |
| Search browser — production preview `:5196` | 30/30 đạt |
| Post interactions browser — dev `:5186` | 24/24 đạt |
| Notifications browser — dev `:5186` | 19/19 đạt |
| `git diff --check`, kiểm tra riêng `frontend/web` | Đạt |

Browser checks dùng UI/React/router/API client thật với fixture API được kiểm soát, không ghi lên tài khoản production. Gồm 4 loại container Post mở đúng detail; skeleton, keyboard, mouse, ESC, clear, safe highlight, error/retry, stale responses, Event cursor/dedupe, URL Back/Forward, lịch sử, mobile 375px, viewport thấp và tablet 768px. Đã xem screenshots dev/preview, dark/light và reduced motion. Các ca mobile chạy bằng Chromium emulation, không phải kiểm chứng bàn phím trên thiết bị iOS thật.

Không có console/page error ngoài HTTP 503 cố ý trong fixture và SignalR StrictMode đã tồn tại. Review độc lập và regression tests xác nhận sửa lỗi whitespace, mở lại dropdown và long-text overflow. Build còn warning chunk game 3D lớn đã có trước task.

```bash
npm ci
npm run lint
npm run build
npm run test:games
npm run test:auth
npm run test:group-header
node --test tests/globalSearch.test.mjs tests/recentSearches.test.mjs

# Chạy server ở terminal riêng.
npm run dev -- --host 127.0.0.1 --port 5186 --strictPort
# npm run preview -- --host 127.0.0.1 --port 5196 --strictPort

PLAYWRIGHT_MODULE=/path/to/playwright/index.mjs \
SEARCH_BASE_URL=http://127.0.0.1:5186 SEARCH_VARIANT=dev \
node tests/browser/globalSearch.mjs
```

Đổi base URL sang `:5196` và `SEARCH_VARIANT=preview` để chạy production build. Playwright/Chromium dùng từ môi trường browser check đã có, không thêm dependency cho Web. Screenshots mặc định ở `/tmp/global-search-artifacts`; đổi bằng `SEARCH_ARTIFACTS`. Có thể chạy phần riêng với `SEARCH_CHECK_PHASE=links|autocomplete|results|history|mobile`; mặc định `full`.

## Commit và giới hạn API

Các commit chức năng đã push `origin/main`:

- `205eb49`: sửa Post deep-link.
- `5f6d73e`: autocomplete bàn phím, highlight và trạng thái dropdown.
- `8a33972`: kết quả tìm kiếm, cursor và tab Sự kiện.
- `3b03391`: lịch sử tìm kiếm gần đây.
- `9e37777`: overlay mobile và focus/scroll handling.
- `2cf5904`: ổn định autocomplete, ngắt dòng và bổ sung regression checks.

Autocomplete vẫn chỉ có People/Groups/Pages vì suggestions API hiện trả 3 loại này. Posts/Reels/Events có trên trang kết quả. Event search backend hiện lọc trạng thái `PUBLISHED`; DTO không trả cancelled status, nên Web không thể hiển thị nhãn hủy hoặc tự thêm cancelled result. Không sửa backend hoặc fake dữ liệu để bù các giới hạn đó. Thay đổi worktree backend không liên quan được giữ nguyên.
