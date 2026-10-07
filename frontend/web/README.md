# Fookbase Web

Các file trong `tests/` chỉ có ở máy local và được Git bỏ qua.
Lệnh test và hướng dẫn kiểm tra bằng trình duyệt bên dưới cần bộ test local.

Client mạng xã hội Fookbase Light, dùng React, TypeScript, React Router, Vite và Tailwind CSS.

## Chạy và kiểm tra

```bash
npm ci
npm run dev
npm run lint
npm run build
```

`test:games` chạy các test Node ở `tests/*.test.mjs`, gồm cả feed, tìm kiếm, thông báo và tương tác bài viết. `test:auth` kiểm tra hợp đồng Identity của web, admin và Zola Light.

## Kết nối API

Development proxy `/api` và `/hubs` tới `http://localhost:5000`. Đặt `VITE_API_PROXY_TARGET` nếu backend dùng origin khác; đặt `VITE_API_BASE_URL` khi frontend và API triển khai riêng.

Web lưu access token và thông tin người dùng trong local storage; refresh token dùng cookie HttpOnly. API client tự làm mới phiên khi access token hết hạn và giữ phiên khi gặp lỗi mạng hoặc lỗi máy chủ tạm thời.

## Cache và phân trang

Saved và Search dùng TanStack Query để quản lý dữ liệu, lỗi và cursor. Cache nằm trong bộ nhớ, riêng cho từng tài khoản, có stale time 30 giây và được xóa khi đổi tài khoản/đăng xuất. Thao tác lưu, bỏ lưu, sửa và xóa bài viết cập nhật hoặc đánh dấu cache Saved cần tải lại. Retry giữ nút điều khiển trên giao diện; request bị hủy khi trang hoặc phiên thay đổi.

Sau khi chạy web, kiểm tra Saved bằng `SAVED_BASE_URL=http://127.0.0.1:5173 node tests/browser/savedQueries.mjs`. Script dùng Playwright/Chromium như các browser check hiện có; có thể đặt `PLAYWRIGHT_MODULE` để dùng bản Playwright đã cài ngoài repository.

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
