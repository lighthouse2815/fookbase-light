# Sao lưu và khôi phục

Backup V1 gồm cả PostgreSQL metadata và object MinIO. Chúng không có distributed snapshot transaction, nên có một consistency window nhỏ. Ghi lại timestamp của hai backup, ưu tiên chạy gần nhau, và luôn diễn tập restore vào môi trường khác trước khi tin backup.

Không đặt dump, object backup, `.env`, Data Protection keys hay credentials trong repository.

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

## MinIO

`scripts/backup-minio.sh` dùng official `minio/mc` image để mirror object key nguyên vẹn. Bucket đích/source không bị đổi anonymous policy; bucket private vẫn private. Set endpoint theo network chứa MinIO:

```bash
export MINIO_ENDPOINT=http://minio:9000
export MINIO_ACCESS_KEY="$MINIO_ROOT_USER"
export MINIO_SECRET_KEY="$MINIO_ROOT_PASSWORD"
export MINIO_BUCKET=fookbase-media
export MINIO_DOCKER_NETWORK=<compose-network>
export MINIO_BACKUP_DIR=/srv/fookbase-minio-backups
bash scripts/backup-minio.sh
```

Với Docker Linux và MinIO expose trên host có thể dùng `MINIO_DOCKER_NETWORK=host` cùng endpoint host. Khôi phục vào bucket khác trước để verify:

```bash
bash scripts/restore-minio.sh \
  --source /srv/fookbase-minio-backups/fookbase-media \
  --target-bucket fookbase-media-restore-smoke \
  --yes-restore
```

Dùng `mc stat` hoặc download một object đại diện để xác nhận object key, size và content. Sau disaster recovery, restore MinIO bucket chính rồi PostgreSQL (hoặc ngược lại trong maintenance window), start API, check `/health/ready`, login một user mẫu, đọc post mẫu và presign/download media mẫu.

## Retention và key ring

Điểm bắt đầu thực tế: backup hàng ngày, giữ 14 daily gần nhất và 8 weekly; copy sang storage/host khác. Chỉ tự động xóa sau khi retention, ownership và restore verification đã được review. Backup `data-protection-keys` cùng các backup ứng dụng: mất key ring làm cursor/token được bảo vệ trước đó không còn đọc được sau recreation.

Mỗi quý nên diễn tập: database user + post representative → `pg_dump` → restore DB khác → query assertion; object representative → mirror → restore bucket khác → compare content. Kết quả diễn tập phải được ghi vào runbook vận hành, không lưu dữ liệu test trong Git.
