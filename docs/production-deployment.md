# Vận hành production V1

Fookbase V1 chạy một API ASP.NET Core duy nhất. Kiến trúc được hỗ trợ là:

```text
React clients → reverse proxy / HTTPS → one API → PostgreSQL + Cloudinary
                                      ↘ SignalR (single instance)
```

Không chạy nhiều replica API trong V1. SignalR và Zola Light presence là process-local; jobs media là durable trong PostgreSQL nhưng không phải lý do để tuyên bố hệ thống đã scale ngang. Redis không cần thiết và không được thêm vào stack này.

## Thành phần và persistence

- `backend/Fookbase.Src/Dockerfile` là multi-stage image, có FFmpeg/ffprobe và chạy user non-root UID 10001.
- Compose giữ named volume cho PostgreSQL (`identity-postgres-data`) và Data Protection (`data-protection-keys`); hai volume này phải sống qua recreation container.
- API không ghi media vào filesystem container. Browser upload trực tiếp bằng Cloudinary signed form; asset dùng authenticated delivery.
- `/health/live` chỉ xác nhận process còn sống. `/health/ready` kiểm tra PostgreSQL và Cloudinary, nên chỉ endpoint này dùng để nhận traffic.
- Swagger UI và OpenAPI chỉ bật ở môi trường Development/Testing; production không công khai `/swagger`. Đây chỉ là tài liệu/test API, không thay thế health check.

## Chuẩn bị cấu hình

Tạo file môi trường ngoài Git từ `.env.example`. Không commit file này, dump, key ring, token hoặc credential.

Production fail fast khi thiếu database URL, JWT signing key, Cloudinary credential, key-ring Data Protection, origin CORS HTTPS rõ ràng hoặc `AllowedHosts` cụ thể. Thay toàn bộ giá trị mẫu. Thiết lập tối thiểu:

```dotenv
POSTGRES_USER=fookbase_prod
POSTGRES_PASSWORD=<secret>
POSTGRES_DB=fookbase_db
Cloudinary__CloudName=<cloud-name>
Cloudinary__ApiKey=<api-key>
Cloudinary__ApiSecret=<api-secret>
Jwt__SigningKey=<random-secret-at-least-32-characters>
AllowedHosts=api.example.com
Cors__AllowedOrigins__0=https://app.example.com
Cors__AllowedOrigins__1=https://admin.example.com
Cors__AllowedOrigins__2=https://zola-light.example.com
```

`DataProtection__KeyRingPath` được Compose đặt là `/var/fookbase/data-protection-keys`; backup volume này cùng application data. `Database__CommandTimeoutSeconds` mặc định 30 giây. Npgsql vẫn nhận `Connection Timeout` và `Maximum Pool Size` từ connection string; với một API instance, chỉ tăng pool sau khi tính rõ giới hạn connection PostgreSQL.

### Kiểm tra Data Protection key ring

Cursor của feed, bài viết đã lưu và hashtag dùng chung key ring. Sau khi triển khai, có thể kiểm tra cursor vẫn đọc được qua lần tái tạo API mà không xóa volume:

```bash
# Đăng nhập và đặt ACCESS_TOKEN thành JWT hợp lệ trước khi chạy.
curl -fsS -H "Authorization: Bearer $ACCESS_TOKEN" \
  'https://api.example.com/api/feed?limit=1' > /tmp/feed-page.json
CURSOR=$(jq -r '.nextCursor' /tmp/feed-page.json)
docker compose -f compose.yml -f compose.prod.yml up -d --force-recreate --no-deps api
curl -fsS -H "Authorization: Bearer $ACCESS_TOKEN" --get \
  --data-urlencode "cursor=$CURSOR" --data 'limit=1' \
  'https://api.example.com/api/feed' >/dev/null
```

Lệnh cuối phải trả HTTP 200. Chỉ dùng cursor khác `null` để kiểm tra; feed cần có đủ bài viết để tạo trang tiếp theo. Không chạy `docker compose down -v` giữa hai request vì lệnh đó xóa key ring.

## Google OAuth

Google login chỉ bật khi có toàn bộ cấu hình server-side sau; không đưa client secret vào source code, static build hoặc biến `VITE_*`:

```dotenv
GoogleAuthentication__Enabled=true
GoogleAuthentication__ClientId=<google-oauth-client-id>
GoogleAuthentication__ClientSecret=<google-oauth-client-secret>
GoogleAuthentication__WebBaseUrl=https://app.example.com
GoogleAuthentication__ZolaLightBaseUrl=https://zola-light.example.com
```

Trong Google Cloud Console, cấu hình OAuth consent screen, xác minh domain HTTPS bạn sở hữu và khai báo chính xác Authorized redirect URI: `https://api.example.com/signin-google`. Dùng homepage, privacy policy và terms URL công khai thuộc domain đã xác minh khi Google yêu cầu. Không khai báo callback SPA trực tiếp: API sẽ kiểm tra Google claims (`sub`, `email`, `email_verified`) rồi redirect về một trong hai đích cố định `https://app.example.com/login` hoặc `https://zola-light.example.com/login` với completion code dùng một lần.

Thêm cả hai SPA vào `Cors__AllowedOrigins`; restart API sau khi inject secret. Với Zalo, Messenger và các embedded WebView tương tự, giao diện chỉ hướng người dùng mở Chrome/Safari cho Google OAuth. Đăng nhập mật khẩu vẫn hoạt động trong các WebView này.

## OTP qua SMS

