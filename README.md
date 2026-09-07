# Fookbase Light

Foundation cho mạng xã hội theo kiến trúc microservices. Milestone hiện tại gồm React frontend, YARP API Gateway, Identity Service với JWT/refresh-token rotation, Users Service quản lý social profile, Friends Service quản lý quan hệ xã hội và hạ tầng PostgreSQL, RabbitMQ, Redis, MinIO. Mỗi service sở hữu database riêng; dữ liệu cross-service được đồng bộ bất đồng bộ qua Transactional Outbox, RabbitMQ và consumer idempotent.

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

Ở terminal khác, chạy Gateway:

```bash
dotnet run --project services/Gateway/Fookbase.Gateway
```

Gateway chạy tại <http://localhost:5000>; health check: <http://localhost:5000/health>. Các request `/api/auth/**`, `/api/users/**` và `/api/friends/**` lần lượt được chuyển tiếp tới Identity `:5001`, Users `:5002` và Friends `:5003`.

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

Users tự validate JWT bằng issuer, audience và signing key từ environment; service không gọi HTTP sang Identity cho mỗi request. Username vẫn do Identity sở hữu và không thể đổi qua Users API. `AvatarUrl` và `CoverUrl` mới chỉ là fields dành cho Media milestone sau.

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

## Register → profile event flow

Khi `/api/auth/register` thành công, Identity ghi user, refresh token và `UserRegisteredIntegrationEvent` vào `identity_db` trong cùng transaction. API trả `201` ngay sau database commit, không phụ thuộc RabbitMQ đang online.

Outbox worker đọc message chưa xử lý, publish persistent message lên durable RabbitMQ topology rồi mới đánh dấu `ProcessedAtUtc`. Publish failure tăng `RetryCount`, lưu `LastError` và retry có backoff; record không bị xóa để hỗ trợ audit. Delivery là at-least-once.

Users consumer dùng manual ACK. Trong một transaction của `users_db`, consumer tạo `UserProfile` mặc định và ghi `InboxMessages`. `InboxMessages.EventId` và `UserProfiles.UserId` là unique/primary key, vì vậy cùng một event được deliver nhiều lần vẫn chỉ tạo một profile. ACK chỉ xảy ra sau commit.

RabbitMQ topology:

- Exchange: `fookbase.identity.events` (durable topic)
- Routing key/event type: `identity.user.registered.v1`
- Queues: `fookbase.users.user-registered.v1` và `fookbase.friends.user-registered.v1` (durable)
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
```

PostgreSQL init script tự tạo `users_db` và `friends_db` trên volume mới. Với volume development đã tồn tại từ trước các milestone này, tạo database còn thiếu một lần mà không reset volume:

```bash
for database in users_db friends_db; do
  docker compose exec -T postgres sh -lc '
    database="$1"
    psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc \
      "SELECT 1 FROM pg_database WHERE datname = '\''$database'\''" | grep -q 1 ||
    createdb -U "$POSTGRES_USER" -O "$POSTGRES_USER" "$database"
  ' sh "$database"
done
```

Integration tests dùng ba PostgreSQL database development, vì vậy cần chạy `docker compose up -d postgres`, apply cả ba migration và nạp `.env` trước khi `dotnet test`.

Nếu `identity_db` đã có user trước khi Friends queue tồn tại, chạy explicit development backfill sau khi cả RabbitMQ và Friends đang hoạt động:

```bash
./scripts/replay-user-registrations-for-new-consumers.sh --confirm
```

Script chỉ reset trạng thái publish của các `identity.user.registered.v1` Outbox records để Identity phát lại. Inbox của Users/Friends loại duplicate nên thao tác an toàn cho development; đây không phải runtime cross-database dependency của Friends.

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

Kiểm tra Outbox recovery: `docker compose stop rabbitmq`, register hoặc thực hiện friend operation qua Gateway và xác nhận request vẫn commit (`201`/`204`); record tương ứng trong Outbox của service phải còn `ProcessedAtUtc = NULL`. Sau `docker compose start rabbitmq`, worker reconnect, publish event và đánh dấu record processed. Với registration, poll `/api/users/<USER_ID>` hoặc thử friend request tới user mới cho tới khi projection xuất hiện.

Kiểm tra queue recovery: dừng Users Service, register qua Gateway, xác nhận queue durable có message trong RabbitMQ Management. Khởi động lại Users và poll profile. Gửi lại cùng payload với cùng `EventId` để xác nhận `InboxMessages` và `UserProfiles` vẫn chỉ có một record.

## Port

| Component | Port |
| --- | ---: |
| React frontend | 5173 |
| YARP Gateway | 5000 |
| Identity Service | 5001 |
| Users Service | 5002 |
| Friends Service | 5003 |
| PostgreSQL (`identity_db`, `users_db`, `friends_db`) | 5432 |
| RabbitMQ AMQP | 5672 |
| RabbitMQ Management | 15672 |
| Redis | 6379 |
| MinIO API | 9000 |
| MinIO Console | 9001 |

Identity, Users và Friends lần lượt là chủ sở hữu duy nhất của `identity_db`, `users_db` và `friends_db`. Các service có thể dùng chung PostgreSQL server trong development nhưng không truy cập database của nhau.
