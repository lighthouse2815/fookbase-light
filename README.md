# Fookbase Light

Fookbase Light là một modular monolith cho mạng xã hội. Toàn bộ Identity, Users, Friends, Messages, Posts và Media chạy trong một ASP.NET Core process tại cổng `5000`; không còn API Gateway, service-to-service HTTP hay RabbitMQ.

Code nghiệp vụ được chia theo feature module trong một project backend duy nhất. Mỗi luồng giữ đơn giản theo `Endpoint -> module coordinator (khi cần phối hợp) -> Service -> DbContext`. Không dùng message broker, event bus, outbox hoặc inbox.

Chi tiết về ranh giới module và kế hoạch hợp nhất database được ghi tại [docs/modular-monolith.md](docs/modular-monolith.md).

## Kiến trúc

```text
React frontend
      |
      v
Fookbase.Api :5000
  |-- Identity module
  |-- Users module
  |-- Friends module
  |-- Messages module (SignalR)
  |-- Posts module
  |-- Admin module
  `-- Media module
      |
      |-- PostgreSQL
      `-- MinIO
```

Backend chỉ có một entry point: `backend/Fookbase.Src/Main`. Các route cũ dưới `/api/*` được giữ nguyên nên frontend/client không cần đổi base URL.

Toàn bộ persistence runtime dùng duy nhất `FookbaseDbContext` và PostgreSQL database `fookbase_db`. Module vẫn giữ entity, configuration và service trong folder riêng; chỉ DbContext và migration history được hợp nhất.

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

Khởi động đầy đủ local stack (PostgreSQL, MinIO và API):

```bash
docker compose up --build -d
docker compose ps
```

Compose chỉ lấy secrets từ `.env`; Docker image không chứa `.env` hoặc credential. Local
compose tự apply migration hợp nhất khi API start. MinIO Console chạy tại
<http://localhost:9001>, API object storage chạy tại <http://localhost:9000>, và API chạy tại
<http://localhost:5000>.

Kiểm tra health:

```bash
curl -fsS http://localhost:5000/health/live
curl -fsS http://localhost:5000/health/ready
```

`/health` vẫn là liveness-compatible endpoint. `/health/live` chỉ xác nhận process sống;
`/health/ready` yêu cầu PostgreSQL và private MinIO bucket sẵn sàng.

Để chạy API trực tiếp thay vì container, apply migration thủ công rồi chạy backend:

```bash
set -a
source .env
set +a
dotnet run --project backend/Fookbase.Src/Main
```

API chạy tại <http://localhost:5000>.

Để mở quyền quản trị cho một tài khoản development, đặt `Admin__BootstrapEmail` thành email
của tài khoản đó trước khi đăng ký hoặc đăng nhập. Hệ thống sẽ tự gán role `Admin` vào lần
phát hành token kế tiếp; không đặt biến này ở môi trường production nếu chưa có quy trình
quản lý role riêng.

Chạy frontend:

```bash
cd frontend/web
npm install
npm run dev
```

Frontend chạy tại <http://localhost:5173>.

Chạy web Admin riêng:

```bash
cd frontend/admin
npm install
npm run dev
```

Admin Center chạy tại <http://localhost:5174>. Khi dùng local, thêm origin này vào
`Cors__AllowedOrigins__1` (đã có sẵn trong `.env.example`).

## Build và test

```bash
dotnet restore FookbaseLight.sln
dotnet build FookbaseLight.sln --no-restore

set -a
source .env
set +a
dotnet test FookbaseLight.sln --no-build

./scripts/test-legacy-import-e2e.sh
```

`test-legacy-import-e2e.sh` tạo một PostgreSQL container tạm, apply active
`FookbaseDbContext` migration, tạo sáu source database legacy đại diện và chạy chính
`scripts/import-legacy-databases.sh`. Test xác nhận preservation của ID, timestamp,
password hash, friendship/block, posts/media/profile reference, conversation/message/read
cursor qua `FookbaseDbContext`, đồng thời kiểm tra script từ chối target không rỗng. Container
và database tạm được xóa sau test; database development không bị dùng.

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

### Messages

| Method | Endpoint | Authentication |
| --- | --- | --- |
| POST | `/api/messages/conversations/{userId}` | Bearer JWT, bạn bè |
| GET | `/api/messages/conversations` | Bearer JWT |
| GET | `/api/messages/conversations/{conversationId}/messages` | Bearer JWT, thành viên |
| POST | `/api/messages/conversations/{conversationId}/read` | Bearer JWT, thành viên |
| POST | `/api/messages/conversations/{conversationId}/messages` | Bearer JWT, thành viên |
| GET | `/api/messages/notifications` | Bearer JWT |

