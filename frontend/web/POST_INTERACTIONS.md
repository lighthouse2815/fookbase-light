# Tương tác Post trên frontend

Thay đổi chỉ thuộc `frontend/web`, dùng React, TypeScript, Tailwind và `postsApi` hiện có. Không thêm dependency, endpoint hoặc thay đổi contract API.

Điều hướng nhiều ảnh, loading/retry, preload, zoom/pan và swipe của lightbox Post được ghi tại [POST_PHOTO_LIGHTBOX.md](POST_PHOTO_LIGHTBOX.md).

## Trước và sau

| Phần | Trước | Sau |
| --- | --- | --- |
| Reaction | Chờ API, nhiều request có thể đè state của nhau | Đổi icon/count ngay; tuần tự hóa và gộp lựa chọn trung gian |
| Picker | Hover bằng CSS, thiếu điều hướng/focus và có thể bị cắt | Portal trên nút, fade/scale 180ms, tooltip, bàn phím, long press |
| Comment | Chờ server mới thêm; có thể double submit | Thêm tạm ngay, clear input, reconcile bằng khóa ổn định |
| Lỗi action | Lỗi inline và notice riêng từng card | Toast dùng chung, tối đa 3, deduplicate, tự đóng sau 5 giây |
| Dropdown | Mở/đóng trực tiếp, thiếu keyboard | Fade/scale, Arrow/Home/End, Tab, Escape, outside click, trả focus |
| Modal | Escape có thể đóng nhiều lớp; focus thoát ra feed | Trap focus, khóa scroll, chỉ đóng lớp trên cùng, trả focus |
| Cùng Post ở nhiều vị trí | State riêng trong từng card | Reaction/list/count dùng chung theo `viewerId:postId` |

Sáu reaction được giữ nguyên: `like`, `love`, `haha`, `wow`, `sad`, `angry`. Click reaction hiện đang chọn vẫn dùng DELETE như luồng Post trước đây. Các action ghim, lưu, sửa, quyền riêng tư, xóa, báo cáo và chia sẻ được giữ lại.

## Optimistic state và rollback

- `postInteractionState.ts` giữ hai state: server đã xác nhận và lựa chọn mới nhất của user. Chỉ một request reaction chạy tại một thời điểm cho một Post. Những lựa chọn trung gian được gộp; counter chỉ chuyển một phiếu của user giữa các loại.
- Response server cập nhật state đã xác nhận. Lựa chọn mới hơn vẫn được giữ trên UI. Nếu request cuối thất bại, rollback về state cuối đã xác nhận; lỗi của request trước không xóa lựa chọn mới hơn. Không refetch feed.
- Sau local intent, props của bản sao cũ hoặc snapshot optimistic từ action sửa Post không thay thế reaction đã xác nhận/rollback. Props có cùng reaction của viewer vẫn cập nhật count tăng/giảm của người khác.
- Trước mỗi request trong queue reaction, kiểm tra viewer của session hiện tại. Đổi tài khoản hủy các lựa chọn còn chờ của user cũ, rollback store cũ và không hiện toast sang user mới; refresh token của cùng user vẫn tiếp tục queue. Request đã gửi trước khi đổi tài khoản vẫn được reconcile cho store của user ban đầu.
- `postDiscussionState.ts` thêm comment với `clientId` và `pending`. Khi thành công, thay bằng Comment thật nhưng giữ `clientId` làm React key; deduplicate nếu một trang GET đã chứa server ID. Khi lỗi, chỉ gỡ dòng tạm và phần cộng counter của dòng đó.
- Guard đồng bộ chặn double submit, kể cả từ hai card của cùng Post. Input vẫn cho phép gõ bản nháp tiếp theo trong lúc gửi. Bản nháp thất bại và reply target chỉ được khôi phục nếu user chưa đổi chúng. Không set state của card đã unmount hoặc đã chuyển sang Post khác.
- Offset phân trang chỉ dựa trên các dòng server đã đọc, không tăng vì comment tạm/mới. Response GET cũ không ghi đè count hoặc edit mới. Nếu xóa dòng trước trong lúc tải trang kế tiếp, đọc lại trang comments tại offset đã sửa. Backend hiện chỉ xóa comment đó, giữ các reply; frontend tuân theo behavior này.
- Một DELETE có thể đã thay đổi offset trên server trước khi response tới frontend. Việc đọc trang chờ các DELETE đang chạy; trang đã đọc trong lúc DELETE chạy được bỏ và tải lại sau khi xóa hoàn tất. Trang comments mới cũng tăng version để counter GET cũ không ghi đè tổng mới hơn.
- Nếu GET comments và submit chạy đồng thời, số tổng có thể đã bao gồm comment mới hoặc chưa. Sau khi write hoàn tất, chỉ dùng GET Post hiện có để xác nhận counter trong trường hợp này; không tải lại feed. Response counter cũ cũng không ghi đè submit mới hơn.
- `usePostInteractions.ts` dùng `useSyncExternalStore`, subscription riêng từng Post. Dọn entry không hoạt động khi cache đạt ngưỡng 200; entry có subscriber/request vẫn được giữ. Optimistic reaction/comment không cập nhật state của Feed. Thứ tự key hoặc việc API bỏ count bằng zero không làm mất reaction đã xác nhận.

