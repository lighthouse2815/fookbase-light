# Fookbase Light

Foundation cho mạng xã hội theo kiến trúc microservices. Milestone hiện tại gồm React frontend, YARP API Gateway, Identity Service với JWT/refresh-token rotation, Users Service quản lý social profile, Friends Service quản lý quan hệ xã hội, Posts Service quản lý bài viết/bình luận/reaction, Media Service lưu ảnh trên MinIO và hạ tầng PostgreSQL, RabbitMQ, Redis. Mỗi service sở hữu database riêng; dữ liệu cross-service được đồng bộ bất đồng bộ qua Transactional Outbox, RabbitMQ và consumer idempotent.

## Yêu cầu trên Linux

- .NET SDK 10
- Node.js và npm
- Docker Engine với Docker Compose plugin

Tạo cấu hình development trước khi chạy:

```bash
cp .env.example .env
```

Các giá trị trong `.env.example` chỉ dành cho máy development. Hãy thay toàn bộ password và JWT secret trước khi dùng ở môi trường khác.

## Restore và build .NET

```bash
dotnet restore FookbaseLight.sln
dotnet build FookbaseLight.sln --no-restore
dotnet test FookbaseLight.sln --no-build
```

Nếu máy chưa cài .NET SDK 10, có thể build bằng SDK image chính thức:

```bash
docker run --rm --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/workspace/.nuget/packages \
  -v "$PWD:/workspace" -w /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet restore FookbaseLight.sln

docker run --rm --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_HOME=/tmp \
  -e NUGET_PACKAGES=/workspace/.nuget/packages \
  -v "$PWD:/workspace" -w /workspace \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet build FookbaseLight.sln --no-restore
```

## Chạy infrastructure

Khởi động từng thành phần:

```bash
docker compose up -d postgres
docker compose up -d rabbitmq
docker compose up -d redis
docker compose up -d minio
```

Hoặc khởi động toàn bộ:

```bash
docker compose config
docker compose up -d
```

Xem trạng thái và log:

```bash
docker compose ps
docker compose logs -f
docker compose logs -f postgres
```

Dừng toàn bộ container nhưng giữ named volumes:

```bash
docker compose down
```

Các địa chỉ quản trị:

- RabbitMQ Management: <http://localhost:15672>
- MinIO Console: <http://localhost:9001>

Đăng nhập bằng credential tương ứng trong file `.env`.

## Chạy các ứng dụng

Nạp biến môi trường và chạy Identity Service:

```bash
set -a
source .env
set +a
dotnet run --project services/Identity/Fookbase.Identity.Api
```

Identity chạy tại <http://localhost:5001>; health check: <http://localhost:5001/health>.

Ở terminal khác, nạp cùng `.env` và chạy Users Service:

```bash
set -a
source .env
set +a
dotnet run --project services/Users/Fookbase.Users.Api
```

Users chạy tại <http://localhost:5002>; health check: <http://localhost:5002/health>.

Ở terminal khác, nạp cùng `.env` và chạy Friends Service:

```bash
set -a
source .env
set +a
dotnet run --project services/Friends/Fookbase.Friends.Api
```

Friends chạy tại <http://localhost:5003>; health check: <http://localhost:5003/health>.

Ở terminal khác, nạp cùng `.env` và chạy Posts Service:

```bash
set -a
source .env
set +a
dotnet run --project services/Posts/Fookbase.Posts.Api
```

Posts chạy tại <http://localhost:5004>; health check: <http://localhost:5004/health>.

Ở terminal khác, nạp cùng `.env` và chạy Media Service:

```bash
set -a
source .env
set +a
dotnet run --project services/Media/Fookbase.Media.Api
```

Media chạy tại <http://localhost:5005>; health check: <http://localhost:5005/health>.

Ở terminal khác, chạy Gateway:

```bash
dotnet run --project services/Gateway/Fookbase.Gateway
```

