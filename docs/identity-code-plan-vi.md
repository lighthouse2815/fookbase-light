# Kế hoạch chỉnh sửa module Identity

Tài liệu này ghi lại các điểm cần xử lý tiếp theo trong module Identity. Mục
tiêu là giữ nguyên hợp đồng API hiện tại, sửa các rủi ro bảo mật trước, sau đó
mới cải thiện cấu trúc và vận hành.

## Phạm vi và nguyên tắc

- Không thay đổi route hoặc format response nếu chưa có yêu cầu rõ ràng.
- Không gom nhiều thay đổi lớn vào một commit.
- Ưu tiên thao tác database nguyên tử, transaction ngắn và kết quả lỗi rõ ràng.
- Không để token, mật khẩu, recovery code hoặc dữ liệu OTP xuất hiện trong log.
- Giữ các thay đổi hiện có của người dùng trong working tree; chỉ sửa file liên
  quan đến từng mục trong kế hoạch.

## Thứ tự ưu tiên

### P1 — Bắt buộc xử lý trước khi mở rộng tính năng

#### 1. Bảo vệ 2FA challenge khỏi replay và brute-force

Hiện tại challenge 2FA được đọc, kiểm tra rồi đánh dấu đã dùng bằng thao tác
riêng. Hai request đồng thời có thể cùng vượt qua kiểm tra và cùng phát hành
session. Challenge cũng chưa có bộ đếm số lần nhập sai riêng.

Việc cần làm:

- Thêm `FailedAttemptCount` và giới hạn số lần sai vào
  `TwoFactorLoginChallenge`.
- Consume challenge bằng `UPDATE` có điều kiện
  `ConsumedAtUtc IS NULL`, `ExpiresAtUtc > now` và chưa vượt giới hạn.
- Đảm bảo chỉ request thắng thao tác nguyên tử mới được phát hành token.
- Với Google link đang chờ 2FA, consume challenge, thêm external login và tạo
  session trong cùng transaction phù hợp.
- Thêm test song song cho cùng challenge và test vượt số lần sai.

#### 2. Chốt chính sách moderation cho các endpoint `/api/auth`

Middleware hiện bỏ qua toàn bộ `/api/auth`, trong khi một số endpoint có token
đã xác thực vẫn thay đổi mật khẩu, 2FA hoặc session. Cần quyết định rõ endpoint
nào được phép dùng khi tài khoản bị suspend/disable.

Đề xuất:

- Cho phép `login`, `refresh`, `logout` và các luồng khôi phục cần thiết theo
  chính sách sản phẩm.
- Chặn đổi mật khẩu, setup/enable/disable 2FA, regenerate recovery codes và
  các thao tác thay đổi bảo mật khi tài khoản không khả dụng.
- Dùng một policy/service chung thay vì rải điều kiện ở từng controller.
- Bổ sung test suspend/disable rồi gọi từng endpoint nhạy cảm.

#### 3. Loại bỏ đường cập nhật trạng thái tài khoản cũ hoặc bổ sung revoke

`AdministrationService.UpdateUserStatusAsync` chỉ đổi `User.IsActive`. Nếu tài
khoản bị disable rồi enable lại, refresh token cũ chưa bị revoke có thể hoạt
động trở lại.

Việc cần làm:

- Ưu tiên bỏ route `/api/admin/users/{userId}/status` nếu moderation V1 đã thay
  thế route này.
- Nếu phải giữ backward compatibility, ủy quyền thao tác cho `ModerationService`
  và revoke toàn bộ session/refresh token khi disable.
- Khi enable lại, không khôi phục hiệu lực token đã tồn tại trước lúc disable.
- Thêm test disable → enable → thử refresh bằng token cũ.

### P2 — Tính nhất quán dữ liệu và xử lý lỗi

#### 4. Gộp refresh rotation vào một transaction hoàn chỉnh

Rotation hiện commit token mới và revoke token cũ trước khi cập nhật
`LastSeenAtUtc`. Nếu bước cập nhật session lỗi, token cũ đã mất hiệu lực nhưng
client không nhận được token mới.

Việc cần làm:

- Đưa revoke token, insert token thay thế và `session.Touch(now)` vào cùng
  transaction.
- Chỉ trả response sau khi transaction commit thành công.
- Thêm test mô phỏng lỗi ở bước cuối và kiểm tra không có trạng thái nửa vời.

