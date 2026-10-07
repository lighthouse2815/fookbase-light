# Fookbase Light

Mạng xã hội dùng ASP.NET Core, PostgreSQL, Cloudinary và SignalR. Backend nằm ở
`backend/Fookbase.Src`; các client nằm trong `frontend/`.

## Cấu hình

Cần .NET SDK 10, Node.js/npm và Docker Compose. Từ thư mục gốc repository:

```bash
cp .env.example .env
```

Điền cấu hình Cloudinary trước khi chạy. SMTP, Google, push notification và AI là
các tính năng tùy chọn được cấu hình bằng biến môi trường trong `.env.example`.
Credential mẫu chỉ dùng cho development; không commit `.env` hoặc API key.

## Chạy backend

Chạy PostgreSQL và API bằng Docker Compose:

```bash
docker compose up --build -d
docker compose ps
```

Hoặc chạy PostgreSQL bằng Docker và API bằng .NET SDK:

```bash
docker compose up -d postgres
docker compose stop api
bash scripts/run-backend.sh
```

Script tự nạp `.env` và bật migration cho development. API chạy tại
<http://localhost:5000>; Swagger ở <http://localhost:5000/swagger> trong development.

```bash
curl -fsS http://localhost:5000/health/live
curl -fsS http://localhost:5000/health/ready
docker compose logs -f api
```

`/health/ready` kiểm tra PostgreSQL và Cloudinary. Tài khoản quản trị cần được gán
role `Admin`; hệ thống không tự cấp quyền quản trị theo email.

## Chạy frontend

Mỗi client có package-lock riêng. Ví dụ chạy web:

```bash
cd frontend/web
npm ci
npm run dev
```

| Client | Thư mục | Local URL |
| --- | --- | --- |
| Web | `frontend/web` | <http://localhost:5173> |
| Admin | `frontend/admin` | <http://localhost:5174> |
| Zola Light | `frontend/zola-light` | <http://localhost:5175> |

Admin và Zola Light chạy bằng các lệnh tương tự trong thư mục tương ứng.
Các origin trên đã có trong `.env.example`.

App mobile cần API HTTPS và file `.env.local` riêng. Xem hướng dẫn
[Fookbase Mobile](docs/mobile-android.md) và [Zola Mobile](docs/zola-mobile.md).

## Build và kiểm tra

Từ thư mục gốc:

```bash
dotnet restore FookbaseLight.sln
dotnet build FookbaseLight.sln --no-restore

for app in web zola-light admin; do
  (cd "frontend/$app" && npm ci && npm run lint && npm run build)
done

for app in mobile zola-mobile; do
  (cd "frontend/$app" && npm ci && npm run typecheck && npm run lint)
done
```

Bộ test được giữ local và được Git bỏ qua; bản clone mới không có file test.
Nếu máy có bộ test, chạy `bash scripts/test-backend.sh` và các lệnh test npm hiện có.
`FookbaseLight.Local.sln` giữ project test để mở trong IDE; solution trên GitHub chỉ
chứa API. CI thủ công chạy build, lint và typecheck.

## Vận hành

- [Deploy, cấu hình production và xử lý sự cố EC2](docs/production-deployment.md)
- [Backup và restore PostgreSQL/media](docs/backup-restore.md)
- [Quy ước migration EF Core](docs/migration-history.md)