Gateway chạy tại <http://localhost:5000>; health check: <http://localhost:5000/health>. Các request `/api/auth/**`, `/api/users/**`, `/api/friends/**`, `/api/posts/**` và `/api/media/**` lần lượt được chuyển tiếp tới Identity `:5001`, Users `:5002`, Friends `:5003`, Posts `:5004` và Media `:5005`.

Chạy React frontend:

```bash
cd frontend/web
npm install
npm run dev
```

Frontend chạy tại <http://localhost:5173>.

Nếu host chưa có Node phù hợp hoặc đường dẫn workspace chứa ký tự đặc biệt, có thể chạy frontend qua Node container:

```bash
docker run --rm --user "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -v "$PWD/frontend/web:/workspace" -w /workspace \
  node:24-bookworm-slim npm install

docker run --rm --user "$(id -u):$(id -g)" \
  -e HOME=/tmp -p 5173:5173 \
  -v "$PWD/frontend/web:/workspace" -w /workspace \
  node:24-bookworm-slim npm run dev
```

## Authentication API

| Method | Endpoint | Authentication | Kết quả chính |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | Không | Tạo user và trả access/refresh token (`201`) |
| POST | `/api/auth/login` | Không | Đăng nhập và trả token pair (`200`) |
| POST | `/api/auth/refresh` | Không | Rotate refresh token và trả token pair mới (`200`) |
| POST | `/api/auth/logout` | Bearer JWT | Revoke refresh token hiện tại (`204`) |
| GET | `/api/auth/me` | Bearer JWT | Trả id, email, username (`200`) |

Các endpoint trả `400` khi request không hợp lệ, `401` khi credential/token không hợp lệ và `409` khi email hoặc username đã tồn tại. Qua Gateway, dùng base URL `http://localhost:5000`; Identity trực tiếp dùng `http://localhost:5001`.

JWT signing key chỉ được đọc từ `Jwt__SigningKey` trong environment. Refresh token raw chỉ trả cho client; database lưu SHA-256 hash. Access token mặc định hết hạn sau 15 phút và refresh token sau 30 ngày.

## Users Service

Users Service giữ social profile trong `users_db`, độc lập hoàn toàn với `identity_db`. `UserId` là global identifier do Identity phát hành; không có foreign key cross-database. Users không lưu password hash, refresh token, JWT, authentication email hoặc secret.

| Method | Endpoint | Authentication | Kết quả chính |
| --- | --- | --- | --- |
| GET | `/api/users/{userId}` | Không | Trả public profile (`200`) hoặc `404` |
| GET | `/api/users/me` | Bearer JWT | Trả profile của claim `sub` (`200`) |
| PATCH | `/api/users/me` | Bearer JWT | Sửa display name, bio, ngày sinh và thành phố (`200`) |

Users tự validate JWT bằng issuer, audience và signing key từ environment; service không gọi HTTP sang Identity cho mỗi request. Username vẫn do Identity sở hữu và không thể đổi qua Users API. `AvatarUrl` và `CoverUrl` đã có trong profile model nhưng luồng tự động gắn Media vào profile chưa nằm trong milestone này.

## Friends Service

Friends Service sở hữu `friends_db` và chỉ giữ `Guid UserId` làm global identifier. Service không reference domain của Identity/Users, không query `identity_db`/`users_db`, không copy profile và tự validate JWT từ cùng issuer, audience, signing configuration. Mọi endpoint dưới đây đều yêu cầu Bearer JWT; actor luôn lấy từ claim `sub`.

| Method | Endpoint | Kết quả chính |
| --- | --- | --- |
| POST | `/api/friends/requests/{userId}` | Gửi lời mời (`201`) |
| DELETE | `/api/friends/requests/{requestId}` | Sender hủy lời mời pending (`204`) |
| POST | `/api/friends/requests/{requestId}/accept` | Receiver chấp nhận và tạo friendship (`200`) |
| POST | `/api/friends/requests/{requestId}/decline` | Receiver từ chối (`204`) |
| DELETE | `/api/friends/{userId}` | Unfriend (`204`) |
| GET | `/api/friends?offset=0&limit=20` | Danh sách bạn bè (`200`) |
| GET | `/api/friends/requests/incoming` | Lời mời pending nhận được (`200`) |
| GET | `/api/friends/requests/outgoing` | Lời mời pending đã gửi (`200`) |
| GET | `/api/friends/status/{userId}` | Trạng thái quan hệ (`200`) |
| GET | `/api/friends/mutual/{userId}` | IDs bạn chung (`200`) |
| POST | `/api/friends/blocks/{userId}` | Block user (`204`) |
| DELETE | `/api/friends/blocks/{userId}` | Unblock user (`204`) |
| GET | `/api/friends/blocks` | Danh sách đã block (`200`) |

