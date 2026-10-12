# ERD trên Cloudflare Pages

Trang ERD: https://fookbase-light-erd.pages.dev/

Project Cloudflare Pages: `fookbase-light-erd`, production branch: `main`.

## Tự động triển khai khi push main

Workflow [Deploy ERD](../.github/workflows/deploy-erd.yml) đóng gói và deploy lên
Cloudflare Pages mỗi khi push vào `main`. Workflow chạy độc lập với Deploy EC2 và
cũng có thể chạy thủ công bằng `workflow_dispatch` trên nhánh `main`.

Cấu hình một lần tại repository GitHub → Settings → Secrets and variables →
Actions → New repository secret:

- `CLOUDFLARE_ACCOUNT_ID`: ID tài khoản chứa project `fookbase-light-erd`.
- `CLOUDFLARE_API_TOKEN`: API token có quyền **Account → Cloudflare Pages → Edit**,
  giới hạn cho tài khoản chứa project.

Sau khi thêm secrets, push vào `main` hoặc chạy lại workflow Deploy ERD nếu lần
chạy đầu tiên báo thiếu credentials. Không đưa token vào source code.

Mỗi deployment gắn với commit được push; các liên kết source trên ERD cũng cố
định tại commit đó. Xem trạng thái và liên kết deployment trong tab Actions.

Tham khảo [Cloudflare Pages với GitHub Actions](https://developers.cloudflare.com/pages/how-to/use-direct-upload-with-continuous-integration/).

## Triển khai thủ công từ máy local

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

Project dùng Direct Upload. Workflow GitHub Actions tự upload HTML sau mỗi push
vào `main`; các lệnh trên dùng khi cần triển khai thủ công.

Tham khảo [Cloudflare Pages Direct Upload](https://developers.cloudflare.com/pages/get-started/direct-upload/).
