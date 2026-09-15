# Google Sign-In cho Fookbase và Zola Light

## Mục tiêu

Thêm đăng nhập Google cho `frontend/web` và `frontend/zola-light` nhưng vẫn
phát hành JWT access token, refresh token, session và 2FA của Fookbase như
hiện có. Google chỉ là nhà cung cấp danh tính; browser không nhận Google
client secret hoặc Google access token.

Người dùng Google mới được tạo tài khoản Fookbase tự động. Người dùng có sẵn
với cùng email phải nhập đúng mật khẩu Fookbase để liên kết Google lần đầu.

## Phạm vi và ràng buộc

- Backend thực hiện OAuth Authorization Code flow bằng Google handler của
  ASP.NET Core.
- Chỉ `web` và `zola-light` là các client hợp lệ. API không nhận return URL
  tự do.
- Google claim phải có `sub`, email và `email_verified=true`.
- Google identity được nhận diện bằng cặp provider `Google` và `sub`; email
  chỉ dùng để tạo/liên kết lần đầu, không thay thế `sub`.
- Mọi token của Fookbase chỉ trả từ API JSON; không đặt token vào query string
  hay fragment URL.
- Tài khoản đã bật 2FA vẫn xác minh 2FA sau khi Google hoặc mật khẩu đã được
  chứng minh.
- OAuth Google không được khởi động trong WebView nhúng của Zalo/Messenger.
  Client hiển thị hướng dẫn mở Chrome/Safari; đăng nhập email/mật khẩu không
  bị ảnh hưởng.

## Kiến trúc

### Cấu hình và middleware

Thêm Google authentication handler và một external temporary cookie scheme.
JWT bearer vẫn là default scheme cho API authorization, vì vậy các endpoint
hiện có không thay đổi hành vi. Google handler chỉ dùng cho challenge và
callback OAuth.

Tạo options Google riêng gồm `Enabled`, `ClientId`, `ClientSecret`, URL base
của Fookbase web và Zola Light. `Enabled` mặc định `false`. Khi bật, startup
validate client ID/secret và hai URL absolute HTTPS trong production. Secret
được đặt bằng environment variables hoặc secret store; tracked config và
`.env.example` chỉ ghi tên biến.

Thêm package `Microsoft.AspNetCore.Authentication.Google` phù hợp .NET 10.
Google redirect URI được cố định tại callback của API và phải đăng ký chính
xác URI đó trong Google Cloud Console.

### Persistence

`FookbaseDbContext` vốn kế thừa `IdentityDbContext`, nên dùng sẵn
`AspNetUserLogins` qua `UserManager.AddLoginAsync` để lưu cặp Google provider
và provider key (`sub`). Không tạo một bảng external identity trùng lặp.

Thêm bảng completion ngắn hạn. Mỗi row lưu hash của mã ngẫu nhiên, client
target, mục đích, Google identity đã xác thực hoặc user ID, thời điểm hết hạn
và thời điểm dùng. Mã thô chỉ xuất hiện một lần trong redirect về client;
exchange luôn hash-compare, atomic consume và từ chối replay/hết hạn.
Mục đích bao gồm phát hành session trực tiếp, hoàn tất liên kết cần mật khẩu,
và hoàn tất sau 2FA. Record hết hạn được dọn cùng lối bảo trì dữ liệu hiện có.

## Luồng API

### Bắt đầu và callback

1. Client gọi/chuyển hướng tới `GET /api/auth/google/start?client=web` hoặc
   `client=zola-light`.
2. API validate client, tạo OAuth challenge với redirect URI nội bộ, rồi đưa
   browser đến Google.
3. Callback đọc external temporary cookie. API từ chối callback không có
   correlation hợp lệ, không có `sub`/email hoặc `email_verified` không đúng.
4. API xác định client target từ state server-protected, không từ URL callback.
   Sau khi xử lý, API redirect tới route login cố định của target, truyền đúng
   một completion code hoặc lỗi public ngắn gọn.

### Tài khoản đã liên kết

Nếu `AspNetUserLogins` có Google `sub`, API kiểm tra account active, lockout
và moderation giống password login. Nếu bật 2FA, API tạo challenge 2FA mang
theo completion context; nếu không, API tạo completion code phát hành session.
Client POST exchange code và nhận cùng `AuthenticationResponse` đang dùng.

### Tài khoản Google mới

Nếu không có login liên kết và không có User với normalized email đó, API tạo
trong một transaction:

