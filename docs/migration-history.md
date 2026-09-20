# Lịch sử migration EF Core

`scripts/check-migrations.sh` kiểm tra timestamp của migration theo múi giờ quy ước
`Asia/Ho_Chi_Minh`. Chỉ đặt `MIGRATION_TIME_ZONE` khi quy trình tạo migration có chủ đích dùng
múi giờ khác.

Hệ thống chỉ có một nguồn migration cho runtime:

```text
backend/Fookbase.Src/Main/Code/Persistence/Migrations/
```

`FookbaseDbContext` bắt đầu bằng migration nền
`20260910143327_InitialFookbase`; các thay đổi tiếp theo nằm cùng thư mục và dùng chung snapshot.
Migration mới phải được tạo bằng `FookbaseDbContext` trong thư mục này. Không tạo migration riêng
trong từng module và không tạo lại migration nền trong quá trình bảo trì thông thường.