Cache phục vụ state trong tab hiện tại; không bổ sung đồng bộ realtime giữa tab/thiết bị. API tạo comment trả Comment, không trả tổng mới: frontend quản lý counter cho thao tác của mình và lấy tổng từ API tải comments khi phù hợp. Không bổ sung idempotency contract cho POST comment; guard ngăn gửi trùng trong phiên thao tác hiện tại.

API reaction không có revision để xác định độ mới của hai props cạnh tranh. Sau tương tác trong tab này, kết quả write đã xác nhận được ưu tiên cho reaction của viewer tới khi cache được tạo lại; thay đổi reaction của chính viewer từ thiết bị khác chưa được đồng bộ. Các response count chưa từng thấy nhưng có cùng reaction không thể phân biệt chắc chắn mới/cũ chỉ bằng contract hiện có.

## Accessibility và styling

- Nút reaction có `aria-pressed`, `aria-haspopup`, `aria-expanded`; popup dùng `menuitemradio` với `aria-checked`. Arrow keys/Home/End điều hướng; Enter/Space chọn; Escape trả focus; Tab rời popup.
- Long press 450ms trên touch mở picker, bị hủy khi di chuyển quá 10px hoặc pointer cancel; không gọi `preventDefault` để chặn scroll. Tap ngắn trên Post giữ hành vi Like/bỏ reaction hiện tại.
- Popup trong portal không làm card đổi kích thước hoặc bị overflow của ảnh/modal cắt. Vị trí được cập nhật khi scroll/resize và giới hạn trong viewport.
- Khi nút của Post cuộn hoàn toàn khỏi viewport, picker/dropdown tự đóng; không trả focus theo cách kéo trang trở lại nút.
- Animation 150–220ms; `prefers-reduced-motion` tắt scale/bounce/transition không cần thiết. Dùng token surface/text/border/primary/danger và theme sáng/tối hiện có.
- `useDialogFocus.ts` tái sử dụng logic focus của AppDialog cho modal Post/ảnh/reaction. Button có trạng thái disabled/aria-busy tại action cần thiết; Post không biến thành loading spinner.
- Khi tất cả controls trong modal đang disabled, Tab/Shift+Tab giữ focus trên panel; sau khi request thất bại và controls hoạt động lại, keyboard tiếp tục đi trong modal như bình thường.
- Toast có live region/status, nút đóng accessible; không dùng `alert()` và không block UI.

## File thay đổi

Các đường dẫn dưới đây tính từ `frontend/web`:

| File | Vai trò |
| --- | --- |
| `src/App.tsx` | Gắn toast viewport dùng chung |
| `src/pages/feed/components/LivePostCard.tsx` | Tích hợp state, action guards và modal |
| `src/pages/posts/PostDetailPage.tsx` | Reset card/draft/modal theo ID khi chuyển sang bài viết khác |
| `src/pages/feed/components/PostActionsMenu.tsx` | Dropdown accessible, portal và keyboard |
| `src/pages/feed/components/PostReactionPicker.tsx` | Picker desktop/touch/keyboard, animation |
| `src/pages/feed/components/PostDiscussion.tsx` | Comment tạm, key ổn định, composer và trạng thái action |
| `src/pages/feed/components/ShareDialog.tsx` | Reuse AppDialog, chống submit trùng, cleanup và toast |
| `src/pages/feed/components/postInteractionState.ts` | Queue reaction, optimistic counts, rollback |
| `src/pages/feed/components/postDiscussionState.ts` | Shared comments, optimistic submit, pagination và mutations |
| `src/pages/feed/components/usePostInteractions.ts` | Hook/cache theo viewer và Post |
| `src/pages/feed/components/useAnchoredPopup.ts` | Định vị hai popup trong viewport |
| `src/pages/feed/components/postInteractions.css` | Animation, focus, tooltip, reduced motion |
| `src/shared/components/ToastViewport.tsx` | UI toast theo theme |
| `src/shared/toastState.ts` | Deduplicate, giới hạn stack và timeout toast |
| `src/shared/useDialogFocus.ts` | Focus/scroll/Escape cho modal lồng nhau |
| `src/shared/components/AppDialog.tsx` | Reuse focus hook, hỗ trợ panel width |
| `src/shared/components/ReportButton.tsx` | Giữ report modal sau khi đóng menu; guard và focus |
| `tests/postInteractions.test.mjs` | Test reaction queue/rollback/toast |
| `tests/postDiscussion.test.mjs` | Test comment reconciliation/count/pagination/race |
| `tests/browser/postInteractionsFixture.mjs` | Response API có kiểm soát chỉ trong browser test |
| `tests/browser/postInteractions.mjs` | Các luồng tương tác thật qua browser |
| `tests/api/authClients.test.mjs` | Cache Vite tạm riêng, tránh làm invalid cache dev khi chạy test có sẵn |
| `POST_INTERACTIONS.md` | Báo cáo triển khai và cách kiểm tra |

