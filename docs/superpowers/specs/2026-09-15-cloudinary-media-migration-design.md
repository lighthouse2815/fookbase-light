# Chuyển lưu trữ media từ MinIO sang Cloudinary

## Mục tiêu

Chuyển hoàn toàn việc lưu ảnh và video của Fookbase từ MinIO sang Cloudinary. Ứng dụng vẫn phải cho phép browser tải trực tiếp file lớn, không đưa `api_secret` ra client, giữ nguyên luồng `MediaAsset`, kiểm tra quyền đọc và worker FFmpeg cho video/Reel/Story.

Quyết định này giả định không có media MinIO cần tiếp tục phục vụ. Không có migration schema và không tự xóa dữ liệu PostgreSQL hay volume MinIO.

## Phạm vi và không thuộc phạm vi

Bao gồm:

- ảnh JPEG, PNG, WebP và video MP4, WebM;
- hai client có upload hiện tại: `frontend/web` và `frontend/zola-light`;
- upload, xác nhận upload, đọc, xoá, worker video, health check và Docker Compose;
- tài liệu cấu hình/deploy.

Không bao gồm:

- chuyển file MinIO cũ sang Cloudinary;
- đổi mô hình quyền riêng tư của Post/Story/Album;
- dùng Cloudinary Upload Widget, unsigned preset, transformation CDN hay bỏ FFmpeg;
- proxy luồng media qua API.

## Kiến trúc được chọn

```text
Browser --POST /api/media/uploads--> API --lưu PendingUpload--> PostgreSQL
Browser <-- upload URL + signed form fields -- API (Cloudinary secret chỉ tại đây)
Browser --multipart/form-data-------------------------------> Cloudinary
Browser --POST /api/media/{id}/complete--> API --xác minh--> Cloudinary

API/worker --Cloudinary SDK--> download source, upload output, destroy asset
Browser <-- API đã kiểm tra quyền <-- signed authenticated delivery URL
```

`MediaAsset.ObjectKey`, `ProcessedObjectKey` và `PosterObjectKey` được giữ nguyên trong database, nhưng từ thời điểm chuyển đổi chúng mang nghĩa **Cloudinary public ID**. Các key deterministic hiện có (theo owner/media ID, `processed.mp4`, `poster.jpg`) được tái sử dụng; do đó không cần cột, backfill hay migration EF.

Mọi asset được upload với Cloudinary `resource_type` tương ứng (`image` hoặc `video`) và delivery `type=authenticated`. Cloudinary là nơi lưu source, video MP4 chuẩn hoá và poster JPEG; worker FFmpeg vẫn là nơi chuẩn hoá và lấy metadata video để các ràng buộc Reel/Story hiện có không thay đổi.

## Upload trực tiếp có chữ ký

`POST /api/media/uploads` tiếp tục xác thực người dùng, kiểm tra tên file, MIME type và kích thước khai báo, tạo `MediaAsset` ở trạng thái `PendingUpload` trước khi trả intent. Response được mở rộng thành:

```json
{
  "mediaId": "...",
  "uploadUrl": "https://api.cloudinary.com/v1_1/<cloud-name>/<resource-type>/upload",
  "uploadMethod": "POST",
  "uploadParameters": {
    "api_key": "...",
    "timestamp": "...",
    "signature": "...",
    "public_id": "<owner>/<media>",
    "type": "authenticated",
    "overwrite": "false"
  },
  "expiresAtUtc": "..."
}
```

`api_key` có thể xuất hiện trong browser; `api_secret` tuyệt đối không xuất hiện trong response, JavaScript, log hoặc repository. API ký toàn bộ tham số upload có thể thay đổi bằng SHA-256. Client gửi tất cả `uploadParameters` và trường `file` qua `multipart/form-data`; không còn `PUT` object storage hay header `Content-Type` tự đặt.

Cloudinary chấp nhận chữ ký upload tối đa một giờ. Backend sẽ tạo timestamp lùi phù hợp để chữ ký hết hạn không muộn hơn `Media.UploadUrlExpiryMinutes` (mặc định 15 phút), và dùng cùng thời hạn cho `expiresAtUtc`. Điều này ngăn pending intent bị upload lại sau khi worker dọn dẹp. Việc này phải được xác nhận bằng integration test có credential Cloudinary trước deploy production.

`frontend/web` giữ API công khai `mediaApi.uploadFile`, `uploadFileWithMetadata` và callback progress; chỉ helper transport được đổi sang `XMLHttpRequest` `POST` với `FormData`. `frontend/zola-light` đổi cùng contract. Đây là thay đổi response có phối hợp: backend và hai frontend phải được deploy cùng release; không hỗ trợ client cũ gửi `PUT` MinIO.

## Xác minh, đọc và xoá

`POST /api/media/{id}/complete` không tin response từ browser. Với public ID đã định sẵn, backend truy vấn Cloudinary để kiểm tra asset tồn tại, delivery type, resource type, bytes và định dạng; sau đó đọc prefix qua authenticated signed URL để giữ kiểm tra magic bytes hiện có. Chỉ asset khớp MIME type và kích thước khai báo mới thành `Ready` hoặc vào hàng đợi video.