Đăng ký mới chấp nhận một email hoặc số di động Việt Nam. Email dùng SMTP; số điện thoại dùng provider HTTP được cấu hình. Demo dùng Traccar SMS Gateway cloud service: cài app trên Android, bật Cloud Service và lưu Cloud Token vào secret store hoặc file môi trường ngoài Git. Backend deployed gọi cloud service; điện thoại không cần mở port trực tiếp ra Internet.

```dotenv
Sms__Enabled=true
Sms__Provider=Traccar
Sms__AccessToken=<traccar-cloud-token>
Sms__BaseUrl=https://www.traccar.org/sms/
```

Không đưa `Sms__AccessToken` vào source, image, log hay biến `VITE_*`. Khi `Sms__Enabled=false` (mặc định), API không gửi SMS và trả `sms_unavailable` cho yêu cầu dùng số điện thoại; đăng ký qua email vẫn dùng được nếu SMTP bật. Sau khi bật, gửi thử tới một số điện thoại thật thuộc nhóm vận hành. App điện thoại phải có mạng, SIM gửi được SMS và được tắt battery optimization.

Khi cần SpeedSMS ở production, đổi `Sms__Provider=SpeedSms`, `Sms__BaseUrl=https://api.speedsms.vn` và bổ sung `Sms__TwoFactorApplicationId`; luồng OTP/API không thay đổi. Không dùng Traccar Local Service qua Internet công khai; nếu cần endpoint local, chỉ kết nối qua VPN và HTTPS proxy đáng tin cậy.

OTP gồm sáu số, hết hạn sau 10 phút, chỉ dùng một lần, có cooldown gửi lại 60 giây và tối đa năm lần gửi hoặc thử mã trong một giờ/challenge. Theo dõi phản hồi nhà cung cấp nhưng không log số điện thoại đầy đủ, OTP, mật khẩu hoặc token SpeedSMS.

## Reverse proxy, HTTPS và CORS

Đặt proxy đáng tin cậy trước API, chuyển WebSocket cho `/hubs/messages` và `/hubs/notifications`, và không public PostgreSQL. Nếu proxy terminate TLS, bật forwarded headers và chỉ khai báo IP trực tiếp của proxy:

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
  sh -lc 'dotnet tool restore && dotnet tool run dotnet-ef database update --project backend/Fookbase.Src --startup-project backend/Fookbase.Src'
```

Sau migration, start/recreate API rồi kiểm tra:

```bash
docker compose -f compose.yml -f compose.prod.yml up -d
curl --fail --show-error https://api.example.com/health/live
curl --fail --show-error https://api.example.com/health/ready
docker compose -f compose.yml -f compose.prod.yml logs --tail=200 api
```

Nếu deploy lỗi, giữ volume, rollback image/application tương thích schema hoặc khôi phục backup đã được diễn tập. Không force-push, không xóa volume như một bước rollback.

## GitHub Actions deploy tự động

Workflow `.github/workflows/deploy-ec2.yml` chạy sau mỗi push vào `main`. Nó chạy trên self-hosted runner mang label `fookbase-production` tại EC2, build main web, publish source release, build API, chạy migration, chờ `/health/ready`, rồi publish static assets. Runner chủ động kết nối GitHub nên không phải mở port SSH cho dải IP GitHub Actions.

Runner phải chạy qua systemd service `actions.runner.lighthouse2815-fookbase-light.fookbase-production.service` dưới user `ubuntu`, có quyền chạy Docker và `sudo` cho các thao tác deploy. Web hiện được publish là `frontend/web`; admin và Zola Light chỉ nên thêm vào workflow sau khi có host/path production riêng.

## Jobs, shutdown và capacity

Video jobs có claim PostgreSQL điều kiện, lease timeout, retry giới hạn và output key deterministic. `Media__MaxConcurrentJobs=1` là default production an toàn; chỉ tăng cùng giới hạn CPU/RAM thực tế và `Media__VideoProcessingBatchSize`. Object deletion chạy durable, retry có delay và chuyển sang `FailedAtUtc` sau giới hạn để dễ chẩn đoán, không busy-loop.

Compose production đặt grace period API 45 giây để `BackgroundService` nhận SIGTERM/cancellation; job đang `Processing` sẽ được claim lại sau timeout nếu container dừng giữa chừng. API 1 CPU/1 GiB và PostgreSQL 2 CPU/2 GiB là guardrail khởi đầu, không phải sizing guarantee. Theo dõi FFmpeg trước khi tăng concurrency.

Application logs JSON ra stdout ở Production, không ghi file log trong container. Runtime Docker phải cấu hình log rotation (ví dụ `json-file` với `max-size`/`max-file`) hoặc thu stdout. Log request gồm method/path/status/duration/request ID và UserId khi đã xác thực; không log body, token, password, key, recovery code hay connection string. Client có thể cung cấp `X-Request-Id` từ error ProblemDetails để operator tìm log.

## Test và migration policy

Không chạy `dotnet test FookbaseLight.sln` làm integration regression vì projects từng dùng chung database có thể race migration. Lệnh chuẩn là:

```bash
bash scripts/test-backend.sh
```

Script tạo một PostgreSQL container tạm, database riêng cho từng nhóm test và chạy tuần tự. `scripts/check-migrations.sh` kiểm tra ID migration trùng/future-dated; thêm `CHECK_EF_MODEL=true` với connection string design-time để kiểm tra pending model changes. Luôn backup, migration một lần, start/restart một API, chờ readiness rồi smoke test.

## Đường scale được hoãn

Khi có bằng chứng cần nhiều API replica: thêm Redis SignalR backplane, chuyển presence sang shared ephemeral store, xác minh worker lease PostgreSQL đa-replica, sizing pool connection, thêm load balancer và load test. Sau đó mới cân nhắc worker process riêng, cache/CDN hoặc search infrastructure.
