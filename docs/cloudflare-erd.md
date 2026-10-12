# ERD trên Cloudflare Pages

Trang ERD: https://fookbase-light-erd-auto.pages.dev/

Link cũ https://fookbase-light-erd.pages.dev/ chuyển hướng sang trang trên.

Project Cloudflare Pages: `fookbase-light-erd-auto`, kết nối GitHub giống các
frontend Web, Admin và Zola.

## Tự động triển khai khi push main

Cloudflare tự lấy source từ GitHub, đóng gói và deploy mỗi khi push vào `main`.
Không cần workflow GitHub Actions hay repository secrets cho ERD.

| Cấu hình Cloudflare Pages | Giá trị |
| --- | --- |
| Repository | `lighthouse2815/fookbase-light` |
| Production branch | `main` |
| Root directory | Gốc repository |
| Build command | `node scripts/build-erd-site.mjs` |
| Build output directory | `dist/erd` |
| Environment variables | `NODE_VERSION=24`, `ERD_REPOSITORY_URL=https://github.com/lighthouse2815/fookbase-light` |
| Production deployments | Bật, mọi push vào `main` |
| Preview deployments | Tắt |

Mỗi deployment gắn với commit được push; các liên kết source trên ERD cũng cố
định tại commit đó. Xem trạng thái trong Cloudflare → Workers & Pages →
`fookbase-light-erd-auto` → Deployments hoặc check Cloudflare trên commit GitHub.
Khi cần build lại, dùng **Retry deployment** trong Cloudflare.

`ERD_REPOSITORY_URL` là cấu hình công khai để tạo liên kết source mà không phụ
thuộc định dạng remote trong môi trường build Cloudflare. Chạy local không cần
biến này; script dùng remote `origin` hiện tại.

Project cũ `fookbase-light-erd` dùng Direct Upload. Cloudflare không hỗ trợ đổi
trực tiếp project đó sang Git integration, nên project mới dùng Git integration
và project cũ chỉ giữ chuyển hướng cho các liên kết đã chia sẻ.

Tham khảo [Cloudflare Pages Git integration](https://developers.cloudflare.com/pages/configuration/git-integration/)
và [giới hạn chuyển đổi Direct Upload](https://developers.cloudflare.com/pages/get-started/direct-upload/).

## Triển khai thủ công từ máy local

Chạy từ thư mục gốc repository, sau khi commit và push các cập nhật ERD:

```bash
node scripts/build-erd-site.mjs
npx wrangler@4 login
npx wrangler@4 pages deploy dist/erd \
  --project-name fookbase-light-erd-auto \
  --branch main \
  --commit-hash "$(git rev-parse HEAD)"
```

Chỉ cần đăng nhập Wrangler nếu máy chưa có phiên đăng nhập Cloudflare hợp lệ.

Script tạo `dist/erd/index.html` từ `docs/database-erd.html`. Các liên kết source
được chuyển sang GitHub và cố định tại commit hiện tại. Thư mục `dist/` được Git
bỏ qua; chỉ HTML được tải lên Pages. Trang dùng CSS, JavaScript và SVG nhúng sẵn,
không cần build frontend hay backend.

Các lệnh trên chỉ dùng khi cần upload thủ công; triển khai thông thường do
Cloudflare Git integration tự thực hiện sau mỗi push vào `main`.