#### 5. Kiểm tra đầy đủ `IdentityResult` trong luồng 2FA

Các thao tác reset authenticator key, bật/tắt 2FA và tạo recovery codes cần xử lý
kết quả thất bại thay vì trả success giả.

Việc cần làm:

- Kiểm tra và ánh xạ lỗi từ `ResetAuthenticatorKeyAsync`.
- Kiểm tra `SetTwoFactorEnabledAsync` khi disable, không tiếp tục nếu thất bại.
- Kiểm tra kết quả tạo recovery codes; không bật 2FA nếu không thể tạo mã theo
  chính sách đã chọn.
- Bổ sung test service/controller cho từng lỗi framework.

#### 6. Chuẩn hóa clock và transaction boundary

- Inject `TimeProvider` vào `SessionsController`, không gọi trực tiếp
  `TimeProvider.System`.
- Rà lại các hàm vừa bulk update vừa `SaveChangesAsync` để xác định transaction
  cần thiết.
- Giữ `CancellationToken` xuyên suốt các thao tác database và provider.

### P3 — Vận hành, bảo trì và hardening

#### 7. Dọn dữ liệu challenge hết hạn

`RegistrationChallenges`, `PasswordResetChallenges`,
`TwoFactorLoginChallenges` và `ExternalLoginCompletions` hiện không có quy trình
cleanup. Cần thêm worker hoặc job batch xóa bản ghi đã hết hạn/đã tiêu thụ sau
thời gian lưu giữ hợp lý, có index hỗ trợ và log tổng hợp không chứa dữ liệu bí
mật.

#### 8. Ràng buộc URL frontend dùng trong email

`EmailOptions.FrontendBaseUrl` được dùng để đưa password-reset/email-verification
token vào URL nhưng cấu hình production chưa buộc URL tuyệt đối và HTTPS.

Việc cần làm:

- Validate scheme, host, không có user info/query bất thường.
- Bắt buộc HTTPS trong production.
- Thêm test cấu hình hợp lệ và không hợp lệ.

#### 9. Cải thiện cấu trúc sau khi hành vi đã ổn định

`AuthenticationService` đang phụ trách login, registration legacy, 2FA,
password reset, email verification và session. Sau khi các test bảo mật ở trên
ổn định, có thể tách theo use case nhỏ hơn, ví dụ:

- `SessionAuthenticationService` cho login/refresh/logout/session.
- `TwoFactorAuthenticationService` cho setup/verify/recovery.
- `PasswordRecoveryService` cho email/SMS reset.

Chỉ tách khi có ranh giới thực tế và giữ nguyên DTO, route, error code hiện tại.

## Trình tự triển khai đề xuất

1. Viết test tái hiện 2FA replay, brute-force và refresh token cũ sau disable.
2. Sửa atomic consume và giới hạn attempt của 2FA; tạo migration nếu thêm cột.
3. Chốt và áp dụng policy moderation cho các endpoint auth nhạy cảm.
4. Xử lý route admin cũ và revoke session/refresh token đúng chính sách.
5. Gộp refresh rotation vào transaction; kiểm tra các `IdentityResult` bị bỏ qua.
6. Chuẩn hóa `TimeProvider`, config validation và cleanup job.
7. Chỉ sau khi regression pass mới xem xét tách `AuthenticationService`.

## Kiểm chứng bắt buộc

- `dotnet build` cho API và test project.
- Nhóm test Identity và Shared.
- `bash scripts/test-backend.sh` cho toàn bộ backend.
- Test concurrency tối thiểu cho 2FA challenge, registration verify,
  password-reset OTP và refresh rotation.
- `git diff --check` trước mỗi commit.
- Mỗi phần độc lập có một commit Conventional Commit tiếng Việt, ví dụ:
  `fix: bảo vệ 2fa challenge khỏi replay`.

## Tiêu chí hoàn thành

- Challenge 2FA chỉ phát hành tối đa một session thành công.
- Mã 2FA sai bị giới hạn theo challenge/user, không chỉ theo IP.
- Disable tài khoản không thể làm token cũ sống lại sau khi enable.
- Các thao tác security của tài khoản bị moderation tuân theo cùng một policy.
- Refresh rotation không để trạng thái token/session nửa vời khi có lỗi.
- Không còn kết quả `IdentityResult` quan trọng bị bỏ qua.
- Tất cả test liên quan và regression backend đều pass trước khi merge.
