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

## Reverse proxy và TLS

Đặt API, MinIO API và frontend phía sau reverse proxy có chứng chỉ TLS. Proxy cần chuyển tiếp WebSocket cho `/hubs/messages`; không mở trực tiếp PostgreSQL, MinIO console hoặc MinIO API ra Internet. Chỉ proxy mới được kết nối tới các service nội bộ.

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
