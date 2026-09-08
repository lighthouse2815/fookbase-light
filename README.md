# Fookbase Light

Fookbase Light là một modular monolith cho mạng xã hội. Toàn bộ Identity, Users, Friends, Posts và Media chạy trong một ASP.NET Core process tại cổng `5000`; không còn API Gateway, service-to-service HTTP hay RabbitMQ.

Code nghiệp vụ vẫn được chia theo module và các layer Domain/Application/Infrastructure để giữ ranh giới rõ ràng. Các module trao đổi event trực tiếp trong process. Transactional outbox và inbox vẫn được giữ để event bền vững, retry được và idempotent, nhưng không cần message broker.

Chi tiết về ranh giới module, quyết định giữ projection/outbox-inbox và kế hoạch hợp nhất database được ghi tại [docs/modular-monolith.md](docs/modular-monolith.md).

## Kiến trúc

```text
React frontend
      |
      v
Fookbase.Api :5000
  |-- Identity module
  |-- Users module
  |-- Friends module
  |-- Posts module ---- gọi trực tiếp ----> Media module
  `-- Media module
      |
      |-- PostgreSQL
      `-- MinIO
```

Backend chỉ có một entry point: `backend/src/Fookbase.Api`. Các route cũ dưới `/api/*` được giữ nguyên nên frontend/client không cần đổi base URL.

Năm database module hiện tại được giữ để migration và dữ liệu development cũ tiếp tục tương thích. Đây chỉ là ranh giới lưu trữ nội bộ của cùng một ứng dụng, không phải các service triển khai độc lập.

## Yêu cầu

- .NET SDK 10
- Node.js và npm
- Docker Engine với Docker Compose plugin

Tạo cấu hình development:

```bash
cp .env.example .env
```

Các credential mẫu chỉ dành cho local development. Hãy thay password và JWT signing key trước khi dùng ở môi trường khác.

## Khởi động

Khởi động PostgreSQL và MinIO:

```bash
docker compose up -d
docker compose ps
```

MinIO Console chạy tại <http://localhost:9001>. API object storage chạy tại <http://localhost:9000>.

Apply migration cho các module:

```bash
set -a
source .env
set +a
dotnet tool restore

for module in Identity Users Friends Posts Media; do
  dotnet tool run dotnet-ef database update \
    --project "backend/src/$module/Fookbase.$module.Infrastructure" \
    --startup-project backend/src/Fookbase.Api
done
```

Chạy backend monolith:

```bash
set -a
source .env
set +a
dotnet run --project backend/src/Fookbase.Api
```

API chạy tại <http://localhost:5000>, health check tại <http://localhost:5000/health>.

Chạy frontend:

```bash
cd frontend/web
npm install
npm run dev
```

Frontend chạy tại <http://localhost:5173>.

## Build và test

```bash
dotnet restore FookbaseLight.sln
dotnet build FookbaseLight.sln --no-restore

set -a
source .env
set +a
dotnet test FookbaseLight.sln --no-build
```

Nếu máy chưa có .NET SDK 10, có thể build bằng container:

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

## API

### Authentication

| Method | Endpoint | Authentication |
| --- | --- | --- |
| POST | `/api/auth/register` | Không |
| POST | `/api/auth/login` | Không |
| POST | `/api/auth/refresh` | Không |
| POST | `/api/auth/logout` | Bearer JWT |
| GET | `/api/auth/me` | Bearer JWT |

JWT signing key chỉ được đọc từ `Jwt__SigningKey`. Refresh token raw chỉ trả cho client; database lưu SHA-256 hash.

### Users

| Method | Endpoint | Authentication |
| --- | --- | --- |
| GET | `/api/users/{userId}` | Không |
| GET | `/api/users/me` | Bearer JWT |
| PATCH | `/api/users/me` | Bearer JWT |

### Friends

| Method | Endpoint |
| --- | --- |
| POST | `/api/friends/requests/{userId}` |
| DELETE | `/api/friends/requests/{requestId}` |
| POST | `/api/friends/requests/{requestId}/accept` |
| POST | `/api/friends/requests/{requestId}/decline` |
| GET | `/api/friends/requests/incoming` |
| GET | `/api/friends/requests/outgoing` |
| DELETE | `/api/friends/{userId}` |
| GET | `/api/friends` |
| GET | `/api/friends/status/{userId}` |
| GET | `/api/friends/mutual/{userId}` |
| POST | `/api/friends/blocks/{userId}` |
| DELETE | `/api/friends/blocks/{userId}` |
| GET | `/api/friends/blocks` |

Tất cả Friends endpoint yêu cầu Bearer JWT. Collection endpoint dùng offset pagination, `limit` mặc định 20 và tối đa 100.

### Posts

| Method | Endpoint | Authentication |
| --- | --- | --- |
| POST | `/api/posts` | Bearer JWT |
| PUT/DELETE | `/api/posts/{postId}` | Bearer JWT, tác giả |
| GET | `/api/posts/{postId}` | Tùy chọn |
| GET | `/api/posts/feed` | Bearer JWT |
| GET | `/api/posts/users/{userId}` | Tùy chọn |
| POST | `/api/posts/{postId}/comments` | Bearer JWT |
| GET | `/api/posts/{postId}/comments` | Tùy chọn |
| PUT/DELETE | `/api/posts/comments/{commentId}` | Bearer JWT, tác giả |
| PUT/DELETE | `/api/posts/{postId}/reaction` | Bearer JWT |
| GET | `/api/posts/{postId}/media/{mediaId}/access` | Bearer JWT |

Privacy hợp lệ gồm `public`, `friends`, `onlyMe`; reaction gồm `like`, `love`, `haha`, `wow`, `sad`, `angry`.

### Media

| Method | Endpoint | Authentication |
| --- | --- | --- |
| POST | `/api/media/uploads` | Bearer JWT |
| POST | `/api/media/{mediaId}/complete` | Bearer JWT, chủ sở hữu |
| GET | `/api/media/{mediaId}` | Bearer JWT, chủ sở hữu |
| DELETE | `/api/media/{mediaId}` | Bearer JWT, chủ sở hữu |

Upload dùng presigned PUT trực tiếp tới bucket private. Posts lấy read URL bằng lời gọi C# trực tiếp tới Media module; endpoint HTTP nội bộ và shared service token cũ đã được loại bỏ.

## Event nội bộ

Các mutation vẫn ghi event và business state trong cùng transaction. Outbox worker của cùng process chuyển event tới projection handler của module đích rồi mới đánh dấu `ProcessedAtUtc`. Nếu handler lỗi, `RetryCount` và `LastError` được cập nhật để worker retry. Inbox giữ tính idempotent khi event được xử lý lại.

Các luồng chính:

- Identity registration → Users, Friends, Posts và Media.
- Friends accepted/removed/blocked/unblocked → Posts.
- Media ready/deleted → Posts.
- Posts media attached/detached → Media.

## Tạo migration mới

```bash
dotnet tool run dotnet-ef migrations add MigrationName \
  --project backend/src/Posts/Fookbase.Posts.Infrastructure \
  --startup-project backend/src/Fookbase.Api \
  --output-dir Persistence/Migrations
```

Thay `Posts` bằng module cần cập nhật. PostgreSQL init script tự tạo các database module còn thiếu khi volume được tạo lần đầu.
