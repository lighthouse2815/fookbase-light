# Privacy & Account Security V1

## Privacy

Mỗi tài khoản có một dòng `UserPrivacySettings`, được tạo khi đăng ký hoặc lazy khi tài khoản cũ dùng cài đặt lần đầu. Mặc định tương thích hành vi cũ: bài viết `public`, nhận lời mời từ `everyone`, và các danh sách quan hệ `public`.

- `defaultPostPrivacy`: `public`, `friends`, `onlyMe`; chỉ được dùng cho Profile Post không gửi privacy rõ ràng.
- `friendRequestPolicy`: `everyone` hoặc `friendsOfFriends`; block luôn được ưu tiên.
- `friendListVisibility` và `followListVisibility`: `public`, `friends`, `onlyMe`. Chủ sở hữu luôn xem được; block không cho lộ identity.

API chỉ cho chủ sở hữu đọc/sửa settings: `GET` và `PATCH /api/privacy`.

## Sessions and tokens

Mỗi đăng nhập/đăng ký tạo một `AuthSession`; refresh rotation giữ nguyên `SessionId`, cập nhật `LastSeenAtUtc`, và refresh token chỉ lưu hash. JWT mang claim `sid` để đánh dấu phiên hiện tại. `GET /api/auth/sessions`, `DELETE /api/auth/sessions/{id}`, và `POST /api/auth/sessions/revoke-others` chỉ hoạt động với phiên của chính chủ.

Revoking session chặn refresh ngay lập tức. Access JWT đã cấp không bị lookup database trên từng request và có thể còn hiệu lực đến hết lifetime ngắn của nó. Refresh token trước M16 được revoke trong migration vì không thể gán session chain một cách đáng tin cậy; người dùng đăng nhập lại.

Access-token lifetime và refresh-token lifetime vẫn lấy từ `JwtOptions` (mặc định lần lượt 15 phút và 30 ngày). Refresh token cũ bị revoke khi xoay vòng.

## Password and lockout

Mật khẩu vẫn dùng ASP.NET Core Identity policy hiện có: ít nhất 8 ký tự, không bắt buộc loại ký tự. Sau 5 lần sai, Identity lock tài khoản trong 15 phút. Đổi mật khẩu kiểm tra mật khẩu hiện tại và thu hồi các session refresh khác; mật khẩu không được log.

## TOTP

TOTP dùng ASP.NET Core Identity authenticator token provider. Setup trả shared key và otpauth URI cho phiên đã xác thực; setup không tự bật 2FA. Enable yêu cầu mã TOTP và chỉ trả recovery codes một lần. Recovery codes do Identity lưu dạng bảo mật, dùng một lần; regeneration thay thế toàn bộ mã cũ.

Khi 2FA bật, login email/password chỉ trả challenge single-use hết hạn sau 5 phút, không trả access/refresh token. `POST /api/auth/2fa/verify` xác minh authenticator hoặc recovery code rồi mới tạo session. Disable yêu cầu mật khẩu hiện tại và thu hồi session khác.

## Deferred

M16 không thêm OAuth, WebAuthn/passkeys, email delivery mới, forgot-password workflow mới, account deletion, personal-data export, custom audiences, Redis/Kafka, hoặc chính sách enterprise.
