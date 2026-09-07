# Fookbase Light

Foundation cho mạng xã hội theo kiến trúc microservices. Milestone hiện tại gồm React frontend, YARP API Gateway, Identity Service với JWT/refresh-token rotation và hạ tầng PostgreSQL, RabbitMQ, Redis, MinIO. Chưa có UI authentication hoặc các service nghiệp vụ khác.

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

Ở terminal khác, chạy Gateway:

```bash
dotnet run --project services/Gateway/Fookbase.Gateway
```

Gateway chạy tại <http://localhost:5000>; health check: <http://localhost:5000/health>. Các request `/api/auth/**` được chuyển tiếp tới Identity Service tại port `5001`.

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
```

Tạo migration mới khi model thay đổi:

```bash
dotnet tool run dotnet-ef migrations add MigrationName \
  --project services/Identity/Fookbase.Identity.Infrastructure \
  --startup-project services/Identity/Fookbase.Identity.Api \
  --output-dir Persistence/Migrations
```

Integration tests dùng PostgreSQL development, vì vậy cần chạy `docker compose up -d postgres` và nạp `.env` trước khi chạy `dotnet test`.

## Port

| Component | Port |
| --- | ---: |
| React frontend | 5173 |
| YARP Gateway | 5000 |
| Identity Service | 5001 |
| PostgreSQL (`identity_db`) | 5432 |
| RabbitMQ AMQP | 5672 |
| RabbitMQ Management | 15672 |
| Redis | 6379 |
| MinIO API | 9000 |
| MinIO Console | 9001 |

Identity Service là service duy nhất sở hữu database `identity_db`. Mỗi microservice được thêm ở các milestone sau sẽ có database riêng.