Collection endpoints dùng offset pagination, mặc định `limit=20`, tối đa `100`. Response lỗi chính gồm `400` cho input/operation không hợp lệ, `401` cho JWT thiếu hoặc sai, `403` khi actor không có quyền thao tác request, `404` khi resource/user không tồn tại và `409` cho conflict.

### Domain rules

`Friendship` lưu cặp user theo thứ tự canonical nên A–B và B–A không thể trở thành hai friendship. Pending request cũng có canonical pair và partial unique index; PostgreSQL advisory transaction lock theo cặp user xử lý an toàn hai request ngược chiều hoặc hai accept chạy đồng thời, kể cả khi service scale nhiều instance.

Không thể request/block chính mình, request một friendship có sẵn, tạo duplicate/reverse pending request hoặc thao tác request khi không đúng vai trò. Chỉ receiver được accept/decline, chỉ sender được cancel. Block A → B trong một transaction sẽ tạo block, xóa friendship, cancel mọi pending request giữa hai bên và ghi event vào Outbox. Sau đó cả A → B và B → A đều không thể gửi/accept request; unblock không phục hồi friendship cũ.

### KnownUsers, Inbox và Outbox

Friends subscribe `identity.user.registered.v1` bằng durable queue riêng và chỉ project `UserId`, `Username`, `CreatedAtUtc` vào `KnownUsers`. Consumer dùng manual ACK; insert `KnownUsers` và `InboxMessages` nằm trong cùng transaction. Primary/unique keys khiến cùng `EventId` hoặc `UserId` được deliver lặp vẫn idempotent.

Các thay đổi quan hệ ghi immutable event vào `friends_db.OutboxMessages` trong cùng transaction với business state. Worker publish persistent message bằng publisher confirmations lên durable topic exchange, sau đó mới đánh dấu `ProcessedAtUtc`. Khi RabbitMQ offline, API vẫn commit; `RetryCount`/`LastError` được cập nhật và worker retry có backoff. Delivery là at-least-once.

RabbitMQ topology của Friends:

- Exchange publish: `fookbase.friends.events` (durable topic)
- Routing keys: `friends.request.sent.v1`, `friends.request.accepted.v1`, `friends.friendship.removed.v1`, `friends.user.blocked.v1`
- Queue consume registration: `fookbase.friends.user-registered.v1` (durable)
- Message: persistent; consumer manual ACK

## Posts Service

Posts Service sở hữu `posts_db`, lưu bài viết, bình luận và reaction. Service không query database của Identity/Friends; thay vào đó nó consume integration event để project `KnownUsers`, `FriendEdges` và `BlockedEdges`. Các projection giữ `LastChangedAtUtc` nên event cũ đến muộn không ghi đè trạng thái mới.

