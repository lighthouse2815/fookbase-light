# React + TypeScript + Vite

Các file trong `tests/` chỉ có ở máy local và được Git bỏ qua.
Lệnh test và hướng dẫn kiểm tra bằng trình duyệt bên dưới cần bộ test local.

This template provides a minimal setup to get React working in Vite with HMR and some Oxlint rules.

## API during development

`npm run dev` proxies `/api` requests to `http://localhost:5000`, the default backend URL.
Set `VITE_API_PROXY_TARGET` to use another backend URL. For a separately deployed frontend,
set `VITE_API_BASE_URL` to the API origin instead.

The sign-in flow stores the JWT session in local storage and refreshes an expired access token automatically.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the Oxlint configuration

If you are developing a production application, we recommend enabling type-aware lint rules by installing `oxlint-tsgolint` and editing `.oxlintrc.json`:

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["react", "typescript", "oxc"],
  "options": {
    "typeAware": true
  },
  "rules": {
    "react/rules-of-hooks": "error",
    "react/only-export-components": ["warn", { "allowConstantExport": true }]
  }
}
```

See the [Oxlint rules documentation](https://oxc.rs/docs/guide/usage/linter/rules) for the full list of rules and categories.

## SEO và Google Search

Trang gốc công khai được React/Vite pre-render sẵn vào HTML khi build để giới thiệu nhất quán các tên thương hiệu **Fookbase**, **Fookbase Light** và `fookbase-light`. Người dùng đã đăng nhập vẫn đi theo luồng ứng dụng tại `/feed`; các trang ứng dụng không phải trang đích tìm kiếm sẽ dùng `noindex`.

Mặc định, site public chính là `https://www.fookbase.io.vn`. Đặt `VITE_SITE_URL` thành một HTTP(S) origin khi build (qua `frontend/web/.env.production.local` hoặc biến môi trường của process build) nếu deploy sang domain khác. Biến này tạo đồng bộ canonical URL, Open Graph, JSON-LD, `robots.txt` và `sitemap.xml`; không thêm path, query, hash hoặc credential. Sitemap chỉ liệt kê trang gốc, còn `robots.txt` vẫn cho crawler truy cập các route ứng dụng để chúng đọc được chỉ thị `noindex`.

Production phục vụ cùng một bản web và backend/database trên `www.fookbase.io.vn`, `fookbase.io.vn` và `fookbase-light.duckdns.org`, không chuyển hướng HTTPS giữa các domain. Cả ba cùng khai báo canonical về bản `www` để thống nhất tín hiệu SEO; trạng thái đăng nhập trong trình duyệt vẫn riêng theo origin. Nginx và chứng chỉ TLS cần bao gồm cả ba hostname. `AllowedHosts` dùng danh sách phân cách bằng dấu `;` và phải đặt trong dấu nháy nếu file môi trường được shell đọc. Giữ các CORS origin hiện có; các mục `Cors__AllowedOrigins__3`, `4`, `5` lần lượt dành cho domain không `www`, domain `www` và DuckDNS. Google OAuth phải cho phép `/signin-google` trên cả ba domain; `GoogleAuthentication__WebBaseUrl` trỏ về bản `www`, nên hoàn tất Google login cho client web sẽ quay về domain chính.

Sau khi deploy, owner domain cần tự thực hiện trong [Google Search Console](https://search.google.com/search-console/about):

1. Tạo property **URL prefix** đúng với origin production và xác minh quyền sở hữu.
2. Gửi `https://<domain>/sitemap.xml`, rồi dùng URL Inspection để yêu cầu index trang gốc.
3. Theo dõi báo cáo Search results theo truy vấn, trang và lỗi lập chỉ mục.

Tham khảo hướng dẫn chính thức của Google về [khởi động Search Console](https://developers.google.com/search/docs/monitor-debug/search-console-start), [sitemap](https://developers.google.com/search/docs/crawling-indexing/sitemaps/overview) và [yêu cầu Google thu thập lại URL](https://developers.google.com/search/docs/crawling-indexing/ask-google-to-recrawl). Repository không lưu credential Search Console, nên bước xác minh/gửi sitemap cần do owner có quyền tài khoản thực hiện. Các bước này giúp Google phát hiện và hiểu site, nhưng không bảo đảm thứ hạng hoặc xuất hiện ngay trên trang đầu.

Kiểm tra SEO trên bản build production bằng `npm run build`, rồi chạy `npm run preview -- --host 127.0.0.1 --port 5197 --strictPort`. Ở terminal khác, chạy `node tests/browser/seo.mjs` với Playwright/Chromium đã cài; có thể đặt `PLAYWRIGHT_MODULE` để dùng Playwright ngoài repository, `SEO_BASE_URL` nếu đổi cổng và `SEO_SITE_URL` nếu build cho domain khác. Bộ kiểm tra bao gồm domain canonical, HTML trước khi chạy JavaScript, sitemap/robots, giao diện điện thoại khi tắt JavaScript và luồng vào trang đăng nhập/bảng tin. Với static hosting, các route ứng dụng dùng chung HTML entry; chỉ thị `noindex` cho chúng được cập nhật khi JavaScript chạy.
