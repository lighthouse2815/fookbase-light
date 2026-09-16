# Đăng ký bằng email hoặc số điện thoại với OTP

## Mục tiêu

Thay thế đăng ký trực tiếp hiện tại bằng luồng hai bước: người dùng nhập họ, tên, ngày sinh, giới tính, một định danh liên hệ (email hoặc số điện thoại) và mật khẩu; Fookbase gửi OTP đến định danh đó; chỉ sau khi OTP đúng mới tạo tài khoản, hồ sơ, cài đặt quyền riêng tư và phiên đăng nhập.

Email và số điện thoại đều là định danh đăng ký/đăng nhập hợp lệ. Email gửi OTP qua SMTP hiện có; SMS gửi OTP qua SpeedSMS khi hệ thống được cấu hình API key. Không đưa credential SMTP hoặc SpeedSMS vào source hay config được track.

## Phạm vi và ràng buộc

- `frontend/web` thay form đăng ký trong `LoginPage` bằng các trường theo mẫu: họ, tên, ngày/tháng/năm sinh, giới tính, email hoặc số điện thoại, mật khẩu. Trường liên hệ chỉ nhận đúng một email hoặc một số điện thoại Việt Nam.
- Username không còn là trường nhập khi đăng ký. Backend tạo username ASCII từ họ tên, giới hạn 3--32 ký tự, thêm hậu tố tăng dần khi trùng.
- Tên hiển thị mặc định là họ tên đầy đủ. Ngày sinh được lưu với chế độ hiển thị `OnlyMe` có sẵn. Thêm giới tính vào `UserProfile`, mặc định chỉ chủ tài khoản đọc được và không đưa vào public profile trong milestone này.
- Tài khoản email cũ tiếp tục đăng nhập, refresh token, 2FA, Google sign-in, reset mật khẩu và API hiện có không bị phá vỡ.
- Số điện thoại được chuẩn hóa về E.164 Việt Nam (`+84...`) trước khi kiểm tra trùng hoặc gửi SMS. Không hỗ trợ số quốc tế trong milestone này.

## Kiến trúc

### Đăng ký chờ và OTP

Thêm entity/bảng Identity `RegistrationChallenge`. Bản ghi chỉ chứa dữ liệu cần để hoàn tất đăng ký: loại định danh (`email` hoặc `phone`), giá trị chuẩn hóa, hash OTP, hash mật khẩu, họ tên, ngày sinh, giới tính, thời điểm hết hạn, thời điểm gửi lại sớm nhất, số lần thử và thời điểm đã dùng. Không lưu OTP hay mật khẩu dạng thô.

API tạo OTP gồm 6 chữ số ngẫu nhiên mật mã, hash trước khi ghi, hết hạn trong 10 phút, chỉ dùng một lần. Mỗi contact chỉ có một challenge đang dùng được; yêu cầu tạo lại thay thế challenge cũ. Gửi lại giữ dữ liệu đăng ký, cấp OTP mới và chỉ cho phép sau 60 giây. Giới hạn năm lần gửi trong một giờ và năm lần nhập sai cho mỗi challenge; sai quá giới hạn thì hủy challenge.

Mật khẩu được kiểm tra bằng các password validator Identity hiện có rồi hash trước khi lưu challenge. Khi xác thực thành công, service tạo `User` với hash đó trong transaction thay vì cần giữ lại plaintext password.

### Định danh tài khoản và đăng nhập

`User.Email` tiếp tục dùng cho tài khoản đăng ký email và được xác nhận sau OTP. Tài khoản đăng ký số điện thoại không có email; `PhoneNumber` và `PhoneNumberConfirmed` của ASP.NET Identity được dùng cho số đó.

`LoginRequest` đổi trường `Email` thành `Identifier` ở contract mới, nhưng endpoint vẫn nhận alias `email` để không làm hỏng client cũ. Backend nhận diện email hoặc số điện thoại đã chuẩn hóa, tìm User tương ứng, rồi áp dụng nguyên vẹn các kiểm tra password, lockout, moderation, 2FA và phát hành JWT đang có. Response user thêm `phoneNumber` nullable; `email` trở thành nullable cho tài khoản phone-only. Web điều chỉnh type và copy để hiển thị định danh khả dụng.

Luồng quên/đặt lại mật khẩu được mở rộng cùng kiểu `Identifier`: email nhận liên kết reset hiện có; phone nhận OTP reset qua SpeedSMS. Phần thay đổi mật khẩu sau khi đăng nhập không đổi. Điều này tránh việc tài khoản phone-only không thể phục hồi mật khẩu.

### Gửi thông điệp

Tạo interface gửi OTP theo contact (`IContactOtpSender`) với hai implementation: email sử dụng `IEmailSender`/SMTP hiện có và SMS sử dụng HTTP API của SpeedSMS. Cấu hình `Sms:Enabled`, `Sms:Provider`, `Sms:AccessToken`, `Sms:Sender`, timeout và URL base được validate khi startup; disabled mặc định.

