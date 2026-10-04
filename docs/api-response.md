# Format response API

Hiện áp dụng cho các API JSON dưới `/api/auth`. Các module khác tiếp tục dùng
format hiện tại trong giai đoạn chuyển đổi.

## Thành công

Ví dụ `GET /api/auth/providers`, HTTP 200, `Content-Type: application/json`:

```json
{
  "success": true,
  "data": { "google": true, "googleMobile": false },
  "error": null,
  "requestId": "a-request-id"
}
```

DTO nằm trong `data`, gồm cả token đăng nhập, challenge 2FA và danh sách
session. Các thao tác không có dữ liệu (logout, reset password, xác minh email,
thu hồi session…) trả HTTP 200 với `data: null`, thay cho HTTP 204 trước đây.
Các response tạo tài khoản và bắt đầu đăng ký giữ HTTP 201/202 và header
`Location`.

## Thất bại

Ví dụ lỗi nghiệp vụ, HTTP 401, `Content-Type: application/json`:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "invalid_credentials",
    "message": "The email, phone number, or password is invalid.",
    "details": null
  },
  "requestId": "a-request-id"
}
```

HTTP status vẫn thể hiện kết quả: 400, 401, 403, 404, 405, 409, 415, 429 hoặc 500.
Lỗi từng trường nằm trong `error.details`, dạng `{ "Password": ["..."] }`;
mã lỗi nghiệp vụ hiện có được giữ. Body JSON hỏng, token không hợp lệ, route
không tồn tại, sai HTTP method/content type và rate limit cũng dùng cấu trúc
này. Lỗi bất ngờ trả `internal_server_error` và thông báo chung, không lộ
exception nội bộ. `requestId` dùng để đối chiếu log và header `X-Request-Id`.

## Cách sử dụng

Service trả DTO hoặc `Task` khi thành công; lỗi nghiệp vụ ném
`BusinessException`. Service không tạo response HTTP. Controller bọc thành
công bằng `ApiResponse.Success`; handler và xử lý lỗi HTTP dùng
`ApiResponse.Failure`.

Đây là thay đổi hợp đồng JSON của Identity. Các client đều đọc dữ liệu trong
`data` và lỗi trong `error.message`. Redirect/challenge OAuth vẫn dùng giao thức
HTTP của OAuth, không bọc vào JSON.

### Refresh token trên browser

Web, Admin và Zola Light gửi `X-Fookbase-Auth-Transport` tương ứng với giá trị
`cookie:web`, `cookie:admin` hoặc `cookie:zola-light`, đồng thời bật
`credentials: include`. API lưu refresh token trong cookie riêng của từng client
với `HttpOnly`, `SameSite=Lax`, `Path=/api/auth` và bật `Secure` khi request dùng
HTTPS. Response browser chỉ trả access token và thời hạn refresh token, không có
trường `refreshToken`.

Header transport là bắt buộc khi dùng refresh cookie để ngăn request cross-site
đọc cookie tự động. Client mobile và các API client khác không gửi header này,
vẫn nhận refresh token trong JSON và gửi token trong body như trước. Client browser
cũ có thể gửi token body một lần qua transport cookie để được xoay vòng rồi xóa
refresh token khỏi `localStorage`.

Kiểm tra: `bash scripts/test-backend.sh`, nhóm test Shared, và
`npm --prefix frontend/web run test:auth` (client web/admin/zola-light).
Mobile có test client, session, Google và logout trong Jest.
