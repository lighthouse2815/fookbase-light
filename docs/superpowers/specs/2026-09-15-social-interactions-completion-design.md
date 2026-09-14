# Social Interactions Completion Design

## Mục tiêu

Hoàn thiện các luồng tương tác còn thiếu quanh reaction: người xem có thể gửi lời
mời kết bạn từ danh sách người đã reaction, xem toàn bộ danh sách theo từng loại
cảm xúc, và reaction cho bình luận. Đồng thời tài liệu API và các checklist kế
hoạch phải phản ánh trạng thái triển khai thực tế.

## Phạm vi

### Danh sách reaction bài viết

- API reaction list trả thêm trạng thái quan hệ của người xem với mỗi reactor.
- Popup hiển thị nút `Thêm bạn bè` chỉ khi quan hệ là `none`; bấm nút gửi friend
  request bằng Friends API và cập nhật trạng thái ngay tại dòng đó.
- API đã phân trang offset/limit. Web lấy trang đầu 100 mục và có nút `Xem thêm`
  khi còn mục; tab `Tất cả` và từng reaction giữ bộ lọc khi tải thêm.

### Reaction bình luận

- Web thêm reaction picker sáu loại hiện có (`like`, `love`, `haha`, `wow`, `sad`,
  `angry`) vào từng bình luận, sử dụng các endpoint hiện có.
- Chọn một loại sẽ đặt hoặc đổi reaction của viewer. Chọn lại cùng loại sẽ gỡ.
- UI chỉ hiển thị tổng và emoji reaction của comment; không mở danh sách reactors
  cho comment trong phạm vi này vì backend chưa có endpoint đọc tương ứng.

### Tài liệu và checklist

- README bổ sung các endpoint public đã được dùng bởi web, trước hết là post
  reactions và comment reactions.
- Các plan cũ được đánh dấu hoàn thành chỉ khi source, test hoặc commit lịch sử
  chứng minh mục đó đã có; các bước validation không có bằng chứng sẽ giữ mở.

## An toàn và lỗi

- Reaction list tiếp tục áp dụng quyền xem post hiện có; friend request tiếp tục
  áp dụng toàn bộ kiểm tra block/quyền trong Friends module.
- Request lỗi không thay đổi trạng thái hiển thị thành công và thông báo lỗi tại
  dòng đang thao tác.
- Tải thêm tránh request chồng lấp và hiển thị nút thử lại nếu request thất bại.

## Kiểm tra

- Bổ sung integration test cho trạng thái quan hệ trong reaction list và gửi
  friend request từ người đọc hợp lệ.
- Bổ sung hoặc mở rộng test comment reaction tại endpoint đã có.
- Chạy focused backend tests, web lint/build, và kiểm tra diff trước mỗi commit.
