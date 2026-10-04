# Notifications Web

Phạm vi thay đổi chỉ nằm trong `frontend/web`. Không thêm dependency, đổi API,
backend, database hoặc hạ tầng SignalR.

## Luồng và thành phần

- Bell và đóng panel bằng click ngoài/ESC: `src/layout/TopNavbar.tsx`.
- Panel và trang `/notifications` dùng chung `NotificationCenter`, `NotificationList`
  và `NotificationAvatar`; màu, typography, spacing dùng theme Tailwind hiện có.
- Nội dung, actor, system icon và destination: mở rộng helper
  `src/shared/notificationPresentation.ts`.
- Thời gian: giữ `formatPostTimestamp`.
- Toast: mở rộng `toastState` và `ToastViewport` hiện có; tự đóng sau 5 giây.
- Notification DTO không có avatar URL; chỉ dùng chữ cái từ tên actor thật,
  không fetch profile riêng. System/thiếu actor dùng icon và câu hoàn chỉnh.

API hiện có trong `src/api/notifications.ts`:

| Việc | Endpoint |
| --- | --- |
| Danh sách | `GET /api/notifications?limit=20&before=...` |
| Unread count | `GET /api/notifications/unread-count` |
| Đọc một | `POST /api/notifications/:id/read` |
| Đọc tất cả | `POST /api/notifications/read-all` |

Provider vẫn kết nối `/hubs/notifications`, nhận `NotificationReceived` và
refetch khi reconnect. Count lấy baseline từ API; một event unread mới tăng ngay
đúng 1. ID đã thấy từ event hoặc query không tăng lại, không phát toast lại.
Merge giữ row realtime, thứ tự đang có, cursor và các read đang optimistic.
Response count cũ bị bỏ nếu có mutation/event trong lúc request đang chạy;
provider lấy lại count để đồng bộ với backend.

Đọc một/tất cả cập nhật row và badge ngay; chặn double-submit, rollback chỉ phần
đã thay đổi và giữ notification mới nhận trong lúc request đang chạy. Lỗi hiện
toast nhỏ. Async response được kiểm tra session để không cập nhật phiên đã đóng.

Panel có filter Tất cả/Chưa đọc, nhóm Mới/Trước đó, skeleton initial loading,
empty state riêng cho từng filter, retry initial fetch và retry load-more giữ
list. Scroll giữ vị trí đọc khi prepend realtime; panel đóng thì toast có thể
hiện khi destination khác trang đang xem. Bell/badge/row/popover dùng CSS animation
ngắn, có `prefers-reduced-motion`; keyboard không bị trap, ESC trả focus về bell.

## Deep-link và giới hạn dữ liệu

| Type | Destination |
| --- | --- |
| FriendRequestReceived, FriendRequestAccepted, UserFollowed | `/profile/:actorUserId`; thiếu actor dùng `/explore` |
| PostReaction, PostShared, PostMention | `/posts/:entityId` khi entity là Post |
| PostComment | Nếu entity là Comment: `/posts/:parentEntityId#comment-:entityId`; nếu entity là Post: mở Post |
| CommentReaction, CommentMention | Parent Post, thêm comment hash khi có CommentId; thiếu PostId về `/notifications` |
| EventInvite | `/events/:parentEntityId`; thiếu parent về `/events`, không dùng InvitationId |
| EventUpdated, EventCancelled | `/events/:entityId` |
| AccountWarning | `/settings/security` |
| GroupInvite, GroupJoinApproved | `/groups/:entityId` nếu entity thật là Group; DTO invite/request hiện thiếu GroupId nên dùng `/groups` |
| PageRoleInvite | `/pages`; DTO thiếu page username mà route chi tiết yêu cầu |
| StoryReaction | `/feed`; hiện chưa có route mở trực tiếp StoryId |

Audit backend cho thấy EventUpdated/EventCancelled hiện chỉ queue notification,
chưa publish vào hub. Web hiển thị khi fetch/reconnect, nhưng không thể nhận
realtime cho event server chưa phát. Không sửa backend trong task này.

## Kiểm tra

Các file thay đổi: `src/layout/TopNavbar.tsx`,
`src/pages/notifications/NotificationsPage.tsx`, `src/realtime/RealtimeProvider.tsx`,
`src/realtime/context.ts`, `src/realtime/notificationState.ts`,
`src/shared/notificationPresentation.ts`, `src/shared/toastState.ts`,
`src/shared/components/{NotificationAvatar,NotificationCenter,NotificationList,ToastViewport}.tsx`,
`src/shared/components/notifications.css`, `package.json`, hai test helper/state
và hai file browser fixture/checks notification.

```sh
npm ci
npm run lint
npm run build
npm run test:notifications
npm run test:games
npm run test:auth
git diff --check -- frontend/web
```

Browser checks dùng Playwright có sẵn bên ngoài project, không thêm package app:

```sh
npm run dev -- --host 127.0.0.1 --port 5184 --strictPort
PLAYWRIGHT_MODULE=/tmp/last-signal-browser/node_modules/playwright/index.mjs \
NOTIFICATIONS_BASE_URL=http://127.0.0.1:5184 \
node tests/browser/notifications.mjs
```

Fixture chỉ nằm trong tests: client SignalR thật negotiate/handshake/long-poll,
API mô phỏng DTO và cursor hiện có, cho phép lỗi/delay/reconnect để kiểm tra UX.
Kiểm tra viewport 375/768/1280/1920px, dark/light, keyboard, reduced motion,
realtime/dedupe, read rollback, mark-all với event đến đồng thời, loading/empty/
error/retry, pagination và deep-link. Đây là kiểm tra browser bằng fixture,
không thay cho demo với tài khoản/backend đang chạy.

Kết quả: lint/build pass; 12 test notification, 18 browser checks,
114 test `.test.mjs` của Web và 18 test auth pass. Build còn cảnh báo chunk lớn
của game hiện có. Diff-check phạm vi Web sạch; diff-check toàn repo báo whitespace
trong 9 DTO Identity backend đang thay đổi từ trước, được giữ nguyên ngoài phạm vi.