| Method | Endpoint | Authentication | Kết quả chính |
| --- | --- | --- | --- |
| POST | `/api/posts` | Bearer JWT | Tạo bài viết (`201`) |
| PUT | `/api/posts/{postId}` | Bearer JWT, tác giả | Sửa nội dung/privacy (`200`) |
| DELETE | `/api/posts/{postId}` | Bearer JWT, tác giả | Soft-delete bài viết (`204`) |
| GET | `/api/posts/{postId}` | Tùy chọn | Đọc bài viết nếu có quyền (`200`) |
| GET | `/api/posts/feed?offset=0&limit=20` | Bearer JWT | Feed public, bạn bè và của chính mình (`200`) |
| GET | `/api/posts/users/{userId}` | Tùy chọn | Danh sách bài viết được phép xem (`200`) |
| POST | `/api/posts/{postId}/comments` | Bearer JWT | Tạo comment/reply (`201`) |
| GET | `/api/posts/{postId}/comments` | Tùy chọn | Danh sách comment (`200`) |
| PUT/DELETE | `/api/posts/comments/{commentId}` | Bearer JWT, tác giả | Sửa/xóa comment (`200`/`204`) |
| PUT/DELETE | `/api/posts/{postId}/reaction` | Bearer JWT | Đặt/đổi/gỡ reaction (`200`) |
| GET | `/api/posts/{postId}/media/{mediaId}/access` | Bearer JWT, có quyền xem post | Cấp presigned GET ngắn hạn (`200`) |

Privacy hợp lệ gồm `public`, `friends` và `onlyMe`; reaction gồm `like`, `love`, `haha`, `wow`, `sad`, `angry`. Feed và collection dùng offset pagination với `limit` tối đa `100`. Bài viết/comment dùng soft-delete. Mỗi user chỉ có một reaction trên một bài viết; gọi PUT lần nữa sẽ đổi reaction hiện tại.

Block theo một trong hai chiều ẩn toàn bộ bài viết giữa hai user và chặn comment/reaction. Bài viết `friends` chỉ hiển thị khi `FriendEdges` đang active. API trả `404` cho bài viết không được phép đọc để không làm lộ sự tồn tại, `403` cho mutation không thuộc quyền sở hữu và `409` khi interaction bị block.

Posts ghi event tạo/sửa/xóa bài viết, tạo comment và đổi reaction vào transactional outbox. Consumer dùng ba durable queue riêng:

- `fookbase.posts.user-registered.v1` nhận `identity.user.registered.v1`.
- `fookbase.posts.friend-events.v1` nhận friend accepted/removed và user blocked/unblocked.
- `fookbase.posts.media-events.v1` nhận media ready/deleted.
- Exchange publish: `fookbase.posts.events`; delivery at-least-once, Inbox xử lý idempotent.

## Media Service

Media Service sở hữu `media_db` và bucket private `fookbase-media`. Upload chính là direct-to-MinIO bằng presigned PUT; client không nhận storage credential hoặc object key. Ảnh JPEG/PNG/WebP tối đa mặc định 20 MB, video MP4/WebM tối đa mặc định 500 MB. Upload URL hết hạn sau 15 phút và read URL sau 5 phút.

| Method | Endpoint | Authentication | Kết quả chính |
| --- | --- | --- | --- |
| POST | `/api/media/uploads` | Bearer JWT | Tạo `PendingUpload` và presigned PUT (`201`) |
| POST | `/api/media/{mediaId}/complete` | Bearer JWT, chủ sở hữu | Verify object/signature rồi chuyển `Ready` (`200`) |
| GET | `/api/media/{mediaId}` | Bearer JWT, chủ sở hữu | Đọc metadata; không stream object (`200`) |
| DELETE | `/api/media/{mediaId}` | Bearer JWT, chủ sở hữu | Từ chối media đang được tham chiếu, nếu không soft-delete (`204`) |

`complete` stat object trên MinIO, so sánh kích thước khai báo/thực tế và kiểm tra magic signature; MIME header của client không được tin cậy riêng lẻ. Complete idempotent và chỉ tạo một `media.asset.ready.v1` Outbox event. Delete tạo `media.asset.deleted.v1` và durable object-deletion job trong cùng transaction. Worker retry object deletion và dọn `PendingUpload` hết hạn mà không xóa media Ready/referenced.

Posts project `KnownMedia` từ Media events, lưu attachment trong `PostMedia` và phát `posts.media.attached.v1`/`posts.media.detached.v1`. Posts kiểm tra privacy/block/attachment trước khi gọi endpoint nội bộ `POST /internal/media/{id}/read-url` bằng `InternalServices__Token`. Gateway không route `/internal/**`; kể cả public post vẫn trả presigned GET ngắn hạn vì bucket không public.