## Verification

Từ `frontend/web`:

```bash
npm run lint
npx tsc -b
npm run build
npm run test:games
npm run test:auth
npm run test:group-header
```

Browser runner dùng Playwright có sẵn ở môi trường test, không thêm vào dependency production:

```bash
# Chỉ cần nếu môi trường chưa có Playwright/Chromium.
npm install --prefix /tmp/fookbase-post-browser --no-save playwright
node /tmp/fookbase-post-browser/node_modules/playwright/cli.js install chromium

POST_BASE_URL=http://127.0.0.1:5183 \
PLAYWRIGHT_MODULE=/tmp/fookbase-post-browser/node_modules/playwright/index.mjs \
node tests/browser/postInteractions.mjs
```

`CHROMIUM_EXECUTABLE` tùy chọn cho môi trường đã có browser executable. Đổi `POST_BASE_URL` sang preview server để kiểm tra bản production build.

Browser test đi qua UI và `postsApi` thật của frontend; response/delay/503 chỉ được kiểm soát trong test để kiểm chứng rollback, không có mock trong sản phẩm. Các ca gồm reaction/đổi/bỏ/rapid/fail, comment success/fail/double submit/bản nháp mới, picker/menu/Escape/keyboard/focus, modal lồng nhau/report/share/edit, Post gốc và bản chia sẻ, unmount/remount, mobile/desktop/resize/theme/reduced motion. Console chỉ bỏ qua HTTP 503 được cố ý tạo và việc SignalR bị hủy lần mount đầu bởi StrictMode đã có sẵn.

### Kết quả ngày 04/10/2026

| Kiểm tra | Kết quả |
| --- | --- |
| `npm run lint` | Đạt, không lỗi hoặc warning lint |
| TypeScript (`tsc -b` trong `npm run build`) | Đạt |
| `npm run build` | Đạt |
| `npm run test:games` | 102/102 đạt, gồm 34 test reaction/comment/toast và 68 test hiện có |
| `npm run test:auth` | 18/18 đạt |
| `npm run test:group-header` | 1/1 đạt |
| Browser trên dev `:5183` | 24/24 đạt |
| Browser trên production build preview `:5194` | 24/24 đạt |
| Console/page errors trong hai lượt browser | Không có lỗi/warning ngoài các trường hợp cố ý hoặc đã tồn tại được ghi ở trên |

Test race dùng Feed/Post đã tải với count bằng 0, sau đó API comments có 30 dòng và trả trang đầu chậm hơn submit. Sau khi gửi một comment, UI có count 31, trang đầu 21 dòng, tải tiếp đủ 31 dòng và chỉ một bản của comment mới. Các test logic còn kiểm tra phản hồi counter cũ không ghi đè submit mới, dữ liệu Post mới hoặc trừ hai lần khi xóa comment.

Lượt bổ sung kiểm tra props cũ sau thành công/rollback, trang đã phản ánh DELETE trước lúc response xóa về, counter đọc trước trang comments mới, focus modal khi mọi controls disabled, đổi tài khoản trong queue reaction, refresh token cùng viewer và đóng popup khi cuộn nút khỏi viewport. Ca popup được chạy thất bại với guard tắt, sau đó chạy đạt khi bật sửa lỗi.

Build vẫn có warning kích thước chunk `Game` khoảng 956 kB từ game 3D đã có; thay đổi Post không sửa phần game. Browser verification dùng response API có kiểm soát, không phải xác nhận các thao tác ghi với tài khoản thật trên production.