Tin nhắn chỉ được gửi giữa bạn bè không bị block. History dùng keyset pagination: request đầu không có `before` trả trang mới nhất; dùng `nextCursor` làm giá trị `before` để tải các tin cũ hơn. GET history không thay đổi trạng thái đã đọc; client xác nhận mốc đọc bằng `POST .../read` với `lastReadMessageId`. Read cursor được lưu theo thành viên conversation để sẵn sàng mở rộng conversation nhiều thành viên trong tương lai. Notification chưa đọc được lưu trong bảng Messages của `fookbase_db` và cập nhật realtime qua SignalR tại `/hubs/messages`.

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

### Admin

| Method | Endpoint | Chức năng |
| --- | --- | --- |
| GET | `/api/admin/dashboard` | Thống kê users, posts và reports chờ xử lý |
| GET | `/api/admin/users` | Danh sách tài khoản, hỗ trợ `query`, `offset`, `limit` |
| PATCH | `/api/admin/users/{userId}/status` | Bật/tắt tài khoản thường |
| GET | `/api/admin/reports` | Danh sách reports, lọc theo `status` |
| PATCH | `/api/admin/reports/{reportId}/status` | Đánh dấu `reviewed`, `resolved` hoặc `dismissed` |
| DELETE | `/api/admin/posts/{postId}` | Gỡ bài viết vi phạm |

Tất cả endpoint Admin yêu cầu JWT có role `Admin`.

## Phối hợp module

Khi một API cần nhiều service, endpoint gọi coordinator trong module sở hữu endpoint:
`Modules/Identity/Services/RegistrationUseCase`, `Modules/Posts/Services/PostsUseCase`, hoặc
`Modules/Admin/Services/AdministrationUseCase`. Đăng ký tạo Identity và profile trong một
transaction; Posts lấy quan hệ hiện tại từ Friends và đồng bộ attachment với Media. Không có
endpoint nào truy cập `DbContext` trực tiếp hoặc điều phối nhiều service.

## Tạo migration mới

```bash
dotnet tool run dotnet-ef migrations add MigrationName \
  --project backend/Fookbase.Src/Main \
  --startup-project backend/Fookbase.Src/Main \
  --context FookbaseDbContext \
  --output-dir Code/Persistence/Migrations
```

Runtime migration và snapshot nằm ở `backend/Fookbase.Src/Main/Code/Persistence/Migrations`.
Historical module migrations vẫn được giữ tại `Code/Modules/<Module>/Data/Migrations` làm
record cho import dữ liệu cũ, nhưng không còn được compile hoặc apply ở runtime.
Xem [docs/migration-history.md](docs/migration-history.md) để biết active source và legacy
history cụ thể.

## Chuyển dữ liệu development cũ

Fresh install tạo `fookbase_db` từ compose init script và migration hợp nhất ở trên. Không xóa
sáu database cũ khi nâng cấp một development environment đã có dữ liệu.

1. Trước khi thay `.env`, backup sáu source database:

   ```bash
   BACKUP_DIR=/safe/path BACKUP_DATABASES="identity_db users_db friends_db messages_db posts_db media_db" \
     ./infrastructure/postgres/backup.sh
   ```

2. Tạo `fookbase_db` nếu volume PostgreSQL đã tồn tại từ trước, rồi apply `FookbaseDbContext`
   migration. Compose init script sẽ làm bước tạo database tự động chỉ với volume mới:

   ```bash
   docker compose exec -T postgres sh -c \
     'psql -U "$POSTGRES_USER" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '\''fookbase_db'\''" | grep -q 1 || psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d postgres -c "CREATE DATABASE fookbase_db"'
   ```

3. Giữ tạm sáu `ConnectionStrings__*Database` cũ trong môi trường chỉ cho lần import và đặt
   `ConnectionStrings__FookbaseDatabase` tới database target. Chạy:

   ```bash
   ./scripts/import-legacy-databases.sh
   ```

   Script kiểm tra target rỗng, không drop/reset/ghi lên source database, bỏ qua sáu
   `__EFMigrationsHistory` cũ, và import toàn bộ dữ liệu trong một transaction target. IDs,
   timestamp, password hash, refresh token, post/comment/reaction, friendship/block/request,
   conversation/message/read cursor, MediaAsset và reference đều được copy nguyên trạng.

4. So sánh row count, chạy test/API smoke check, sau đó chỉ giữ
   `ConnectionStrings__FookbaseDatabase` trong cấu hình runtime. Giữ source backup cho tới khi
   rollback không còn cần thiết.
