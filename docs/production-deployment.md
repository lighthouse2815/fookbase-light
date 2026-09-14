# Vận hành production V1

Fookbase V1 chạy một API ASP.NET Core duy nhất. Kiến trúc được hỗ trợ là:

```text
React clients → reverse proxy / HTTPS → one API → PostgreSQL + private MinIO
                                      ↘ SignalR (single instance)
```

Không chạy nhiều replica API trong V1. SignalR và Messenger presence là process-local; jobs media là durable trong PostgreSQL nhưng không phải lý do để tuyên bố hệ thống đã scale ngang. Redis không cần thiết và không được thêm vào stack này.

## Thành phần và persistence

- `backend/Fookbase.Src/Main/Dockerfile` là multi-stage image, có FFmpeg/ffprobe và chạy user non-root UID 10001.
- Compose giữ named volume cho PostgreSQL (`identity-postgres-data`), MinIO (`minio-data`) và Data Protection (`data-protection-keys`). Ba volume này phải sống qua recreation container.
- API không ghi media vào filesystem container. Bucket `fookbase-media` private; client chỉ nhận presigned URL.
- `/health/live` chỉ xác nhận process còn sống. `/health/ready` kiểm tra PostgreSQL và bucket MinIO, nên chỉ endpoint này dùng để nhận traffic.

## Chuẩn bị cấu hình

Tạo file môi trường ngoài Git từ `.env.example`. Không commit file này, dump, key ring, token hoặc credential.

Production fail fast khi thiếu database URL, JWT signing key, MinIO endpoint/credential/bucket, key-ring Data Protection, origin CORS HTTPS rõ ràng hoặc `AllowedHosts` cụ thể. Thay toàn bộ giá trị mẫu. Thiết lập tối thiểu:

```dotenv
POSTGRES_USER=fookbase_prod
POSTGRES_PASSWORD=<secret>
POSTGRES_DB=fookbase_db
MINIO_ROOT_USER=<secret>
MINIO_ROOT_PASSWORD=<secret>
Jwt__SigningKey=<random-secret-at-least-32-characters>
AllowedHosts=api.example.com
Cors__AllowedOrigins__0=https://app.example.com
Cors__AllowedOrigins__1=https://messenger.example.com
Cors__AllowedOrigins__2=https://admin.example.com
MINIO_CORS_ALLOWED_ORIGIN=https://app.example.com
```

`DataProtection__KeyRingPath` được Compose đặt là `/var/fookbase/data-protection-keys`; backup volume này cùng application data. `Database__CommandTimeoutSeconds` mặc định 30 giây. Npgsql vẫn nhận `Connection Timeout` và `Maximum Pool Size` từ connection string; với một API instance, chỉ tăng pool sau khi tính rõ giới hạn connection PostgreSQL.

## Reverse proxy, HTTPS và CORS

Đặt proxy đáng tin cậy trước API, chuyển WebSocket cho `/hubs/messages` và `/hubs/notifications`, và không public PostgreSQL, MinIO API hay MinIO console. Nếu proxy terminate TLS, bật forwarded headers và chỉ khai báo IP trực tiếp của proxy:

```dotenv
ForwardedHeaders__Enabled=true
ForwardedHeaders__KnownProxies__0=172.20.0.2
ForwardedHeaders__ForwardLimit=1
```

API không tin `X-Forwarded-*` khi setting này tắt hoặc proxy không nằm trong allow-list. HTTPS redirection trong API chỉ bật cùng forwarded headers, tránh redirect loop hoặc endpoint nội bộ không có TLS; proxy phải enforce HTTP→HTTPS khi forwarded headers không được bật. Production dùng explicit origins với credentials, không dùng `AllowAnyOrigin`. Proxy/front-end host có thể bổ sung CSP phù hợp với asset của React; API chỉ đặt header an toàn không phá SignalR: `nosniff`, `no-referrer`, frame deny, permissions policy và HSTS ở Production.