Migration corrective đánh dấu metadata legacy không thể chứng minh object tồn tại thành `Failed`; record đã soft-delete thành `Deleted`. Đây là reconciliation bảo toàn dữ liệu, không fake object và không reset volume.

## Register → profile event flow

Khi `/api/auth/register` thành công, Identity ghi user, refresh token và `UserRegisteredIntegrationEvent` vào `identity_db` trong cùng transaction. API trả `201` ngay sau database commit, không phụ thuộc RabbitMQ đang online.

Outbox worker đọc message chưa xử lý, publish persistent message lên durable RabbitMQ topology rồi mới đánh dấu `ProcessedAtUtc`. Publish failure tăng `RetryCount`, lưu `LastError` và retry có backoff; record không bị xóa để hỗ trợ audit. Delivery là at-least-once.

Users consumer dùng manual ACK. Trong một transaction của `users_db`, consumer tạo `UserProfile` mặc định và ghi `InboxMessages`. `InboxMessages.EventId` và `UserProfiles.UserId` là unique/primary key, vì vậy cùng một event được deliver nhiều lần vẫn chỉ tạo một profile. ACK chỉ xảy ra sau commit.

RabbitMQ topology:

- Exchange: `fookbase.identity.events` (durable topic)
- Routing key/event type: `identity.user.registered.v1`
- Queues: `fookbase.users.user-registered.v1`, `fookbase.friends.user-registered.v1`, `fookbase.posts.user-registered.v1` và `fookbase.media.user-registered.v1` (durable)
- Message: persistent

Event chỉ chứa `EventId`, `UserId`, `Username` và `OccurredAtUtc`; email không được copy sang social profile.

## EF Core migrations

Khôi phục local tool và apply migration hiện có:

```bash
set -a
source .env
set +a
dotnet tool restore
dotnet tool run dotnet-ef database update \
  --project services/Identity/Fookbase.Identity.Infrastructure \
  --startup-project services/Identity/Fookbase.Identity.Api

dotnet tool run dotnet-ef database update \
  --project services/Users/Fookbase.Users.Infrastructure \
  --startup-project services/Users/Fookbase.Users.Api

dotnet tool run dotnet-ef database update \
  --project services/Friends/Fookbase.Friends.Infrastructure \
  --startup-project services/Friends/Fookbase.Friends.Api

dotnet tool run dotnet-ef database update \
  --project services/Posts/Fookbase.Posts.Infrastructure \
  --startup-project services/Posts/Fookbase.Posts.Api

dotnet tool run dotnet-ef database update \
  --project services/Media/Fookbase.Media.Infrastructure \
  --startup-project services/Media/Fookbase.Media.Api
```

Tạo migration mới khi model thay đổi:

```bash
dotnet tool run dotnet-ef migrations add MigrationName \
  --project services/Identity/Fookbase.Identity.Infrastructure \
  --startup-project services/Identity/Fookbase.Identity.Api \
  --output-dir Persistence/Migrations

dotnet tool run dotnet-ef migrations add MigrationName \
  --project services/Users/Fookbase.Users.Infrastructure \
  --startup-project services/Users/Fookbase.Users.Api \
  --output-dir Persistence/Migrations

dotnet tool run dotnet-ef migrations add MigrationName \
  --project services/Friends/Fookbase.Friends.Infrastructure \
  --startup-project services/Friends/Fookbase.Friends.Api \
  --output-dir Persistence/Migrations

dotnet tool run dotnet-ef migrations add MigrationName \
  --project services/Posts/Fookbase.Posts.Infrastructure \
  --startup-project services/Posts/Fookbase.Posts.Api \
  --output-dir Persistence/Migrations

dotnet tool run dotnet-ef migrations add MigrationName \
  --project services/Media/Fookbase.Media.Infrastructure \
  --startup-project services/Media/Fookbase.Media.Api \
  --output-dir Persistence/Migrations
```

