# ERD trên Cloudflare Pages

Trang ERD: https://fookbase-light-erd.pages.dev/

Project Cloudflare Pages: `fookbase-light-erd`, production branch: `main`.

## Đóng gói và triển khai

Chạy từ thư mục gốc repository, sau khi commit và push các cập nhật ERD:

```bash
node scripts/build-erd-site.mjs
npx wrangler@4 login
npx wrangler@4 pages deploy dist/erd \
  --project-name fookbase-light-erd \
  --branch main \
  --commit-hash "$(git rev-parse HEAD)"
```

Chỉ cần đăng nhập Wrangler nếu máy chưa có phiên đăng nhập Cloudflare hợp lệ.

Script tạo `dist/erd/index.html` từ `docs/database-erd.html`. Các liên kết source
được chuyển sang GitHub và cố định tại commit hiện tại. Thư mục `dist/` được Git
bỏ qua; chỉ HTML được tải lên Pages. Trang dùng CSS, JavaScript và SVG nhúng sẵn,
không cần build frontend hay backend.

Project dùng Direct Upload. Sau mỗi lần cập nhật, chạy lại lệnh đóng gói và deploy;
push GitHub riêng lẻ không tự triển khai trang ERD.

Tham khảo [Cloudflare Pages Direct Upload](https://developers.cloudflare.com/pages/get-started/direct-upload/).
