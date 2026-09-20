# Google login cho app mobile

App dùng endpoint riêng `/api/auth/google/mobile/*`; web và Zola giữ hợp đồng hiện tại. Chỉ bật khi Google đã cấu hình và `GoogleAuthentication:MobileCallbackUrl` là URL HTTPS cố định, không query/fragment/userinfo, được Android App Links/iOS Universal Links xác minh.

Start nhận state ngẫu nhiên và SHA-256 PKCE challenge. Gắn client/state/challenge vào AuthenticationProperties được OAuth middleware bảo vệ, không đọc chúng từ query callback. External identity reader trả các thuộc tính này cùng danh tính Google sau khi xác thực cookie bên ngoài.

Callback tạo completion bằng service hiện có với client mobile; bọc mã gốc và challenge trong Data Protection token có hạn 5 phút. Callback chỉ mang completion token, state và mode link; không access/refresh token. Exchange/link giải mã và kiểm tra verifier trước khi gọi service completion hiện có. Database hiện tại tiếp tục chịu trách nhiệm dùng mã một lần, không cần migration mới. Data Protection key ring production phải bền vững và dùng chung theo cấu hình triển khai hiện có.

App tạo verifier/state, lưu giao dịch ngắn hạn trong SecureStore để phục hồi cold start. Chỉ chấp nhận callback đúng origin/path/state, hủy mã quá hạn. Browser hệ thống thực hiện Google OAuth. Link password và 2FA dùng đúng challenge API. Logout/hủy không đưa user về giao dịch cũ.

Kiểm thử: challenge/verifier sai; token sửa/expired; mobile callback thiếu thuộc tính đã bảo vệ; completion replay; callback web/Zola không bị đổi; cancel/cold start và đăng nhập thật cần domain/certificate cùng tài khoản thử.