- `User` không password với email xác thực từ Google và `EmailConfirmed=true`;
- username xác định từ local-part đã sanitize của email, giới hạn theo quy tắc
  hiện có và thêm hậu tố khi trùng;
- `UserProfile` và `UserPrivacySettings` qua các service hiện hữu;
- Google login qua `UserManager.AddLoginAsync`;
- session/refresh token hiện có hoặc challenge 2FA nếu về sau được yêu cầu.

Display name mặc định vẫn theo username, giữ đúng convention profile hiện
hữu. Tài khoản Google-only có thể đặt mật khẩu lần đầu bằng luồng quên mật
khẩu đã có.

### Email trùng nhưng chưa liên kết

Nếu email Google đã xác thực trùng một User chưa có Google login, callback
không đăng nhập và không liên kết ngay. Nó tạo completion code mục đích
`link-existing` và redirect về login UI với email đã khóa hiển thị.

Client gửi completion code cùng mật khẩu. API atomically kiểm tra:

- completion code còn hạn, chưa dùng và đúng client;
- User đích có email normalized bằng email Google;
- password hợp lệ, account active, không lockout/moderation;
- Google `sub` chưa được liên kết với User khác.

Sau khi đúng mật khẩu, API liên kết Google. Với user bật 2FA, việc liên kết và
phát hành session chỉ hoàn tất sau 2FA; nếu không bật 2FA, API tạo session như
login thường. Mật khẩu sai dùng lỗi chung, không tiết lộ account details.

## Frontend

`frontend/web` thêm hành động Google vào `LoginPage`, dùng `AuthProvider` để
đổi completion code lấy `AuthenticationResponse` và đi qua handling 2FA hiện
có. Login route hiểu các trạng thái callback ngắn hạn và giữ nguyên form
email/mật khẩu, đăng ký, quên mật khẩu và xác minh email.

`frontend/zola-light` thêm nút tương ứng vào login form. Sau callback, nó đổi
completion code và lưu `AuthSession` bằng các hàm hiện có. Hai client dùng API
contract chung, khác nhau duy nhất ở client key và URL route login cố định.

Client kiểm tra in-app browser theo user agent chỉ để UX fallback. Đây không
phải kiểm soát bảo mật: backend vẫn thực hiện OAuth protections và Google có
thể chặn WebView. Khi phát hiện Zalo/Messenger WebView, UI giải thích cần mở
Chrome/Safari, không tạo OAuth request; email/password vẫn dùng được.

## Xử lý lỗi và bảo mật

- Nút Google ẩn/disabled khi public provider-status endpoint nói Google chưa
  cấu hình; API cũng trả lỗi cấu hình an toàn nếu bị gọi trực tiếp.
- Callback Google cancel, OAuth state/correlation lỗi, provider error, thiếu
  claim, email chưa xác thực, unknown client, URL redirect sai, completion
  replay/hết hạn, password sai, account unavailable và 2FA sai đều trả lỗi
  public ngắn gọn. Không log raw OAuth code, completion code, client secret,
  access token hoặc refresh token.
- Login/link/exchange dùng rate limit sensitive auth phù hợp. Existing JWT,
  refresh rotation, session revocation, password reset và authorization không
  đổi contract.
- Chỉ scope tối thiểu `openid`, `email`, `profile`. Không lưu Google access
  token vì Fookbase không gọi Google API thay người dùng.
- Production deployment cần HTTPS callback, Google Cloud OAuth consent screen,
  redirect URI chính xác, homepage/terms/privacy policy trên domain sở hữu.

## Kiểm thử và tài liệu

Integration tests dùng external-auth test scheme/claims đã kiểm soát để không
gọi Google thật. Bao phủ start client validation, callback state failure,
Google user đã liên kết, provisioning account mới + profile/privacy + confirmed
email, email trùng cần đúng password, unverified email, duplicate provider
key, replay/expiry completion code, account unavailable, và Google flow với
2FA.

Frontend kiểm tra callback completion, link-password state, lỗi và WebView
fallback theo convention test hiện có; tối thiểu lint + production build cho
`web` và `zola-light` phải pass. README và production deployment docs mô tả
endpoint/public configuration cùng checklist Google Cloud, không ghi secret.

## Không thuộc phạm vi

- SSO cross-origin giữa Fookbase và Zola Light.
- Đăng nhập Google trong WebView Zalo/Messenger.
- Google API access, sync dữ liệu Google, One Tap hoặc native mobile SDK.
- Quản lý/hủy external login trong security settings; có thể làm milestone
  riêng sau khi Google sign-in ổn định.
