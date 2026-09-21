# Photos & Albums V1

`MediaAsset` vẫn là nguồn metadata duy nhất của ảnh lưu trên Cloudinary. Album chỉ giữ `AlbumMedia` reference; không sao chép file ảnh.

## Album và riêng tư

- `Custom`: người dùng tạo, sửa và soft-delete.
- `ProfilePictures`, `CoverPhotos`, `TimelinePhotos`: album hệ thống tạo lazy, duy nhất theo user/type và không xoá như Custom.
- Privacy: `Public`, `Friends`, `OnlyMe`. Owner luôn truy cập được; block hai chiều luôn từ chối; URL ảnh chỉ được ký sau bước kiểm tra album.
- Mỗi ảnh chỉ thuộc một Custom album; ảnh vẫn có thể đồng thời được Post hoặc album hệ thống tham chiếu.

## Tích hợp hồ sơ và bài viết

- Avatar mới thêm vào `ProfilePictures`; cover user profile thêm vào `CoverPhotos`.
- Ảnh của Profile Standard Post thêm vào `TimelinePhotos`.
- Cover của Group/Page/Event và Story/Reel/Messenger không đi vào album user.
- Không thể xoá MediaAsset khi còn AlbumMedia reference. Gỡ ảnh khỏi album không xoá object; gỡ avatar/cover đang active bị từ chối.

## API và web

`/api/albums` cung cấp CRUD Custom album, danh sách media, caption, thêm/gỡ ảnh và detail/signed access. `/api/users/{userId}/albums` trả danh sách accessible.

Web cung cấp `/photos`, `/albums/:albumId`, tạo Custom album với upload lại Media flow hiện có và lightbox với đóng/Escape/phím mũi tên.

## Hoãn lại

Không có photo search, tag người trong ảnh, nhận diện, Memories, feed album, notification, drag-drop reorder, explicit cover hoặc cloud sync trong V1.
