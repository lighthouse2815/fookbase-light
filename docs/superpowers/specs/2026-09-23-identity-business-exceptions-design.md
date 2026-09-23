# Identity: xử lý lỗi nghiệp vụ bằng exception

## Mục tiêu đã thống nhất

Service xác thực trả dữ liệu trực tiếp khi thành công và ném exception khi
thất bại nghiệp vụ. Controller trả phản hồi thành công, không kiểm tra
`ApplicationResult.Succeeded` và không thêm `try/catch` để đổi lỗi sang HTTP.
`GlobalExceptionHandler` chịu trách nhiệm trả lỗi tập trung.

## Phạm vi

- Chuyển `AuthenticationService`, `RegistrationUseCase`,
  `RegistrationChallengeService`, `GoogleAuthenticationService` và
  `IGoogleExternalIdentityReader` cùng implementation/test double tương ứng.
- Cập nhật tám controller Identity và helper lấy user ID.
- Thêm exception nghiệp vụ dùng chung và tích hợp handler hiện tại.
- Cập nhật test gọi trực tiếp các service trên, bổ sung test phản hồi lỗi và
  rollback khi exception đi qua các luồng đăng ký.
- `AdministrationService` phục vụ API Admin và các module khác tiếp tục dùng
  `ApplicationResult`. Không xóa kiểu dùng chung vừa gom vào Shared.
- Không thay database, dependency, quy tắc xác thực, chính sách 2FA hoặc cách
  chia service trong đợt này.

## Lựa chọn thiết kế

Giữ `ApplicationResult` và rút gọn controller bằng helper chuyển kết quả cũng
có thể giảm code, nhưng vẫn giữ cơ chế mà người dùng muốn thay. Chuyển toàn
backend sang exception ngay sẽ mở rộng phạm vi sang nhiều nghiệp vụ.

Chọn chuyển luồng xác thực Identity trước, dùng chung handler với phần còn lại
của backend. Đây là thay đổi giao diện nội bộ; hợp đồng HTTP phải được giữ.

## Exception và phản hồi HTTP

Thêm `BusinessException` trong `Shared/ErrorHandling`, chứa một
`ApplicationError`. Tái sử dụng `Code`, `Message`, `Type`, `Details` đang có;
không tạo thêm hệ thống mã lỗi hoặc subclass cho từng lỗi.

Tách phần tạo `ProblemDetails` trong `ApplicationResultExtensions` thành hàm
dùng chung cho cả `ToHttpResult` và `GlobalExceptionHandler`, để chỉ có một nơi
ánh xạ loại lỗi sang status code:

| Loại lỗi | HTTP |
| --- | --- |
| Validation | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| NotFound | 404 |
| Conflict | 409 |

Handler nhận diện `BusinessException` trước nhánh lỗi bất ngờ. Phản hồi giữ
nguyên `title`, `detail`, `code`, `errors` nếu có và content type
`application/problem+json`; thêm `requestId` theo convention của handler.
Không ghi lỗi nghiệp vụ dự kiến ở mức Error hoặc log mật khẩu/token.
Exception không được nhận diện vẫn trả lỗi 500 chung, giấu chi tiết nội bộ.
Lỗi JSON/body và validation MVC giữ đường xử lý hiện có.

## Service và controller

- `Task<ApplicationResult<T>>` đổi thành `Task<T>`; kết quả thành công không
  có dữ liệu đổi thành `Task`.
- Các nhánh thất bại nghiệp vụ ném `BusinessException` với đúng lỗi cũ.
- Giữ kiểm tra `IdentityResult.Succeeded` của ASP.NET Identity: đây là kết quả
  của framework, cần chuyển thành exception nghiệp vụ khi phù hợp.
- Giữ hai dạng phản hồi đăng nhập hiện tại: token hoặc challenge 2FA. Không
  thiết kế lại DTO đăng nhập trong đợt này.
- Controller giữ route, attribute phân quyền/rate limit, status thành công,
  `Location`, redirect và OAuth challenge. Loại bỏ kiểm tra kết quả service.
- Helper lấy user ID có thể ném lỗi `invalid_access_token` khi claim không hợp
  lệ, để không lặp nhánh trả lỗi trong controller.
- Kiểm tra giao thức Google ở HTTP boundary vẫn được phép tồn tại, nhưng lỗi
  có nội dung nghiệp vụ được ném tới handler thay vì tự tạo ProblemDetails.
- Google mobile phải tiếp tục đổi lỗi đọc external identity thành
  `invalid_mobile_google_login`; thực hiện chuyển đổi có giới hạn trong lớp
  đọc identity, không bắt mọi exception trong controller. Giữ nguyên phản hồi
  404 rỗng khi Google mobile chưa bật.

## Transaction và catch

Rà từng `catch` trước khi đổi nhánh return thành throw. Catch chỉ rollback rồi
rethrow được giữ. Catch gửi email/SMS không được vô tình nuốt BusinessException
từ bước xác thực hoặc phát hành session; giữ ranh giới bắt lỗi giao vận rõ ràng.
Không đổi cancellation thành lỗi nghiệp vụ giao vận.

Đăng ký tạo user/profile/privacy/session và Google tạo tài khoản phải vẫn
rollback khi thất bại trước commit. Tránh rollback hai lần khi một nhánh đang
rollback rồi return được đổi sang throw và đi qua catch rollback bên ngoài.
Giữ các cập nhật có chủ đích khi thất bại: số lần nhập sai, trạng thái challenge
và cleanup sau gửi OTP lỗi. Không bọc transaction lớn mới làm rollback các
cập nhật này. Không tự động retry hoặc đổi thời điểm tiêu thụ code/token.

## Tiêu chí kiểm chứng

1. Test handler cho 400/401/403/404/409, lỗi từng trường, request ID và lỗi 500
   không tiết lộ thông tin nhạy cảm; test mapping dùng chung vẫn qua.
2. Test HTTP cho sai thông tin đăng nhập, refresh token sai, validation mật
   khẩu, đăng ký trùng liên hệ và lỗi Google web/mobile giữ mã/status cũ.
3. Test rollback khi lỗi nghiệp vụ xảy ra sau một bước ghi dữ liệu nhưng trước
   commit; đối chiếu các test atomicity hiện có.
4. Test trực tiếp service kiểm tra DTO hoặc `BusinessException`, không xóa
   assertion nghiệp vụ cũ chỉ để test qua.
5. Build backend, chạy nhóm Identity và Shared, sau đó chạy
   `bash scripts/test-backend.sh` trên database test riêng cho sáu nhóm.
6. Controller Identity không còn kiểm tra `ApplicationResult.Succeeded` hoặc
   `try/catch` chuyển lỗi thành HTTP. Module chưa chuyển vẫn hoạt động.

## Trạng thái

Thiết kế để người dùng xem trước khi lập kế hoạch triển khai. Chưa sửa code
ứng dụng hoặc chạy test cho việc chuyển sang exception.