PostgreSQL init script tự tạo `users_db`, `friends_db`, `posts_db` và `media_db` trên volume mới. Với volume development đã tồn tại từ trước các milestone này, tạo database còn thiếu một lần mà không reset volume:

```bash
for database in users_db friends_db posts_db media_db; do
  docker compose exec -T postgres sh -lc '
    database="$1"
    psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc \
      "SELECT 1 FROM pg_database WHERE datname = '\''$database'\''" | grep -q 1 ||
    createdb -U "$POSTGRES_USER" -O "$POSTGRES_USER" "$database"
  ' sh "$database"
done
```

Integration tests dùng năm PostgreSQL database development, vì vậy cần chạy `docker compose up -d postgres`, apply đủ migration và nạp `.env` trước khi `dotnet test`. Media integration tests thay MinIO bằng object storage in-memory; test không ghi object vào bucket development.

Nếu `identity_db` đã có user trước khi Friends queue tồn tại, chạy explicit development backfill sau khi cả RabbitMQ và Friends đang hoạt động:

```bash
./scripts/replay-user-registrations-for-new-consumers.sh --confirm
```

Script chỉ reset trạng thái publish của các `identity.user.registered.v1` Outbox records để Identity phát lại. Inbox của Users/Friends/Posts/Media loại duplicate nên thao tác an toàn cho development; chạy sau khi Media đã tạo queue để backfill `KnownUsers`. Đây không phải runtime cross-database dependency.

Nếu Posts được thêm sau khi đã có user và quan hệ xã hội, chạy backfill dành riêng cho consumer mới sau khi Identity, Friends, Posts và RabbitMQ đều hoạt động:

```bash
./scripts/replay-events-for-posts-consumer.sh --confirm
```

Script phát lại registration cùng các event friend accepted/removed và block/unblock. `InboxMessages.EventId` chống duplicate; `LastChangedAtUtc` ngăn event quan hệ cũ ghi đè trạng thái mới. Script chỉ dành cho development.

## E2E và failure recovery

Gọi register qua Gateway, sau đó poll public profile vì đây là eventual consistency:

```bash
curl -i http://localhost:5000/api/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"email":"user@example.com","username":"user123","password":"Password123!"}'

curl -i http://localhost:5000/api/users/<USER_ID>
curl -i http://localhost:5000/api/users/me \
  -H 'Authorization: Bearer <ACCESS_TOKEN>'

curl -i -X PATCH http://localhost:5000/api/users/me \
  -H 'Authorization: Bearer <ACCESS_TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{"displayName":"User 123","bio":"Hello","dateOfBirth":"2000-01-02","currentCity":"Da Nang"}'
```

Để kiểm tra friend lifecycle, register A và B qua Gateway, poll tới khi `KnownUsers` đã nhận event (request có thể tạm trả `404` trong cửa sổ eventual consistency), rồi chạy:

```bash
# A gửi tới B
curl -i -X POST http://localhost:5000/api/friends/requests/<B_USER_ID> \
  -H 'Authorization: Bearer <A_ACCESS_TOKEN>'

# B xem incoming và accept REQUEST_ID
curl -i http://localhost:5000/api/friends/requests/incoming \
  -H 'Authorization: Bearer <B_ACCESS_TOKEN>'
curl -i -X POST http://localhost:5000/api/friends/requests/<REQUEST_ID>/accept \
  -H 'Authorization: Bearer <B_ACCESS_TOKEN>'

curl -i http://localhost:5000/api/friends \
  -H 'Authorization: Bearer <A_ACCESS_TOKEN>'
curl -i http://localhost:5000/api/friends/status/<B_USER_ID> \
  -H 'Authorization: Bearer <A_ACCESS_TOKEN>'
```

Tạo bài viết, đọc feed, comment và reaction qua Gateway:

