# Fookbase Light

Foundation cho mạng xã hội theo kiến trúc microservices. Milestone hiện tại gồm React frontend, YARP API Gateway, Identity Service với JWT/refresh-token rotation, Users Service quản lý social profile và hạ tầng PostgreSQL, RabbitMQ, Redis, MinIO. Identity và Users sở hữu database riêng; profile được tạo bất đồng bộ qua Transactional Outbox và RabbitMQ.

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

Ở terminal khác, chạy Gateway:

```bash
dotnet run --project services/Gateway/Fookbase.Gateway
```

Gateway chạy tại <http://localhost:5000>; health check: <http://localhost:5000/health>. Các request `/api/auth/**` được chuyển tiếp tới Identity tại port `5001`, còn `/api/users/**` được chuyển tiếp tới Users tại port `5002`.

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

## Register → profile event flow

Khi `/api/auth/register` thành công, Identity ghi user, refresh token và `UserRegisteredIntegrationEvent` vào `identity_db` trong cùng transaction. API trả `201` ngay sau database commit, không phụ thuộc RabbitMQ đang online.

Outbox worker đọc message chưa xử lý, publish persistent message lên durable RabbitMQ topology rồi mới đánh dấu `ProcessedAtUtc`. Publish failure tăng `RetryCount`, lưu `LastError` và retry có backoff; record không bị xóa để hỗ trợ audit. Delivery là at-least-once.

Users consumer dùng manual ACK. Trong một transaction của `users_db`, consumer tạo `UserProfile` mặc định và ghi `InboxMessages`. `InboxMessages.EventId` và `UserProfiles.UserId` là unique/primary key, vì vậy cùng một event được deliver nhiều lần vẫn chỉ tạo một profile. ACK chỉ xảy ra sau commit.

RabbitMQ topology:

- Exchange: `fookbase.identity.events` (durable topic)
- Routing key/event type: `identity.user.registered.v1`
- Queue: `fookbase.users.user-registered.v1` (durable)
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
```

PostgreSQL init script tự tạo `users_db` trên volume mới. Với volume development đã tồn tại từ trước milestone Users, tạo database một lần mà không reset volume:

```bash
docker compose exec postgres sh -lc '
  psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc \
    "SELECT 1 FROM pg_database WHERE datname = '\''users_db'\''" | grep -q 1 ||
  createdb -U "$POSTGRES_USER" -O "$POSTGRES_USER" users_db'
```

Integration tests dùng hai PostgreSQL database development, vì vậy cần chạy `docker compose up -d postgres`, apply cả hai migration và nạp `.env` trước khi `dotnet test`.

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

Kiểm tra Outbox recovery: `docker compose stop rabbitmq`, register qua Gateway và xác nhận request vẫn trả `201`; record tương ứng trong `OutboxMessages` phải còn `ProcessedAtUtc = NULL`. Sau `docker compose start rabbitmq`, poll `/api/users/<USER_ID>` cho tới khi trả `200`; Outbox được đánh dấu processed.

Kiểm tra queue recovery: dừng Users Service, register qua Gateway, xác nhận queue durable có message trong RabbitMQ Management. Khởi động lại Users và poll profile. Gửi lại cùng payload với cùng `EventId` để xác nhận `InboxMessages` và `UserProfiles` vẫn chỉ có một record.

## Port

| Component | Port |
| --- | ---: |
| React frontend | 5173 |
| YARP Gateway | 5000 |
| Identity Service | 5001 |
| Users Service | 5002 |
| PostgreSQL (`identity_db`, `users_db`) | 5432 |
| RabbitMQ AMQP | 5672 |
| RabbitMQ Management | 15672 |
| Redis | 6379 |
| MinIO API | 9000 |
| MinIO Console | 9001 |

Identity là service duy nhất sở hữu `identity_db`; Users là service duy nhất sở hữu `users_db`. Hai service có thể dùng chung PostgreSQL server trong development nhưng không truy cập database của nhau.
