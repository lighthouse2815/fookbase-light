# Lịch sử migration EF Core

`scripts/check-migrations.sh` kiểm tra timestamp của migration theo múi giờ quy ước
`Asia/Ho_Chi_Minh`. Chỉ đặt `MIGRATION_TIME_ZONE` khi quy trình tạo migration có chủ đích dùng
múi giờ khác.

Hệ thống chỉ có một nguồn migration cho runtime:

```text
backend/Fookbase.Src/Code/Persistence/Migrations/
```

`FookbaseDbContext` dùng migration nền `20261010061733_InitialFookbaseBaseline`.
Ngày 10/10/2026, 53 migration từ `20260910143327_InitialFookbase` đến
`20261006182552_AddReelViewRelationships` được gộp thành migration nền này để giảm code sinh tự động.
Thư mục còn ba file: migration, Designer và `FookbaseDbContextModelSnapshot.cs`.
Snapshot giữ nguyên; migration nền giữ các bảng, cột, khóa ngoại, constraint, index,
extension `citext`, `pg_trgm` và tám index tìm kiếm viết bằng SQL.

## Database mới

Chạy `dotnet ef database update` như bình thường. Migration nền tạo toàn bộ schema.
Không chạy script chuyển lịch sử trên database trống.

## Database hiện có

Trước lần deploy đầu tiên dùng migration nền, backup và xác nhận database đã áp dụng đủ
53 migration cũ, với mốc cuối `20261006182552_AddReelViewRelationships`.
Lịch sử migration không chứng minh schema chưa bị sửa tay: cần đối chiếu schema thực tế
với database tạo từ bộ migration cũ nếu có nghi ngờ.

Chạy `scripts/baseline-migrations.sql` trên từng database hiện có sau khi kiểm tra.
Script dùng transaction và khóa bảng lịch sử, yêu cầu đúng toàn bộ 53 ID cũ,
rồi chỉ thêm bản ghi migration nền vào `__EFMigrationsHistory`.
Nó không xóa lịch sử cũ, không thay đổi bảng ứng dụng hoặc dữ liệu, và chạy lại không thêm bản ghi trùng.
Database thiếu migration hoặc có ID lạ sẽ bị từ chối trước khi ghi.

Ví dụ với Compose production, từ thư mục repository có file môi trường ngoài Git:

```bash
set -a
. ./.env.production
set +a
BACKUP_DIR=/srv/fookbase-backups bash scripts/backup-postgres.sh
docker compose --env-file .env.production -f compose.yml -f compose.prod.yml \
  exec -T postgres psql --set ON_ERROR_STOP=1 \
  --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  < scripts/baseline-migrations.sql
```

Trên EC2, thêm `-f compose.ec2.yml` và dùng `sudo docker` nếu tài khoản vận hành cần.
Sau khi đánh dấu baseline, quy trình deploy và `dotnet ef database update` hoạt động bình thường.
Nếu chưa đánh dấu, migration nền sẽ dừng với thông báo yêu cầu chạy script;
nó không cố tạo lại bảng trên database có lịch sử.
Chuẩn bị tất cả database trước khi bật deploy tự động hoặc chạy workflow deploy thủ công.

Nếu database chưa ở mốc cuối, dùng source tại commit `0d108639` trong một checkout riêng
để áp dụng migration cũ trước, rồi kiểm tra và chạy script chuyển lịch sử.
Không dùng baseline để bỏ qua các thay đổi còn thiếu.

## Thay đổi tiếp theo

Tiếp tục tạo migration bằng EF trong cùng thư mục và namespace:

```bash
dotnet tool run dotnet-ef migrations add TenThayDoi \
  --context FookbaseDbContext --project backend/Fookbase.Src \
  --startup-project backend/Fookbase.Src \
  --output-dir Code/Persistence/Migrations \
  --namespace Fookbase.Api.Persistence.Migrations
```

Không tạo migration riêng trong từng module hoặc gộp lại baseline trong bảo trì thông thường.
Lịch sử source cũ vẫn có trong Git. Bộ mới không hỗ trợ rollback từng mốc cũ;
không rollback baseline trên database có dữ liệu vì `Down` xóa các bảng.
