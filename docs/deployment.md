# Triển khai production

## Biến môi trường bắt buộc

Tạo file môi trường ngoài Git từ `.env.example`. Thay toàn bộ mật khẩu mẫu và tạo JWT signing key ngẫu nhiên tối thiểu 32 ký tự.

Khi frontend và API dùng domain khác nhau, đặt đúng origin HTTPS của frontend:

```dotenv
Cors__AllowedOrigins__0=https://app.example.com
MINIO_CORS_ALLOWED_ORIGIN=https://app.example.com
Minio__Endpoint=storage.example.com
Minio__Secure=true
Minio__BucketInitializationEnabled=false
```

`VITE_API_BASE_URL` phải là origin HTTPS của API nếu frontend không reverse-proxy `/api` và `/hubs` về cùng domain.

Khi API và MinIO cùng khởi động, bootstrap bucket sẽ retry 5 lần với khoảng cách 2 giây. Điều chỉnh `Minio__BucketInitializationMaxAttempts` và `Minio__BucketInitializationRetrySeconds` nếu storage cần thời gian sẵn sàng lâu hơn.

## Docker image và local stack

Backend image được build từ
`backend/Fookbase.Src/Main/Dockerfile`. Đây là multi-stage .NET 10 build/publish Release,
chỉ copy publish output sang ASP.NET runtime image, chạy bằng user không phải root, expose cổng
`5000`, và không copy `.env` vào build context.

Để chạy đầy đủ local stack:

```bash
cp .env.example .env
# thay các giá trị mẫu trước khi dùng ngoài máy local
docker compose config --quiet
docker compose up --build -d
curl -fsS http://localhost:5000/health/live
curl -fsS http://localhost:5000/health/ready
```

Compose local đặt `Database__ApplyMigrationsOnStartup=true` để một fresh
`fookbase_db` có schema ngay khi start. Với production, giữ giá trị này `false` và chạy
`dotnet-ef database update` như một deployment step có kiểm soát trước khi đổi traffic. Liveness
không phụ thuộc dependency; readiness chỉ trả thành công sau khi PostgreSQL và bucket MinIO
private đã sẵn sàng.

## Data Protection key ring

Cursor của bảng tin được bảo vệ bằng ASP.NET Core Data Protection. Khi chạy Production,
`DataProtection__KeyRingPath` là bắt buộc; API sẽ dừng khi thiếu cấu hình này để tránh dùng key
ephemeral. Không đặt private key trong configuration: framework tạo và luân phiên key ring tại
đường dẫn đã cấu hình. Bảo vệ volume/path này như credential, backup cùng dữ liệu ứng dụng và chỉ
cấp quyền ghi cho API.

Compose mount named volume `data-protection-keys` tại `/var/fookbase/data-protection-keys`. Service
`data-protection-init` chỉ thiết lập quyền sở hữu cho UID API không phải root; API không chạy bằng
root. Có thể smoke-test cursor qua một lần tái tạo container mà không xoá volume:

```bash
# Đăng nhập và thay ACCESS_TOKEN bằng JWT hợp lệ trước khi chạy.
curl -fsS -H "Authorization: Bearer $ACCESS_TOKEN" \
  'http://localhost:5000/api/feed?limit=1' > /tmp/feed-page.json
CURSOR=$(jq -r '.nextCursor' /tmp/feed-page.json)
docker compose up -d --force-recreate --no-deps api
curl -fsS -H "Authorization: Bearer $ACCESS_TOKEN" --get \
  --data-urlencode "cursor=$CURSOR" --data 'limit=1' \
  'http://localhost:5000/api/feed' >/dev/null
```

Lệnh cuối phải trả HTTP 200. Không chạy `docker compose down -v` giữa hai request vì lệnh đó chủ
động xoá key ring và làm mọi cursor cũ không thể giải mã.

## Reverse proxy và TLS

Đặt API, MinIO API và frontend phía sau reverse proxy có chứng chỉ TLS. Proxy cần chuyển tiếp WebSocket cho `/hubs/messages` và `/hubs/notifications`; không mở trực tiếp PostgreSQL, MinIO console hoặc MinIO API ra Internet. Chỉ proxy mới được kết nối tới các service nội bộ.

Đặt `AllowedHosts` thành host API thực tế thay vì `*`. Không bật HTTPS redirection trong container API khi proxy chưa gửi/cấu hình forwarded headers chính xác, vì điều đó gây redirect loop.

## Rate limit

API giới hạn mặc định 120 request mỗi 60 giây cho mỗi JWT subject hoặc địa chỉ IP chưa đăng nhập. Các route nhạy cảm có limiter riêng nghiêm ngặt hơn: login dùng `RateLimiting__SensitiveAuth__LoginPermitLimit`; quên/đặt lại mật khẩu dùng `RateLimiting__SensitiveAuth__RecoveryPermitLimit`; gửi lại email xác thực dùng `RateLimiting__SensitiveAuth__ResendVerificationPermitLimit`. Cửa sổ chung của các limiter này là `RateLimiting__SensitiveAuth__WindowSeconds` (mặc định 300 giây). Các request bị từ chối nhận HTTP 429.

## Backup và khôi phục

Chạy backup từ máy có Docker Compose và file môi trường production:

```bash
set -a
. ./.env
set +a
BACKUP_DIR=/srv/fookbase-backups sh infrastructure/postgres/backup.sh
```

Lưu backup ở vị trí tách biệt khỏi host chạy ứng dụng và kiểm tra khôi phục định kỳ. Khôi phục một database vào database trống bằng `pg_restore --clean --if-exists --dbname=<connection-string> <file.dump>`; chỉ chạy lệnh này sau khi xác nhận đúng database đích vì nó ghi đè schema/dữ liệu hiện có.

Database backup không chứa ảnh/video. Bật versioning hoặc replication/backup định kỳ cho bucket private của MinIO/S3 theo chính nhà cung cấp storage, và kiểm tra khôi phục cả database lẫn object storage cùng nhau.

## Trước khi mở cho người dùng

1. Chạy migration trên bản sao staging và kiểm tra `/health`.
2. Kiểm tra đăng nhập, upload ảnh/video, tin nhắn SignalR và CORS từ domain thật.
3. Tạo backup ban đầu và thử khôi phục nó vào môi trường tách biệt.
4. Giới hạn quyền bucket ở private; frontend chỉ dùng presigned URL.
