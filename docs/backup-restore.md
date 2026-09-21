# Sao lưu và khôi phục

Sao lưu PostgreSQL để bảo vệ tài khoản, bài viết và metadata media. Ảnh/video nằm trên Cloudinary nên bản dump PostgreSQL không chứa nội dung file. Hai nguồn dữ liệu không có snapshot chung; ghi lại thời điểm sao lưu và diễn tập khôi phục trước khi tin bản backup.

Không đặt dump, bản sao media, `.env`, khóa Data Protection hay thông tin đăng nhập trong repository.

## PostgreSQL

Các script dùng service `postgres` của Docker Compose, không in password. Backup custom format (`pg_dump --format=custom`) hỗ trợ restore chọn lọc và cần một thư mục ngoài repository:

```bash
set -a; . ./.env.production; set +a
BACKUP_DIR=/srv/fookbase-backups bash scripts/backup-postgres.sh
```

Khôi phục luôn cần tên database đích rõ ràng và flag xác nhận. Tạo database trống trước, không dùng `fookbase_db` production trong buổi diễn tập:

```bash
docker compose exec -T postgres createdb -U "$POSTGRES_USER" fookbase_restore_smoke
bash scripts/restore-postgres.sh \
  --file /srv/fookbase-backups/fookbase_db-<timestamp>.dump \
  --target-database fookbase_restore_smoke \
  --yes-restore
docker compose exec -T postgres psql -U "$POSTGRES_USER" -d fookbase_restore_smoke \
  -c 'SELECT COUNT(*) FROM "AspNetUsers";'
```

`--clean --if-exists` có thể thay schema/data của database đích; script không có default target và từ chối thiếu `--yes-restore`.

## Media trên Cloudinary

Sao lưu PostgreSQL không sao lưu ảnh/video trên Cloudinary. Cần có chính sách lưu bản sao media độc lập phù hợp với tài khoản Cloudinary đang dùng và bảo toàn public ID, loại tài nguyên và quyền phân phối `authenticated`. Repository hiện không có script xuất/khôi phục media Cloudinary; không xem bản dump PostgreSQL là bản backup đầy đủ của ứng dụng.

Sau khi khôi phục, kiểm tra `/health/ready`, đăng nhập bằng tài khoản kiểm thử, đọc một bài viết có media và xác nhận URL đọc có chữ ký còn truy cập được.

## Retention và key ring

Điểm bắt đầu thực tế: backup hàng ngày, giữ 14 daily gần nhất và 8 weekly; copy sang storage/host khác. Chỉ tự động xóa sau khi retention, ownership và restore verification đã được review. Backup `data-protection-keys` cùng các backup ứng dụng: mất key ring làm cursor/token được bảo vệ trước đó không còn đọc được sau recreation.

Mỗi quý nên diễn tập: chọn tài khoản và bài viết đại diện → `pg_dump` → khôi phục vào database khác → kiểm tra dữ liệu; đồng thời kiểm tra khả năng truy xuất và quy trình khôi phục media Cloudinary theo chính sách sao lưu đã chọn. Ghi kết quả vào runbook vận hành, không lưu dữ liệu test trong Git.