```bash
curl -i -X POST http://localhost:5000/api/posts \
  -H 'Authorization: Bearer <A_ACCESS_TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{"content":"Hello Fookbase","privacy":"friends"}'

curl -i http://localhost:5000/api/posts/feed \
  -H 'Authorization: Bearer <B_ACCESS_TOKEN>'

curl -i -X POST http://localhost:5000/api/posts/<POST_ID>/comments \
  -H 'Authorization: Bearer <B_ACCESS_TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{"content":"Chào bạn!","parentCommentId":null}'

curl -i -X PUT http://localhost:5000/api/posts/<POST_ID>/reaction \
  -H 'Authorization: Bearer <B_ACCESS_TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{"type":"love"}'
```

Tạo upload intent qua Gateway, PUT trực tiếp vào URL trả về, complete và đọc metadata:

```bash
curl -i -X POST http://localhost:5000/api/media/uploads \
  -H 'Authorization: Bearer <ACCESS_TOKEN>' \
  -H 'Content-Type: application/json' \
  -d '{"fileName":"avatar.png","contentType":"image/png","sizeBytes":<FILE_SIZE>}'

curl -i -X PUT '<UPLOAD_URL>' -H 'Content-Type: image/png' --data-binary @/path/to/avatar.png

curl -i -X POST http://localhost:5000/api/media/<MEDIA_ID>/complete \
  -H 'Authorization: Bearer <ACCESS_TOKEN>'

curl -i http://localhost:5000/api/media/<MEDIA_ID> \
  -H 'Authorization: Bearer <ACCESS_TOKEN>'

curl -i -X DELETE http://localhost:5000/api/media/<MEDIA_ID> \
  -H 'Authorization: Bearer <ACCESS_TOKEN>'
```

MinIO dùng các tag đã pin `RELEASE.2025-09-07T16-13-09Z` và `mc RELEASE.2025-08-13T08-35-41Z`. `minio-bootstrap` tạo bucket, tắt anonymous policy và fail nếu bootstrap lỗi. `minio-cors` chỉ trả preflight PUT cho `http://localhost:5173`; MinIO Community không hỗ trợ bucket `PutBucketCors` ở release này nên proxy CORS development là enforcement point.

Khi MinIO tạm dừng, API đang chạy vẫn giữ state `PendingUpload`; PUT/complete trả lỗi dependency và không chuyển nhầm sang Ready. Khởi động MinIO và CORS proxy lại để retry upload/complete. Nếu outage xảy ra sau soft-delete, `ObjectDeletions` giữ job chưa xử lý để worker retry.

Kiểm tra Outbox recovery: `docker compose stop rabbitmq`, register hoặc thực hiện friend operation qua Gateway và xác nhận request vẫn commit (`201`/`204`); record tương ứng trong Outbox của service phải còn `ProcessedAtUtc = NULL`. Sau `docker compose start rabbitmq`, worker reconnect, publish event và đánh dấu record processed. Với registration, poll `/api/users/<USER_ID>` hoặc thử friend request tới user mới cho tới khi projection xuất hiện.

Kiểm tra queue recovery: dừng Users hoặc Posts Service, register qua Gateway, xác nhận queue durable tương ứng có message trong RabbitMQ Management. Khởi động lại service và poll profile/feed. Gửi lại cùng payload với cùng `EventId` để xác nhận projection và `InboxMessages` vẫn chỉ có một record.

## Port

| Component | Port |
| --- | ---: |
| React frontend | 5173 |
| YARP Gateway | 5000 |
| Identity Service | 5001 |
| Users Service | 5002 |
| Friends Service | 5003 |
| Posts Service | 5004 |
| Media Service | 5005 |
| PostgreSQL (`identity_db`, `users_db`, `friends_db`, `posts_db`, `media_db`) | 5432 |
| RabbitMQ AMQP | 5672 |
| RabbitMQ Management | 15672 |
| Redis | 6379 |
| MinIO API | 9000 |
| MinIO Console | 9001 |

Identity, Users, Friends, Posts và Media lần lượt là chủ sở hữu duy nhất của `identity_db`, `users_db`, `friends_db`, `posts_db` và `media_db`. Các service có thể dùng chung PostgreSQL server trong development nhưng không truy cập database của nhau.