## Deploy

`compose.yml` là stack local. Production dùng override, không public port service nội bộ:

```bash
set -a; . ./.env.production; set +a
docker compose -f compose.yml -f compose.prod.yml config --quiet
bash scripts/check-migrations.sh
BACKUP_DIR=/srv/fookbase-backups bash scripts/backup-postgres.sh
docker compose -f compose.yml -f compose.prod.yml build api
```

Chạy migration một lần trước khi đổi traffic. `Database__ApplyMigrationsOnStartup=false` trong production để tránh race khi sau này có replica:

Image runtime không có EF tool; dùng SDK container hoặc deployment job có `dotnet-ef` cho migration, ví dụ:

```bash
docker run --rm --network <compose-network> \
  -e ConnectionStrings__FookbaseDatabase="$ConnectionStrings__FookbaseDatabase" \
  -v "$PWD:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -lc 'dotnet tool restore && dotnet tool run dotnet-ef database update --project backend/Fookbase.Src/Main --startup-project backend/Fookbase.Src/Main'
```

Sau migration, start/recreate API rồi kiểm tra:

```bash
docker compose -f compose.yml -f compose.prod.yml up -d
curl --fail --show-error https://api.example.com/health/live
curl --fail --show-error https://api.example.com/health/ready
docker compose -f compose.yml -f compose.prod.yml logs --tail=200 api
```

Nếu deploy lỗi, giữ volume, rollback image/application tương thích schema hoặc khôi phục backup đã được diễn tập. Không force-push, không xóa volume như một bước rollback.

## Jobs, shutdown và capacity

Video jobs có claim PostgreSQL điều kiện, lease timeout, retry giới hạn và output key deterministic. `Media__MaxConcurrentJobs=1` là default production an toàn; chỉ tăng cùng giới hạn CPU/RAM thực tế và `Media__VideoProcessingBatchSize`. Object deletion chạy durable, retry có delay và chuyển sang `FailedAtUtc` sau giới hạn để dễ chẩn đoán, không busy-loop.

Compose production đặt grace period API 45 giây để `BackgroundService` nhận SIGTERM/cancellation; job đang `Processing` sẽ được claim lại sau timeout nếu container dừng giữa chừng. API 1 CPU/1 GiB, PostgreSQL 2 CPU/2 GiB và MinIO 1 CPU/1 GiB là guardrail khởi đầu, không phải sizing guarantee. Theo dõi FFmpeg trước khi tăng concurrency.

Application logs JSON ra stdout ở Production, không ghi file log trong container. Runtime Docker phải cấu hình log rotation (ví dụ `json-file` với `max-size`/`max-file`) hoặc thu stdout. Log request gồm method/path/status/duration/request ID và UserId khi đã xác thực; không log body, token, password, key, recovery code hay connection string. Client có thể cung cấp `X-Request-Id` từ error ProblemDetails để operator tìm log.

## Test và migration policy

Không chạy `dotnet test FookbaseLight.sln` làm integration regression vì projects từng dùng chung database có thể race migration. Lệnh chuẩn là:

```bash
bash scripts/test-backend.sh
```

Script tạo một PostgreSQL container tạm, database riêng cho từng project và chạy tuần tự. `scripts/check-migrations.sh` kiểm tra ID migration trùng/future-dated; thêm `CHECK_EF_MODEL=true` với connection string design-time để kiểm tra pending model changes. Luôn backup, migration một lần, start/restart một API, chờ readiness rồi smoke test.

## Đường scale được hoãn

Khi có bằng chứng cần nhiều API replica: thêm Redis SignalR backplane, chuyển presence sang shared ephemeral store, xác minh worker lease PostgreSQL đa-replica, sizing pool connection, thêm load balancer và load test. Sau đó mới cân nhắc worker process riêng, cache/CDN hoặc search infrastructure.
