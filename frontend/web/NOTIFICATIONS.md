# Notifications Web

Web dùng API Notification và hub hiện có. BE bổ sung field phản hồi tùy chọn và
phát hai notification Event đã có; giữ field, endpoint, cursor và tên hub cũ.
Không thêm dependency, migration hoặc thay kiến trúc SignalR.

## Luồng và thành phần

- Bell và đóng panel bằng click ngoài/ESC: `src/layout/TopNavbar.tsx`.
- Panel và trang `/notifications` dùng chung `NotificationCenter`, `NotificationList`
  và `NotificationAvatar`; màu, typography, spacing dùng theme Tailwind hiện có.
- Nội dung, actor, system icon và destination: mở rộng helper
  `src/shared/notificationPresentation.ts`.
- Thời gian: giữ `formatPostTimestamp`.
- Toast: mở rộng `toastState` và `ToastViewport` hiện có; tự đóng sau 5 giây.
- Avatar dùng `actorAvatarUrl`, qua `resolveProfileImageUrl` hiện có; ảnh lỗi hoặc
  DTO cũ dùng chữ cái từ tên actor thật. Không fetch profile riêng cho từng row.
  System/thiếu actor luôn dùng icon và câu hoàn chỉnh, kể cả DTO có avatar.

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
| GroupInvite | `/groups?invite=:entityId&group=:parentEntityId`; chọn đúng lời mời rồi mở `/groups/:groupId` sau khi chấp nhận |
| GroupJoinApproved | `/groups/:parentEntityId`; hỗ trợ entity Group trực tiếp như trước |
| PageRoleInvite | `/pages?invite=:entityId&pageId=:parentEntityId&page=:pageUsername`; chọn lời mời rồi mở `/pages/:pageId` sau khi chấp nhận |
| StoryReaction | `/stories/:entityId`, dùng StoryViewer hiện có; thiếu ID về `/feed` |

Group/Page lời mời được tìm qua cursor hiện có, dedupe, highlight và focus đúng
row. Flow này xử lý nhóm riêng tư/Trang chưa xuất bản mà không nới quyền xem trước
khi nhận lời mời. Nếu target không còn pending, kiểm tra quyền mở đúng parent một
lần để mở nội dung đã chấp nhận; không có quyền thì hiện trạng thái không khả dụng.
DTO cũ thiếu parent vẫn dùng `/groups` hoặc `/pages`. Query username dùng để trình
bày lời mời; navigation sau khi chấp nhận dùng ID thật để hỗ trợ Trang đổi username.

BE `NotificationService.ToResponsesAsync` dùng chung cho REST và SignalR, lấy
avatar cùng batch actor hiện có, batch GroupInvite/GroupJoinRequest/PageRoleInvitation
để trả `parentEntityId`, thêm `pageUsername`. Các field mới nullable và optional.
StoryId đã nằm trong DTO, không cần thêm field.

EventUpdated/EventCancelled được publish sau SaveChanges (và commit transaction
đối với update), cho đúng participants, bỏ notification tự gửi và không gửi lại
khi hủy lặp. So sánh schedule/location sau normalization để tránh phát update
khi dữ liệu thực không đổi. Event đã hủy có thể được xem bởi người có RSVP/lời mời
hợp lệ theo quyền host; không mở draft/deleted event hoặc nhóm riêng tư cho người
ngoài. Quyền đăng bài vẫn yêu cầu event published.

Story hết hạn, bị xóa hoặc không có quyền xem hiển thị trạng thái không khả dụng;
lỗi tải tạm thời có retry. Route không vượt qua privacy/expiry của API.

## File của phần bổ sung BE và Web

- BE: `NotificationPageResponse.cs`, `NotificationService.cs`, `EventsService.cs`,
  `EventAccessService.cs`; nằm trong các module Notifications/Events hiện có.
- Web: `src/api/notifications.ts`, `src/shared/notificationPresentation.ts`,
  `src/shared/components/NotificationAvatar.tsx`, `src/pages/groups/GroupsPage.tsx`,
  `src/pages/pages/PagesPage.tsx`, `src/pages/stories/StoryDetailPage.tsx`,
  `src/routes/index.tsx`.
- BE tests: bổ sung assertion vào Group/Page/PostEndpointsTests, thêm
  EventNotificationPublicationTests, CancelledEventAccessTests và transport recorder.
- Web tests: notification presentation helper và browser notifications; thêm
  Story/invitation browser checks cùng fixture riêng trong `tests/browser`.

## Kiểm tra

```sh
npm ci
npm run lint
npm run build
npm run test:notifications
npm run test:games
npm run test:auth
npm run test:group-header
git diff --check
../../scripts/test-backend.sh
```

Browser checks dùng Playwright có sẵn bên ngoài project, không thêm package app:

```sh
npm run dev -- --host 127.0.0.1 --port 5184 --strictPort
PLAYWRIGHT_MODULE=/tmp/last-signal-browser/node_modules/playwright/index.mjs \
NOTIFICATIONS_BASE_URL=http://127.0.0.1:5184 \
node tests/browser/notifications.mjs
PLAYWRIGHT_MODULE=/tmp/last-signal-browser/node_modules/playwright/index.mjs \
NOTIFICATIONS_BASE_URL=http://127.0.0.1:5184 \
node tests/browser/storyNotifications.mjs
PLAYWRIGHT_MODULE=/tmp/last-signal-browser/node_modules/playwright/index.mjs \
NOTIFICATIONS_BASE_URL=http://127.0.0.1:5184 \
node tests/browser/invitationNotifications.mjs
```

Fixture chỉ nằm trong tests: client SignalR thật negotiate/handshake/long-poll,
API mô phỏng DTO và cursor hiện có, cho phép lỗi/delay/reconnect để kiểm tra UX.
Kiểm tra viewport 375/768/1280/1920px, dark/light, keyboard, reduced motion,
realtime/dedupe, read rollback, mark-all với event đến đồng thời, loading/empty/
error/retry, pagination và deep-link. Đây là kiểm tra browser bằng fixture,
không thay cho demo với tài khoản/backend đang chạy.

BE integration tests dùng PostgreSQL tạm qua script của repo, kiểm tra dữ liệu
REST/SignalR cùng avatar/parent, publication sau persist và quyền xem Event đã
hủy. Kết quả: 462 BE tests (6/6 nhóm), 13 notification helper/state tests,
115 Web `.test.mjs` tests, 18 auth tests và 1 group-header test pass; 19 notification,
9 Story và 18 invitation browser checks pass. `npm ci`, lint/build và diff-check
pass. Build còn cảnh báo chunk lớn của game hiện có.
