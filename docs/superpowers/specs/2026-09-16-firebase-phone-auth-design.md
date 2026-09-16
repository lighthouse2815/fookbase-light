# Firebase Phone Auth Design

## Mục tiêu

Cho phép người dùng đăng ký và đặt lại mật khẩu bằng số điện thoại Việt Nam qua Firebase Phone Auth. Email tiếp tục dùng luồng OTP email hiện có. Backend chỉ tạo hoặc thay đổi tài khoản sau khi xác minh một Firebase ID token hợp lệ.

## Phạm vi

- Điện thoại dùng Firebase Phone Auth trên web, với reCAPTCHA của Firebase.
- Email giữ nguyên endpoint và OTP nội bộ hiện có.
- Đăng ký bằng điện thoại xác minh Firebase ID token trước khi provision `User`, `Profile`, `PrivacySettings` và JWT Fookbase.
- Đặt lại mật khẩu bằng điện thoại cũng yêu cầu Firebase ID token đã xác minh.
- Đăng nhập bằng email hoặc số điện thoại/mật khẩu giữ nguyên.

Không thay đổi đăng nhập Google, 2FA TOTP, Brandname/SpeedSMS, hay UI không liên quan.

## Kiến trúc

```text
Web browser                     Firebase                 Fookbase API
-----------                     --------                 ------------
phone + reCAPTCHA ────────────> sends SMS
OTP code ─────────────────────> verifies code
<── Firebase ID token ─────────
Firebase ID token ─────────────────────────────────────> verify signature,
                                                          issuer, audience,
                                                          expiration and phone
                                                        -> provision/reset
                                                        -> Fookbase JWT
```

Frontend dùng Firebase Web SDK để gọi `signInWithPhoneNumber`, nhận confirmation result và đổi mã SMS thành Firebase ID token. Token chỉ được gửi đến API qua request TLS; không được ghi vào local storage, log ứng dụng hay URL.

Backend dùng Firebase Admin SDK với application-default credentials hoặc JSON service-account từ environment. Một service xác minh ID token, kiểm tra `aud`/`iss` đúng Firebase project, token còn hạn, `phone_number` tồn tại và khớp chính xác số điện thoại chuẩn hóa trong request. API không tin `uid` hoặc số điện thoại do browser gửi nếu không khớp claim Firebase.

## Giao diện API

Email không đổi.

Với số điện thoại, endpoint tạo hoặc gửi lại registration challenge không gửi SMS từ backend. API có thể trả metadata Firebase cần thiết để frontend quyết định dùng Firebase. Endpoint hoàn tất registration nhận `firebaseIdToken` thay cho OTP nội bộ khi contact là số điện thoại.

Endpoint đặt lại mật khẩu bằng số điện thoại nhận Firebase ID token cùng mật khẩu mới. Request OTP nội bộ và verify OTP nội bộ cho số điện thoại được loại khỏi luồng active; endpoint email reset vẫn không đổi.

Request và response phải thể hiện rõ một trong hai phương thức xác minh, tránh một field `code` mơ hồ:

- `verificationMethod: "emailOtp" | "firebasePhone"`
- `emailOtpCode` chỉ dùng với email.
- `firebaseIdToken` chỉ dùng với số điện thoại.

## Cấu hình

Frontend dùng các biến public Vite:

```env
VITE_FIREBASE_API_KEY=
VITE_FIREBASE_AUTH_DOMAIN=
VITE_FIREBASE_PROJECT_ID=fookbase-cd714
VITE_FIREBASE_APP_ID=
```

Các giá trị này không phải secret, nhưng chỉ domain production/local được thêm vào Firebase Authorized domains.

Backend production dùng một trong hai cách credential:

- Workload/Application Default Credentials nếu runtime Google Cloud cung cấp identity; hoặc
- `Firebase__ServiceAccountJson` trong secret store/environment, không commit, không in log.

`Firebase__ProjectId=fookbase-cd714` là cấu hình server-side bắt buộc. Khi Firebase Phone Auth chưa được cấu hình đầy đủ, backend từ chối phone flow bằng lỗi cấu hình rõ ràng; email flow vẫn hoạt động.

## Xử lý lỗi và chống lạm dụng

- Firebase reCAPTCHA xử lý chống bot trước khi SMS được gửi.
- API giữ rate limit auth-sensitive hiện có cho các endpoint provision/reset.
- Token Firebase sai chữ ký, sai project, hết hạn hoặc không có claim điện thoại trả lỗi xác minh chung, không rò chi tiết token.
- Số Firebase và số form được chuẩn hóa E.164 `+84...` trước khi so sánh.
- Không tạo account, không xác nhận phone và không thay đổi mật khẩu nếu token không hợp lệ.

## Kiểm thử

- Backend unit/integration test service xác minh: token hợp lệ, sai audience, hết hạn, phone claim thiếu, phone mismatch.
- Endpoint test đảm bảo phone registration/reset từ chối token sai và provision/reset khi verifier hợp lệ.
- Frontend test module tách Firebase adapter để fake confirmation result; kiểm tra token chỉ được đính vào phone flow.
- Chạy Identity integration tests, build/lint frontend và migration check. Không cần migration vì không đổi schema.

## Triển khai

1. Đăng ký web app trong Firebase project `fookbase-cd714` và thêm production/local domains vào Firebase Authentication.
2. Đặt cấu hình public Vite ở môi trường build web.
3. Đặt Firebase project ID và service-account credential trong secret production.
4. Deploy API và web.
5. Test một số điện thoại thật trong quota 10 SMS/ngày, sau đó theo dõi tab Firebase Authentication Usage.

