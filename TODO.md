# TODO

## Identity

- [ ] Dọn tự động các `RegistrationChallenge` đã xác minh hoặc hết hạn. Xóa challenge ngay sau khi tạo `User` thành công; thêm job định kỳ để xóa các challenge bị bỏ dở/hết hạn nhằm không lưu lâu `PasswordHash`, OTP hash và thông tin đăng ký.