Adapter Cloudinary thay thế implementation MinIO của `IObjectStorage`:

- tạo upload intent có chữ ký thay cho presigned PUT;
- lấy metadata, đọc prefix và tải source xuống file cho `MediaService`/FFmpeg;
- upload file MP4/poster từ worker với `authenticated` delivery type;
- tạo signed delivery URL và `destroy` asset theo public ID/resource type.

`ObjectDeletionWorker` và `PendingUploadCleanupWorker` vẫn dùng bảng `ObjectDeletions`, retry và key hiện hữu. Cloudinary `destroy` phải gửi `invalidate=true` để xóa cache CDN sau khi asset bị xóa. Lỗi tạm thời vẫn đi qua retry durable hiện tại.

Các endpoint access và endpoint nội bộ đang phát `MediaReadUrlResponse` vẫn là điểm kiểm tra quyền. Chúng trả URL delivery Cloudinary có chữ ký cho authenticated asset, thay cho presigned GET MinIO. Signed delivery URL chặn truy cập không ký và URL đoán được, **nhưng không tự hết hạn trên Cloudinary gói thường**; ai nhận được URL vẫn có thể chia sẻ nó. Nếu sau này cần link hết hạn tuyệt đối, cần Cloudinary Advanced token/cookie access control hoặc đổi sang API proxy, cả hai đều ngoài phạm vi release này.

## Cấu hình, an toàn và health check

Thêm section `Cloudinary` và chỉ cung cấp credential qua biến môi trường:

```dotenv
Cloudinary__CloudName=<cloud-name>
Cloudinary__ApiKey=<api-key>
Cloudinary__ApiSecret=<api-secret>
```

`CloudName`, `ApiKey` và `ApiSecret` đều bắt buộc khi chạy Production; validator fail fast khi thiếu. `ApiSecret` chỉ có trong môi trường API/CI secret store, không đưa vào `.env.example` với giá trị thật. Tài khoản Cloudinary cần cho phép upload browser từ các HTTPS origin của web, admin và Zola Light qua CORS/cấu hình product environment.

Thêm package .NET chính thức của Cloudinary, cấu hình SDK dùng HTTPS và SHA-256. Không thêm SDK JavaScript: upload dùng `FormData`/`XMLHttpRequest` sẵn có. Thay `MinioBucketHealthCheck` bằng `CloudinaryHealthCheck` gọi API Cloudinary có timeout ngắn; `/health/ready` tiếp tục chỉ sẵn sàng khi cả PostgreSQL lẫn Cloudinary truy cập được. `/health/live` không phụ thuộc Cloudinary.

## Thay đổi deployment

Xóa dependency MinIO khỏi API và DI, gồm NuGet package, options, client, bucket initializer, presigned URL client, MinIO object storage và health check. Xóa service `minio`, `minio-cors`, `minio-bootstrap`, cấu hình/biến MinIO và dependency `depends_on` của chúng khỏi `compose.yml` và `compose.prod.yml`. Không chạy lệnh xóa named volume `minio-data`; Docker có thể giữ volume cũ để rollback dữ liệu hạ tầng, dù release mới không dùng nó.

`docs/production-deployment.md`, `.env.example` và README sẽ đổi mọi hướng dẫn credential, CORS, readiness, capacity và backup từ MinIO sang Cloudinary. Backup PostgreSQL và key ring Data Protection vẫn bắt buộc; Cloudinary media được quản lý/backup theo công cụ và chính sách của Cloudinary, không còn nằm trong Docker volume.

Trước deploy cần tạo Cloudinary product environment, đặt secret trên server, cho phép các origin browser, chạy migration check (dự kiến không có migration mới), build image, rồi deploy API và hai client cùng release. Smoke test phải gồm ảnh, video, poster, upload quá hạn, unauthorized access, xoá và `/health/ready`.

Rollback ứng dụng chỉ an toàn khi chưa có media Cloudinary mới được người dùng cần truy cập. Release cũ không biết các public ID Cloudinary và không thể đọc chúng từ MinIO. Do đó rollback sau khi có upload mới cần chuyển tiếp release Cloudinary hoặc có kế hoạch migration riêng; không được xóa volume hay database để rollback.

## Kiểm chứng khi triển khai

Mã nguồn sẽ có unit/integration coverage cho tạo intent, tham số ký, hoàn tất upload, read URL và delete retry; test Cloudinary thật chỉ chạy bằng credential thử nghiệm tách riêng. Theo yêu cầu hiện tại, không thêm hoặc chạy automated test trong bước tài liệu này. Khi implementation được duyệt, tối thiểu chạy build API/Docker, lint và build cả frontend; integration thật chỉ chạy khi môi trường test có Cloudinary credential.

## Nguồn tham khảo

- [Cloudinary signed upload and authentication signatures](https://cloudinary.com/documentation/authentication_signatures)
- [Cloudinary Upload API](https://cloudinary.com/documentation/image_upload_api_reference)
- [Cloudinary authenticated delivery](https://cloudinary.com/documentation/control_access_to_media)
- [Cloudinary .NET SDK](https://cloudinary.com/documentation/dotnet_integration)