Nếu contact là phone mà SMS chưa cấu hình hoặc nhà cung cấp lỗi, API trả lỗi `sms_unavailable`, không tạo challenge khả dụng. Với email, xử lý tương tự bằng `email_unavailable`. Log chỉ chứa user-independent correlation/id challenge; không log OTP, mật khẩu, API key hay số liên hệ đầy đủ.

## Luồng API

1. `POST /api/auth/registration/start` nhận toàn bộ form. API validate dữ liệu, contact và tuổi tối thiểu 13; từ chối contact đã thuộc tài khoản đang hoạt động; tạo challenge và gửi OTP. Response chỉ gồm challenge ID, thời điểm có thể gửi lại và thời điểm hết hạn.
2. Client chuyển sang màn hình “Xác nhận tài khoản”, khóa/mask contact, có ô OTP sáu chữ số, nút tiếp tục, quay lại chỉnh form và nút gửi lại với đếm ngược 60 giây.
3. `POST /api/auth/registration/verify` nhận challenge ID và OTP. Backend atomic-consume challenge, tạo User/Profile/PrivacySettings, đánh dấu email hoặc phone đã xác nhận, rồi trả `AuthenticationResponse` như đăng ký cũ.
4. `POST /api/auth/registration/resend` dùng challenge ID, áp dụng cooldown và limit, cấp OTP mới và gửi qua đúng kênh đã chọn.
5. Endpoint `POST /api/auth/register` cũ bị thay bằng lỗi hướng dẫn dùng luồng mới; cả web và tests chuyển sang endpoints mới trong cùng release để không còn lối tạo tài khoản bỏ qua OTP.

Các endpoint start/verify/resend dùng rate limiter sensitive authentication; response lỗi không tiết lộ contact đã tồn tại khi có thể tránh được. Hết hạn, replay, OTP sai, gửi lại quá sớm, gửi quá giới hạn và provider unavailable có code lỗi riêng nhưng thông báo công khai ngắn gọn.

## Frontend

`LoginPage` giữ nguyên màn hình đăng nhập, Google, 2FA và reset hiện có. Khi chọn “Tạo tài khoản”, form có:

- hai ô Họ và Tên;
- ba select ngày, tháng, năm; giới hạn tuổi tối thiểu 13;
- select giới tính gồm Nữ, Nam, Khác và Không muốn nêu;
- ô “Email hoặc số điện thoại” tự phát hiện định dạng;
- mật khẩu, điều khoản và nút “Gửi mã”.

Sau start thành công, form thay bằng màn xác nhận OTP theo mẫu người dùng cung cấp. Text nêu đúng email hoặc số điện thoại đã mask. “Bạn không nhận được mã?” gửi lại qua cùng kênh. Thành công gọi `AuthProvider` để lưu session và redirect feed. Lỗi validation hiển thị sát trường tương ứng, lỗi gửi/xác minh hiển thị trong alert form; không xóa thông tin người dùng đã nhập.

## Migration và tương thích

Thêm migration cho `RegistrationChallenges` và `UserProfiles.Gender`. Các bảng Identity đã có cột số điện thoại nên không tạo cột trùng. Thêm unique index có điều kiện hoặc kiểm tra transaction-safe cho phone number đã chuẩn hóa, tránh hai tài khoản dùng cùng số.

Giữ endpoint email verification cũ cho account đã tồn tại. Các email account mới qua OTP đã `EmailConfirmed=true`, nên không gửi thêm link xác minh. Các phone account không nhận email verification. Google account không thay đổi.

## Kiểm thử

- Integration tests: start bằng email/phone, validate contact và tuổi, gửi email/SMS bằng fake sender, resend cooldown/limit, OTP sai, expiry, replay, contact trùng, provisioning atomic User/Profile/PrivacySettings và xác nhận đúng contact.
- Login tests: email account legacy và phone-only account đăng nhập được; password sai, lockout, moderation và 2FA giữ hành vi cũ.
- Password recovery tests cho email link và phone OTP.
- Migration/snapshot tests, web typecheck/lint/build và focused Identity tests.

## Vận hành

SpeedSMS phải được đăng ký và nạp tiền bởi chủ dự án. Trước production, đặt `Sms__AccessToken` và các cấu hình cần thiết qua secret store/environment, bật `Sms__Enabled=true`, kiểm thử một số điện thoại thật và cấu hình giới hạn gửi phù hợp. Nếu chưa có credential SpeedSMS, email OTP vẫn hoạt động khi SMTP được cấu hình; đăng ký bằng phone trả `sms_unavailable` rõ ràng.

## Không thuộc phạm vi

- Xác minh hoặc thay đổi email/số điện thoại trong Settings sau đăng ký.
- SMS Brandname FPT/VNPT, số điện thoại quốc tế, Zalo ZNS hoặc fallback đa nhà cung cấp.
- Hiển thị gender công khai hay thêm bộ lọc theo gender.
