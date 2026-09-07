# Fookbase Light

Foundation cho mạng xã hội theo kiến trúc microservices. Milestone hiện tại gồm React frontend, YARP API Gateway, Identity Service skeleton và hạ tầng PostgreSQL, RabbitMQ, Redis, MinIO. Chưa có Register/Login hoặc các service nghiệp vụ khác.

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
